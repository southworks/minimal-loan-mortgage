namespace CohereLoanAndMortgage.Foundry.Governance.Audit;

public interface IAgentGovernanceAuditStore
{
    void Append(AgentGovernanceAuditRecord record);
}
