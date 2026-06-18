using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class FoundryEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly AzureFoundryModelsOptions _options;
    private readonly DefaultAzureCredential _credential = new();

    public FoundryEmbeddingService(HttpClient httpClient, IOptions<AzureFoundryModelsOptions> options)
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

        ValidateEndpoint(_options.EmbedEndpoint, "EmbedEndpoint");

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUrl(_options.EmbedEndpoint, "v1/embed"));
        await ApplyAuthorizationAsync(request, cancellationToken);
        request.Content = JsonContent.Create(new EmbedRequest
        {
            Model = _options.EmbedModelName,
            Texts = texts,
            InputType = "search_document",
            EmbeddingTypes = ["float"],
            Dimensions = _options.EmbeddingDimensions
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Azure Foundry embed response was empty.");

        return payload.Embeddings.Select(vector => vector.ToArray()).ToArray();
    }

    private async Task ApplyAuthorizationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            return;
        }

        AccessToken token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://ai.azure.com/.default"]),
            cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static string BuildRequestUrl(string endpoint, string route)
    {
        return $"{endpoint.TrimEnd('/')}/{route}";
    }

    private static void ValidateEndpoint(string endpoint, string settingName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"AzureFoundryModels:{settingName} is required.");
        }
    }

    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("texts")]
        public required IReadOnlyList<string> Texts { get; init; }

        [JsonPropertyName("input_type")]
        public required string InputType { get; init; }

        [JsonPropertyName("embedding_types")]
        public required IReadOnlyList<string> EmbeddingTypes { get; init; }

        [JsonPropertyName("dimensions")]
        public required int Dimensions { get; init; }
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public required IReadOnlyList<IReadOnlyList<float>> Embeddings { get; init; }
    }
}

public sealed class FoundryRerankService
{
    private readonly HttpClient _httpClient;
    private readonly AzureFoundryModelsOptions _options;
    private readonly DefaultAzureCredential _credential = new();

    public FoundryRerankService(HttpClient httpClient, IOptions<AzureFoundryModelsOptions> options)
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

        ValidateEndpoint(_options.RerankEndpoint, "RerankEndpoint");

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUrl(_options.RerankEndpoint, "v1/rerank"));
        await ApplyAuthorizationAsync(request, cancellationToken);
        request.Content = JsonContent.Create(new RerankRequest
        {
            Model = _options.RerankModelName,
            Query = query,
            Documents = documents,
            TopN = topK
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RerankResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Azure Foundry rerank response was empty.");

        return payload.Results
            .Select(result => new RerankResult(result.Index, result.RelevanceScore))
            .ToArray();
    }

    private async Task ApplyAuthorizationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            return;
        }

        AccessToken token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://ai.azure.com/.default"]),
            cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static string BuildRequestUrl(string endpoint, string route)
    {
        return $"{endpoint.TrimEnd('/')}/{route}";
    }

    private static void ValidateEndpoint(string endpoint, string settingName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"AzureFoundryModels:{settingName} is required.");
        }
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
