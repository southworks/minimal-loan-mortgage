using CohereLoanAndMortgage.Api.Host.Options;
using CohereLoanAndMortgage.Api.Host.Services;
using CohereLoanAndMortgage.Foundry.Governance;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Governance;

public sealed class FoundryGovernedAgentsFactory
{
    private readonly GovernanceOptions _options;
    private readonly FoundryAgentGovernanceFactory _governanceFactory;

    public FoundryGovernedAgentsFactory(
        IOptions<GovernanceOptions> options,
        FoundryAgentGovernanceFactory governanceFactory)
    {
        _options = options.Value;
        _governanceFactory = governanceFactory;
    }

    public FoundryAgents CreateGovernedAgents(FoundryAgents rawAgents)
    {
        if (!_options.EnableFoundryAgentGovernance)
        {
            return rawAgents;
        }

        return new FoundryAgents
        {
            DocumentProcessing = _governanceFactory.WrapAgent(
                rawAgents.DocumentProcessing,
                AgentRole.DocumentProcessing),
            Underwriting = _governanceFactory.WrapAgent(
                rawAgents.Underwriting,
                AgentRole.Underwriting),
            ResponsibleAi = _governanceFactory.WrapAgent(
                rawAgents.ResponsibleAi,
                AgentRole.ResponsibleAi),
            LoanSetup = _governanceFactory.WrapAgent(
                rawAgents.LoanSetup,
                AgentRole.LoanSetup)
        };
    }
}
