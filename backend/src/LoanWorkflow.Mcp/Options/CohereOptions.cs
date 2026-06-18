namespace LoanWorkflow.Mcp.Options;

public sealed class CohereOptions
{
    public const string SectionName = "Cohere";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.cohere.ai";

    public string EmbedModel { get; set; } = "embed-english-v3.0";

    public string RerankModel { get; set; } = "rerank-english-v3.0";
}
