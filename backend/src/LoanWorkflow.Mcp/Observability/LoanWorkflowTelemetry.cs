using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LoanWorkflow.Mcp.Observability;

public static class LoanWorkflowTelemetry
{
    public const string WorkflowActivitySourceName = "CohereLoanAndMortgage.Workflow";
    public const string AgentActivitySourceName = "CohereLoanAndMortgage.Agents";
    public const string McpActivitySourceName = "CohereLoanAndMortgage.Mcp";
    public const string MeterName = "CohereLoanAndMortgage.Observability";

    public static readonly string[] ActivitySourceNames =
    [
        WorkflowActivitySourceName,
        AgentActivitySourceName,
        McpActivitySourceName,
        "Microsoft.Agents",
        "Microsoft.Agents.AI",
        "Microsoft.Agents.AI.Workflows",
        "Microsoft.Agents.AI.Foundry"
    ];

    public static readonly ActivitySource WorkflowActivitySource = new(WorkflowActivitySourceName);
    public static readonly ActivitySource AgentActivitySource = new(AgentActivitySourceName);
    public static readonly ActivitySource McpActivitySource = new(McpActivitySourceName);

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> WorkflowStartedCounter =
        Meter.CreateCounter<long>("loan.workflow.started");

    public static readonly Counter<long> WorkflowCompletedCounter =
        Meter.CreateCounter<long>("loan.workflow.completed");

    public static readonly Counter<long> WorkflowFailedCounter =
        Meter.CreateCounter<long>("loan.workflow.failed");

    public static readonly Counter<long> WorkflowAwaitingHumanReviewCounter =
        Meter.CreateCounter<long>("loan.workflow.awaiting_human_review");

    public static readonly Histogram<double> WorkflowStageDurationMs =
        Meter.CreateHistogram<double>("loan.workflow.stage.duration.ms", unit: "ms");

    public static readonly Histogram<double> AgentDurationMs =
        Meter.CreateHistogram<double>("loan.workflow.agent.duration.ms", unit: "ms");

    public static readonly Histogram<double> McpToolDurationMs =
        Meter.CreateHistogram<double>("loan.mcp.tool.duration.ms", unit: "ms");

    public static Activity? StartWorkflowActivity(
        string name,
        string runId,
        string executionMode,
        string? caseId = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        Activity? activity = WorkflowActivitySource.StartActivity(name, kind);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("workflow.run_id", runId);
        activity.SetTag("foundry.run_id", runId);
        activity.SetTag("foundry.thread_id", runId);
        activity.SetTag("execution_mode", executionMode);

        if (!string.IsNullOrWhiteSpace(caseId))
        {
            activity.SetTag("case.id", caseId);
        }

        return activity;
    }

    public static Activity? StartAgentActivity(
        string name,
        string runId,
        string executionMode,
        string agentRole,
        string agentName,
        string? caseId = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        Activity? activity = AgentActivitySource.StartActivity(name, kind);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("workflow.run_id", runId);
        activity.SetTag("foundry.run_id", runId);
        activity.SetTag("foundry.thread_id", runId);
        activity.SetTag("execution_mode", executionMode);
        activity.SetTag("agent.role", agentRole);
        activity.SetTag("agent.name", agentName);

        if (!string.IsNullOrWhiteSpace(caseId))
        {
            activity.SetTag("case.id", caseId);
        }

        return activity;
    }

    public static Activity? StartMcpToolActivity(
        string operationName,
        string caseId,
        string executionId,
        string agentRole,
        string agentName,
        ActivityKind kind = ActivityKind.Server)
    {
        Activity? activity = McpActivitySource.StartActivity(operationName, kind);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("workflow.run_id", executionId);
        activity.SetTag("foundry.run_id", executionId);
        activity.SetTag("foundry.thread_id", executionId);
        activity.SetTag("case.id", caseId);
        activity.SetTag("agent.role", agentRole);
        activity.SetTag("agent.name", agentName);
        activity.SetTag("execution_mode", "hosted");

        return activity;
    }

    public static KeyValuePair<string, object?>[] BuildWorkflowTags(
        string runId,
        string caseId,
        string? stage = null,
        string? executionMode = null,
        string? agentRole = null,
        string? agentName = null)
    {
        var tags = new List<KeyValuePair<string, object?>>(8)
        {
            new("workflow.run_id", runId),
            new("foundry.run_id", runId),
            new("foundry.thread_id", runId),
            new("case.id", caseId)
        };

        if (!string.IsNullOrWhiteSpace(stage))
        {
            tags.Add(new("workflow.stage", stage));
        }

        if (!string.IsNullOrWhiteSpace(executionMode))
        {
            tags.Add(new("execution_mode", executionMode));
        }

        if (!string.IsNullOrWhiteSpace(agentRole))
        {
            tags.Add(new("agent.role", agentRole));
        }

        if (!string.IsNullOrWhiteSpace(agentName))
        {
            tags.Add(new("agent.name", agentName));
        }

        return tags.ToArray();
    }
}
