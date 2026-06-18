namespace LoanWorkflow.Mcp.Options;

public sealed class AzureFoundryModelsOptions
{
    public const string SectionName = "AzureFoundryModels";

    public string EmbedDeploymentName { get; set; } = "cohere-embed-v4";

    public string RerankDeploymentName { get; set; } = "cohere-rerank-v4-pro";

    public string EmbedModelName { get; set; } = "embed-v-4-0";

    public string RerankModelName { get; set; } = "Cohere-rerank-v4.0-pro";

    /// <summary>
    /// Hub deployment URL, e.g. https://{account}.services.ai.azure.com/openai/deployments/cohere-embed-v4
    /// </summary>
    public string EmbedEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Foundry account base URL, e.g. https://{account}.services.ai.azure.com
    /// </summary>
    public string RerankEndpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int EmbeddingDimensions { get; set; } = 1024;

    public int EmbeddingBatchSize { get; set; } = 16;

    public int MaxConcurrentEmbeddingRequests { get; set; } = 1;

    public int MaxConcurrentRerankRequests { get; set; } = 2;

    /// <summary>
    /// When true, retries transient Foundry HTTP failures including 429 throttling.
    /// </summary>
    public bool RetryEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts after the initial request.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 4;

    /// <summary>
    /// Base delay in seconds for exponential backoff when Retry-After is not present.
    /// </summary>
    public double BaseDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum delay cap in seconds for exponential backoff retries.
    /// </summary>
    public double MaxDelaySeconds { get; set; } = 30;
}
