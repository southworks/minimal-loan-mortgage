namespace CohereLoanAndMortgage.Api.Host.Options;

public sealed class GovernanceOptions
{
    public const string SectionName = "Governance";

    public bool EnableFoundryAgentGovernance { get; set; } = true;

    public string AgentAuditStoreDirectory { get; set; } = "data/agent-governance-audit";

    public int RogueDetectionWindowSize { get; set; } = 10;

    public int RogueDetectionTriggerCount { get; set; } = 5;

    public bool LogFunctionInvocations { get; set; } = true;
}
