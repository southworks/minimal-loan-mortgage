using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class CohereEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly CohereOptions _options;

    public CohereEmbeddingService(HttpClient httpClient, IOptions<CohereOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Cohere API key is required for embedding generation.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/embed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new EmbedRequest
        {
            Model = _options.EmbedModel,
            Texts = texts,
            InputType = "search_document"
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Cohere embed response was empty.");

        return payload.Embeddings.Select(vector => vector.ToArray()).ToArray();
    }

    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("texts")]
        public required IReadOnlyList<string> Texts { get; init; }

        [JsonPropertyName("input_type")]
        public required string InputType { get; init; }
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public required IReadOnlyList<IReadOnlyList<float>> Embeddings { get; init; }
    }
}

public sealed class CohereRerankService
{
    private readonly HttpClient _httpClient;
    private readonly CohereOptions _options;

    public CohereRerankService(HttpClient httpClient, IOptions<CohereOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return documents
                .Select((document, index) => new RerankResult(index, Math.Max(0.1, 1.0 - (index * 0.05))))
                .Take(topK)
                .ToArray();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/rerank");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new RerankRequest
        {
            Model = _options.RerankModel,
            Query = query,
            Documents = documents,
            TopN = topK
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RerankResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Cohere rerank response was empty.");

        return payload.Results
            .Select(result => new RerankResult(result.Index, result.RelevanceScore))
            .ToArray();
    }

    public sealed record RerankResult(int Index, double Score);

    private sealed class RerankRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("query")]
        public required string Query { get; init; }

        [JsonPropertyName("documents")]
        public required IReadOnlyList<string> Documents { get; init; }

        [JsonPropertyName("top_n")]
        public required int TopN { get; init; }
    }

    private sealed class RerankResponse
    {
        [JsonPropertyName("results")]
        public required IReadOnlyList<RerankResponseItem> Results { get; init; }
    }

    private sealed class RerankResponseItem
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("relevance_score")]
        public double RelevanceScore { get; init; }
    }
}
