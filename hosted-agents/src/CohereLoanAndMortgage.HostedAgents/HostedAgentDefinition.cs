namespace CohereLoanAndMortgage.HostedAgents;

public sealed record HostedAgentDefinition(
    string Name,
    string Description,
    string Instructions,
    string ToolboxName);
