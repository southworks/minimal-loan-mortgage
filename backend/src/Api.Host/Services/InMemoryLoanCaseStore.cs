using System.Collections.Concurrent;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI.Workflows;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoanCaseRecord
{
    public required LoanCaseState State { get; set; }

    public CheckpointInfo? PendingCheckpoint { get; set; }

    public CheckpointManager? WorkflowCheckpointManager { get; set; }

    public ExternalRequest? PendingExternalRequest { get; set; }

    public string? WorkflowSessionId { get; set; }
}

public sealed class InMemoryLoanCaseStore
{
    private readonly ConcurrentDictionary<string, LoanCaseRecord> _executions = new(StringComparer.OrdinalIgnoreCase);

    public void Save(LoanCaseRecord record) => _executions[record.State.ExecutionId] = record;

    public LoanCaseRecord GetRequired(string executionId)
    {
        if (_executions.TryGetValue(executionId, out LoanCaseRecord? record))
        {
            return record;
        }

        throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");
    }

    public bool TryGet(string executionId, out LoanCaseRecord? record) =>
        _executions.TryGetValue(executionId, out record);
}
