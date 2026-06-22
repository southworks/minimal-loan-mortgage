using Microsoft.Agents.AI;

namespace CohereLoanAndMortgage.Foundry.Governance;

public sealed class FoundryAgentGovernanceFactory
{
    private readonly FoundryAgentGovernanceBootstrap _bootstrap;

    public FoundryAgentGovernanceFactory(FoundryAgentGovernanceBootstrap bootstrap)
    {
        _bootstrap = bootstrap;
    }

    public AIAgent WrapAgent(AIAgent agent, AgentRole role) =>
        _bootstrap.WrapAgent(agent, role);
}
