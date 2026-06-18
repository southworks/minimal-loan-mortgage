using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class EvidenceIndexAdapter
{
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
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<EvidenceChunkDocument>();

        foreach (var document in documents)
        {
            var chunkTexts = ChunkText(document.SummaryText);
            for (var index = 0; index < chunkTexts.Count; index++)
            {
                chunks.Add(new EvidenceChunkDocument
                {
                    Id = $"{caseId}:{executionId}:{document.DocumentId}:{index}",
                    CaseId = caseId,
                    ExecutionId = executionId,
                    DocumentId = document.DocumentId,
                    DocumentType = document.DocumentType,
                    Category = document.Category,
                    ChunkText = chunkTexts[index],
                    SourcePath = document.SourcePath
                });
            }
        }

        if (chunks.Count == 0)
        {
            return new IndexCaseDocumentsResponse
            {
                CaseId = caseId,
                ExecutionId = executionId,
                IndexName = _options.EvidenceIndexName,
                IndexedDocumentCount = 0,
                ChunkCount = 0
            };
        }

        var embeddings = await _embeddingService.EmbedAsync(
            chunks.Select(chunk => chunk.ChunkText).ToArray(),
            cancellationToken);

        for (var index = 0; index < chunks.Count; index++)
        {
            chunks[index].Embedding = embeddings[index];
        }

        var batch = IndexDocumentsBatch.Upload(chunks);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

        return new IndexCaseDocumentsResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            IndexName = _options.EvidenceIndexName,
            IndexedDocumentCount = documents.Count,
            ChunkCount = chunks.Count
        };
    }

    public async Task<IReadOnlyList<EvidenceMatch>> SearchAsync(
        string caseId,
        string executionId,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var candidates = await SearchCandidatesAsync(
            caseId,
            executionId,
            query,
            category: null,
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
        CancellationToken cancellationToken = default)
    {
        var candidates = await SearchCandidatesAsync(
            caseId,
            executionId,
            query,
            category,
            candidateSize: Math.Max(topK * 2, topK),
            cancellationToken);

        return await RerankEvidenceAsync(query, candidates, topK, cancellationToken);
    }

    private async Task<IReadOnlyList<EvidenceChunkDocument>> SearchCandidatesAsync(
        string caseId,
        string executionId,
        string query,
        string? category,
        int candidateSize,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = (await _embeddingService.EmbedAsync([query], cancellationToken)).Single();

        var filter = $"caseId eq '{EscapeFilterValue(caseId)}' and executionId eq '{EscapeFilterValue(executionId)}'";
        if (!string.IsNullOrWhiteSpace(category))
        {
            filter += $" and category eq '{EscapeFilterValue(category)}'";
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

        var response = await _searchClient.SearchAsync<EvidenceChunkDocument>(null, searchOptions, cancellationToken);
        var candidates = new List<EvidenceChunkDocument>();

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
        IReadOnlyList<EvidenceChunkDocument> candidates,
        int topK,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var reranked = await _rerankService.RerankAsync(
            query,
            candidates.Select(candidate => candidate.ChunkText).ToArray(),
            topK,
            cancellationToken);

        return reranked
            .Select(result => new EvidenceMatch
            {
                DocumentId = candidates[result.Index].DocumentId,
                DocumentType = candidates[result.Index].DocumentType,
                Category = candidates[result.Index].Category,
                Snippet = candidates[result.Index].ChunkText,
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

    public sealed class EvidenceChunkDocument
    {
        public required string Id { get; set; }

        public required string CaseId { get; set; }

        public required string ExecutionId { get; set; }

        public required string DocumentId { get; set; }

        public required string DocumentType { get; set; }

        public required string Category { get; set; }

        public required string ChunkText { get; set; }

        public required string SourcePath { get; set; }

        public IReadOnlyList<float> Embedding { get; set; } = [];
    }
}
