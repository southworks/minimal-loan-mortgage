namespace CohereLoanAndMortgage.Api.Host.Options;

public sealed class AzureFoundryOptions
{
    public const string SectionName = "AzureFoundry";

    public string ProjectEndpoint { get; set; } = string.Empty;

    public string DocumentProcessingAgentName { get; set; } = "document-processing-agent";

    public string UnderwritingAgentName { get; set; } = "underwriting-agent";

    public string ResponsibleAiAgentName { get; set; } = "responsible-ai-agent";

    public string LoanSetupAgentName { get; set; } = "loan-setup-agent";
}
