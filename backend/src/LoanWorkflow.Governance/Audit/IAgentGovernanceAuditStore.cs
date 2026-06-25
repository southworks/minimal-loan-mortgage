namespace LoanWorkflow.Governance.Audit;

public interface IAgentGovernanceAuditStore
{
    void Append(AgentGovernanceAuditRecord record);
}
