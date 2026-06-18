namespace LoanWorkflow.Mcp.Options;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; set; } = string.Empty;

    public string EvidenceIndexName { get; set; } = "loan-case-evidence";

    public string PolicyIndexName { get; set; } = "loan-policy-knowledge";

    public int VectorDimensions { get; set; } = 1024;
}
