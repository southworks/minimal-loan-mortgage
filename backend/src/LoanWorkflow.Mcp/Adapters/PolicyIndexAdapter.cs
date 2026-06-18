using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class PolicyIndexAdapter
{
    public const string MetadataDocumentId = "policy-index-metadata";

    private readonly SearchClient _searchClient;
    private readonly CohereEmbeddingService _embeddingService;
    private readonly CohereRerankService _rerankService;
    private readonly AzureSearchOptions _options;

    public PolicyIndexAdapter(
        SearchIndexClient indexClient,
        CohereEmbeddingService embeddingService,
        CohereRerankService rerankService,
        IOptions<AzureSearchOptions> options)
    {
        _options = options.Value;
        _searchClient = indexClient.GetSearchClient(_options.PolicyIndexName);
        _embeddingService = embeddingService;
        _rerankService = rerankService;
    }

    public async Task<string?> GetStoredContentHashAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _searchClient.GetDocumentAsync<PolicySearchDocument>(
                MetadataDocumentId,
                cancellationToken: cancellationToken);

            return response.Value.ContentHash;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SeedPoliciesAsync(
        IReadOnlyList<PolicyEntry> policies,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<PolicySearchDocument>();

        foreach (var policy in policies)
        {
            documents.Add(new PolicySearchDocument
            {
                Id = policy.PolicyRef,
                PolicyRef = policy.PolicyRef,
                DocumentType = "policy",
                Rule = policy.Rule,
                Threshold = policy.Threshold,
                Action = policy.Action,
                Exception = policy.Exception,
                FullText = policy.FullText,
                ContentHash = string.Empty
            });
        }

        documents.Add(new PolicySearchDocument
        {
            Id = MetadataDocumentId,
            PolicyRef = "METADATA",
            DocumentType = "metadata",
            Rule = string.Empty,
            Threshold = string.Empty,
            Action = string.Empty,
            Exception = string.Empty,
            FullText = "Policy index metadata",
            ContentHash = contentHash
        });

        var embeddings = await _embeddingService.EmbedAsync(
            documents.Select(document => document.FullText).ToArray(),
            cancellationToken);

        for (var index = 0; index < documents.Count; index++)
        {
            documents[index].Embedding = embeddings[index];
        }

        var batch = IndexDocumentsBatch.Upload(documents);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task<GetRelevantPoliciesResponse> GetRelevantPoliciesAsync(
        string query,
        string? caseContext,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var effectiveQuery = string.IsNullOrWhiteSpace(caseContext)
            ? query
            : $"{query}\n\nCase context:\n{caseContext}";

        var queryEmbedding = (await _embeddingService.EmbedAsync([effectiveQuery], cancellationToken)).Single();

        var searchOptions = new SearchOptions
        {
            Size = Math.Max(topK * 3, topK),
            Filter = "documentType eq 'policy'",
            Select = { "policyRef", "rule", "threshold", "action", "exception", "fullText" }
        };

        searchOptions.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizedQuery(queryEmbedding)
                {
                    KNearestNeighborsCount = searchOptions.Size,
                    Fields = { "embedding" }
                }
            }
        };

        var response = await _searchClient.SearchAsync<PolicySearchDocument>(null, searchOptions, cancellationToken);
        var candidates = new List<PolicySearchDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            if (result.Document is not null)
            {
                candidates.Add(result.Document);
            }
        }

        if (candidates.Count == 0)
        {
            return new GetRelevantPoliciesResponse
            {
                Query = query,
                Policies = []
            };
        }

        var reranked = await _rerankService.RerankAsync(
            effectiveQuery,
            candidates.Select(candidate => candidate.FullText).ToArray(),
            topK,
            cancellationToken);

        var policies = reranked
            .Select(result => new PolicyMatch
            {
                PolicyRef = candidates[result.Index].PolicyRef,
                Rule = candidates[result.Index].Rule,
                Threshold = candidates[result.Index].Threshold,
                Action = candidates[result.Index].Action,
                Exception = candidates[result.Index].Exception,
                Score = result.Score
            })
            .ToArray();

        return new GetRelevantPoliciesResponse
        {
            Query = query,
            Policies = policies
        };
    }

    public sealed class PolicySearchDocument
    {
        public required string Id { get; set; }

        public required string PolicyRef { get; set; }

        public required string DocumentType { get; set; }

        public required string Rule { get; set; }

        public required string Threshold { get; set; }

        public required string Action { get; set; }

        public required string Exception { get; set; }

        public required string FullText { get; set; }

        public required string ContentHash { get; set; }

        public IReadOnlyList<float> Embedding { get; set; } = [];
    }
}
