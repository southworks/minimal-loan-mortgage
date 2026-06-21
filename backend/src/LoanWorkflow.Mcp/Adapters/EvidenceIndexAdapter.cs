using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class EvidenceIndexAdapter
{
    public const string CustomerContextSourceType = "customer-context";
    public const string WorkflowPayloadSourceType = "workflow-payload";

    public static string CreateCaseSourceKey(string caseId) => $"case:{caseId.Trim()}";

    public static string CreateCustomerContextSourceKey(string caseId) => $"assets:{caseId.Trim()}";
    private const string MetadataDocumentType = "metadata";

    private readonly SearchClient _searchClient;
    private readonly FoundryEmbeddingService _embeddingService;
    private readonly FoundryRerankService _rerankService;
    private readonly AzureSearchOptions _options;

    public EvidenceIndexAdapter(
        SearchIndexClient indexClient,
        FoundryEmbeddingService embeddingService,
        FoundryRerankService rerankService,
        IOptions<AzureSearchOptions> options)
    {
        _options = options.Value;
        _searchClient = indexClient.GetSearchClient(_options.EvidenceIndexName);
        _embeddingService = embeddingService;
        _rerankService = rerankService;
    }

    public async Task<IndexCaseDocumentsResponse> IndexDocumentsAsync(
        string caseId,
        string executionId,
        IReadOnlyList<CaseDocument> documents,
        string sourceType = WorkflowPayloadSourceType,
        string? sourceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);

        var effectiveSourceKey = string.IsNullOrWhiteSpace(sourceKey) ? sourceType : sourceKey.Trim();
        var contentHash = ComputeDocumentsHash(documents);
        var metadataId = CreateMetadataId(caseId, executionId, sourceType, effectiveSourceKey);

        var storedMetadata = await GetMetadataAsync(metadataId, cancellationToken);
        if (storedMetadata is not null
            && string.Equals(storedMetadata.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            return new IndexCaseDocumentsResponse
            {
                CaseId = caseId,
                ExecutionId = executionId,
                IndexName = _options.EvidenceIndexName,
                SourceType = sourceType,
                SourceKey = effectiveSourceKey,
                ContentHash = contentHash,
                IndexedDocumentCount = storedMetadata.SourceDocumentCount,
                ChunkCount = storedMetadata.ChunkCount,
                AlreadyIndexed = true
            };
        }

        var chunks = new List<EvidenceChunkDocument>();

        foreach (var document in documents)
        {
            var chunkTexts = ChunkText(document.SummaryText);
            for (var index = 0; index < chunkTexts.Count; index++)
            {
                chunks.Add(new EvidenceChunkDocument
                {
                    Id = CreateChunkId(caseId, executionId, document.DocumentId, index),
                    CaseId = caseId,
                    ExecutionId = executionId,
                    DocumentId = document.DocumentId,
                    DocumentType = document.DocumentType,
                    Category = document.Category,
                    SourceType = sourceType,
                    SourceKey = effectiveSourceKey,
                    ContentHash = contentHash,
                    IndexedAtUtc = DateTimeOffset.UtcNow,
                    SourceDocumentCount = documents.Count,
                    ChunkCount = 0,
                    ChunkText = chunkTexts[index],
                    SourcePath = document.SourcePath
                });
            }
        }

        if (chunks.Count == 0)
        {
            await UpsertMetadataAsync(
                metadataId,
                caseId,
                executionId,
                sourceType,
                effectiveSourceKey,
                contentHash,
                documents.Count,
                chunkCount: 0,
                _options.VectorDimensions,
                cancellationToken);

            return new IndexCaseDocumentsResponse
            {
                CaseId = caseId,
                ExecutionId = executionId,
                IndexName = _options.EvidenceIndexName,
                SourceType = sourceType,
                SourceKey = effectiveSourceKey,
                ContentHash = contentHash,
                IndexedDocumentCount = 0,
                ChunkCount = 0,
                AlreadyIndexed = false
            };
        }

        var embeddings = await _embeddingService.EmbedAsync(
            chunks.Select(chunk => chunk.ChunkText).ToArray(),
            cancellationToken);

        for (var index = 0; index < chunks.Count; index++)
        {
            chunks[index].Embedding = embeddings[index];
            chunks[index].ChunkCount = chunks.Count;
        }

        chunks.Add(CreateMetadataDocument(
            metadataId,
            caseId,
            executionId,
            sourceType,
            effectiveSourceKey,
            contentHash,
            documents.Count,
            chunks.Count,
            _options.VectorDimensions));

        var batch = IndexDocumentsBatch.Upload(chunks);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

        return new IndexCaseDocumentsResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            IndexName = _options.EvidenceIndexName,
            SourceType = sourceType,
            SourceKey = effectiveSourceKey,
            ContentHash = contentHash,
            IndexedDocumentCount = documents.Count,
            ChunkCount = chunks.Count - 1,
            AlreadyIndexed = false
        };
    }

    public async Task<IReadOnlyList<EvidenceMatch>> SearchAsync(
        string caseId,
        string executionId,
        string query,
        int topK,
        string? sourceType = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await SearchCandidatesAsync(
            caseId,
            executionId,
            query,
            category: null,
            sourceType,
            candidateSize: Math.Max(topK * 3, topK),
            cancellationToken);

        return await RerankEvidenceAsync(query, candidates, topK, cancellationToken);
    }

    public async Task<IReadOnlyList<EvidenceMatch>> SearchCategoryAsync(
        string caseId,
        string executionId,
        string category,
        string query,
        int topK,
        string? sourceType = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await SearchCandidatesAsync(
            caseId,
            executionId,
            query,
            category,
            sourceType,
            candidateSize: Math.Max(topK * 2, topK),
            cancellationToken);

        return await RerankEvidenceAsync(query, candidates, topK, cancellationToken);
    }

    private async Task<IReadOnlyList<EvidenceSearchCandidate>> SearchCandidatesAsync(
        string caseId,
        string executionId,
        string query,
        string? category,
        string? sourceType,
        int candidateSize,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = (await _embeddingService.EmbedAsync([query], cancellationToken)).Single();

        var filter = $"caseId eq '{EscapeFilterValue(caseId)}' and executionId eq '{EscapeFilterValue(executionId)}' and documentType ne '{MetadataDocumentType}'";
        if (!string.IsNullOrWhiteSpace(category))
        {
            filter += $" and category eq '{EscapeFilterValue(category)}'";
        }

        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            filter += $" and sourceType eq '{EscapeFilterValue(sourceType)}'";
        }

        var searchOptions = new SearchOptions
        {
            Size = candidateSize,
            Filter = filter,
            Select = { "documentId", "documentType", "category", "chunkText" }
        };

        searchOptions.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizedQuery(queryEmbedding)
                {
                    KNearestNeighborsCount = candidateSize,
                    Fields = { "embedding" }
                }
            }
        };

        var response = await _searchClient.SearchAsync<EvidenceSearchCandidate>(null, searchOptions, cancellationToken);
        var candidates = new List<EvidenceSearchCandidate>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            if (result.Document is not null)
            {
                candidates.Add(result.Document);
            }
        }

        return candidates;
    }

    private async Task<IReadOnlyList<EvidenceMatch>> RerankEvidenceAsync(
        string query,
        IReadOnlyList<EvidenceSearchCandidate> candidates,
        int topK,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        if (candidates.Count <= topK)
        {
            return candidates
                .Select(candidate => new EvidenceMatch
                {
                    DocumentId = candidate.DocumentId ?? string.Empty,
                    DocumentType = candidate.DocumentType ?? string.Empty,
                    Category = candidate.Category ?? string.Empty,
                    Snippet = candidate.ChunkText ?? string.Empty,
                    Score = 1
                })
                .ToArray();
        }

        var reranked = await _rerankService.RerankAsync(
            query,
            candidates.Select(candidate => candidate.ChunkText ?? string.Empty).ToArray(),
            topK,
            cancellationToken);

        return reranked
            .Select(result => new EvidenceMatch
            {
                DocumentId = candidates[result.Index].DocumentId ?? string.Empty,
                DocumentType = candidates[result.Index].DocumentType ?? string.Empty,
                Category = candidates[result.Index].Category ?? string.Empty,
                Snippet = candidates[result.Index].ChunkText ?? string.Empty,
                Score = result.Score
            })
            .ToArray();
    }

    private static List<string> ChunkText(string text, int maxChunkLength = 1200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = new List<string>();
        var currentLength = 0;

        foreach (var line in lines)
        {
            if (currentLength + line.Length > maxChunkLength && current.Count > 0)
            {
                chunks.Add(string.Join(Environment.NewLine, current));
                current.Clear();
                currentLength = 0;
            }

            current.Add(line);
            currentLength += line.Length;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join(Environment.NewLine, current));
        }

        return chunks;
    }

    private static string EscapeFilterValue(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private async Task<EvidenceMetadataDocument?> GetMetadataAsync(
        string metadataId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _searchClient.GetDocumentAsync<EvidenceMetadataDocument>(
                metadataId,
                new GetDocumentOptions
                {
                    SelectedFields = { "contentHash", "sourceDocumentCount", "chunkCount" }
                },
                cancellationToken: cancellationToken);

            return response.Value;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (JsonException)
        {
            // Legacy or partial metadata documents may not match the current schema; treat as missing.
            return null;
        }
    }

    private async Task UpsertMetadataAsync(
        string metadataId,
        string caseId,
        string executionId,
        string sourceType,
        string sourceKey,
        string contentHash,
        int sourceDocumentCount,
        int chunkCount,
        int vectorDimensions,
        CancellationToken cancellationToken)
    {
        var metadata = CreateMetadataDocument(
            metadataId,
            caseId,
            executionId,
            sourceType,
            sourceKey,
            contentHash,
            sourceDocumentCount,
            chunkCount,
            vectorDimensions);

        var batch = IndexDocumentsBatch.Upload([metadata]);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    private static EvidenceChunkDocument CreateMetadataDocument(
        string metadataId,
        string caseId,
        string executionId,
        string sourceType,
        string sourceKey,
        string contentHash,
        int sourceDocumentCount,
        int chunkCount,
        int vectorDimensions) =>
        new()
        {
            Id = metadataId,
            CaseId = caseId,
            ExecutionId = executionId,
            DocumentId = metadataId,
            DocumentType = MetadataDocumentType,
            Category = MetadataDocumentType,
            SourceType = sourceType,
            SourceKey = sourceKey,
            ContentHash = contentHash,
            IndexedAtUtc = DateTimeOffset.UtcNow,
            SourceDocumentCount = sourceDocumentCount,
            ChunkCount = chunkCount,
            ChunkText = $"Evidence index metadata for {sourceType}:{sourceKey}",
            SourcePath = sourceKey,
            Embedding = new float[vectorDimensions]
        };

    private static string CreateMetadataId(
        string caseId,
        string executionId,
        string sourceType,
        string sourceKey) =>
        $"metadata-{HashKey($"{caseId}:{executionId}:{sourceType}:{sourceKey}")}";

    private static string CreateChunkId(
        string caseId,
        string executionId,
        string documentId,
        int index) =>
        $"chunk-{HashKey($"{caseId}:{executionId}:{documentId}:{index}")}";

    private static string ComputeDocumentsHash(IReadOnlyList<CaseDocument> documents)
    {
        var builder = new StringBuilder();
        foreach (var document in documents
            .OrderBy(document => document.DocumentId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(document.DocumentId);
            builder.AppendLine(document.DocumentType);
            builder.AppendLine(document.Category);
            builder.AppendLine(document.SourcePath);
            builder.AppendLine(document.SummaryText);
        }

        return HashKey(builder.ToString());
    }

    private static string HashKey(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private sealed class EvidenceSearchCandidate
    {
        public string? DocumentId { get; set; }

        public string? DocumentType { get; set; }

        public string? Category { get; set; }

        public string? ChunkText { get; set; }
    }

    private sealed class EvidenceMetadataDocument
    {
        public string? ContentHash { get; set; }

        public int SourceDocumentCount { get; set; }

        public int ChunkCount { get; set; }
    }

    public sealed class EvidenceChunkDocument
    {
        public required string Id { get; set; }

        public required string CaseId { get; set; }

        public required string ExecutionId { get; set; }

        public required string DocumentId { get; set; }

        public required string DocumentType { get; set; }

        public required string Category { get; set; }

        public required string SourceType { get; set; }

        public required string SourceKey { get; set; }

        public required string ContentHash { get; set; }

        public DateTimeOffset IndexedAtUtc { get; set; }

        public int SourceDocumentCount { get; set; }

        public int ChunkCount { get; set; }

        public required string ChunkText { get; set; }

        public required string SourcePath { get; set; }

        public IReadOnlyList<float>? Embedding { get; set; }
    }
}
