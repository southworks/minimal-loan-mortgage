namespace CohereLoanAndMortgage.Foundry.Governance;

public sealed class GovernanceSettings
{
    public const string SectionName = "Governance";

    public bool EnableFoundryAgentGovernance { get; set; } = true;

    public bool EnableMcpToolGovernance { get; set; } = true;

    public bool RequireMcpAgentRoleHeader { get; set; } = true;

    public string AgentAuditStoreDirectory { get; set; } = "data/agent-governance-audit";
}
