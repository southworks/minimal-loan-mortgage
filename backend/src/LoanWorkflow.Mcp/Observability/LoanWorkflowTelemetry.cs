using System.Diagnostics;

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

}
