namespace CohereLoanAndMortgage.AgentProvisioning.Models;

public sealed class ProvisioningSettings
{
    public string ProjectEndpoint { get; set; } = string.Empty;

    public string FoundryProjectResourceId { get; set; } = string.Empty;

    public string ModelDeploymentName { get; set; } = "cohere-command-a";

    public string MemoryStoreName { get; set; } = "loan-mortgage-agent-memory";
}
