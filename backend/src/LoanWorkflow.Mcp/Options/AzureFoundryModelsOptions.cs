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
}
