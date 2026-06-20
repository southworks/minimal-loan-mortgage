using System.Collections.Concurrent;
using System.Text;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI.Workflows;

namespace CohereLoanAndMortgage.Api.Host.Services;

public enum BasicWorkflowStatus
{
    Pending,
    Running,
    WaitingForHuman,
    Completed,
    Rejected,
    Failed
}

public sealed class BasicWorkflowExecution
{
    public required string ExecutionId { get; init; }

    public required string CaseId { get; init; }

    public BasicWorkflowStatus Status { get; set; } = BasicWorkflowStatus.Pending;

    public Dictionary<string, string> AgentOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, StringBuilder> StreamingBuffers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PendingApprovalInfo? PendingApproval { get; set; }

    public CheckpointInfo? PendingCheckpoint { get; set; }

    public CheckpointInfo? HaltCheckpoint { get; set; }

    public CheckpointManager? WorkflowCheckpointManager { get; set; }

    public ExternalRequest? PendingExternalRequest { get; set; }

    public bool UnderwritingDecisionSubmitted { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InMemoryBasicWorkflowStore
{
    private readonly ConcurrentDictionary<string, BasicWorkflowExecution> _executions = new(StringComparer.OrdinalIgnoreCase);

    public void Save(BasicWorkflowExecution execution) => _executions[execution.ExecutionId] = execution;

    public BasicWorkflowExecution GetRequired(string executionId)
    {
        if (_executions.TryGetValue(executionId, out BasicWorkflowExecution? execution))
        {
            return execution;
        }

        throw new KeyNotFoundException($"Basic workflow execution '{executionId}' was not found.");
    }
}
