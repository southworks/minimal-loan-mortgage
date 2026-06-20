using System.Collections.Concurrent;
using System.Text;

namespace CohereLoanAndMortgage.Api.Host.Services;

public enum BasicWorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed class BasicWorkflowExecution
{
    public required string ExecutionId { get; init; }

    public required string CaseId { get; init; }

    public BasicWorkflowStatus Status { get; set; } = BasicWorkflowStatus.Pending;

    public string? CurrentAgent { get; set; }

    public Dictionary<string, AgentExecutionState> Agents { get; } = [];

    public Dictionary<string, string> AgentOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, StringBuilder> StreamingBuffers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? FailureReason { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AgentExecutionState
{
    public string AgentName { get; init; }

    public BasicWorkflowStatus Status { get; set; }

    public string? Output { get; set; }
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
