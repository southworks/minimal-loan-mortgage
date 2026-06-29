namespace LoanWorkflow.Governance;

public static class AgentIdentityCatalog
{
    public const string PolicyBundleVersion = "v1";

    public static string ToMeshAgentId(AgentRole role) =>
        role switch
        {
            AgentRole.DocumentProcessing => "did:mesh:loan-document-processing",
            AgentRole.Underwriting => "did:mesh:loan-underwriting",
            AgentRole.ResponsibleAi => "did:mesh:loan-responsible-ai",
            AgentRole.LoanSetup => "did:mesh:loan-setup",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
}
