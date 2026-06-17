using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI.Workflows;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoanCaseRecord
{
    public required LoanCaseState State { get; set; }

    public CheckpointInfo? PendingCheckpoint { get; set; }

    public string? WorkflowSessionId { get; set; }
}

public sealed class InMemoryLoanCaseStore
{
    private readonly Dictionary<string, LoanCaseRecord> _cases = new(StringComparer.OrdinalIgnoreCase);

    public void Save(LoanCaseRecord record) => _cases[record.State.CaseId] = record;

    public LoanCaseRecord GetRequired(string caseId)
    {
        if (_cases.TryGetValue(caseId, out LoanCaseRecord? record))
        {
            return record;
        }

        throw new KeyNotFoundException($"Loan case '{caseId}' was not found.");
    }

    public bool TryGet(string caseId, out LoanCaseRecord? record) =>
        _cases.TryGetValue(caseId, out record);
}
