namespace CohereLoanAndMortgage.Api.Host.Governance;

public interface IAgentGovernanceAuditStore
{
    void Append(AgentGovernanceAuditRecord record);

    IReadOnlyList<AgentGovernanceAuditRecord> ReadAll();

    bool VerifyIntegrity();
}
