using System.Diagnostics;

namespace LoanWorkflow.Mcp.Observability;

public static class McpToolInstrumentation
{
    public static async Task<T> ExecuteAsync<T>(
        string operationName,
        string caseId,
        string executionId,
        string agentRole,
        string agentName,
        Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        using Activity? activity = LoanWorkflowTelemetry.StartMcpToolActivity(
            operationName,
            caseId,
            executionId,
            agentRole,
            agentName);

        try
        {
            T result = await action().ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LoanWorkflowTelemetry.McpToolDurationMs.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                LoanWorkflowTelemetry.BuildWorkflowTags(
                    executionId,
                    caseId,
                    executionMode: "hosted",
                    agentRole: agentRole,
                    agentName: agentName));
        }
    }

    public static T Execute<T>(
        string operationName,
        string caseId,
        string executionId,
        string agentRole,
        string agentName,
        Func<T> action)
    {
        var stopwatch = Stopwatch.StartNew();
        using Activity? activity = LoanWorkflowTelemetry.StartMcpToolActivity(
            operationName,
            caseId,
            executionId,
            agentRole,
            agentName);

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LoanWorkflowTelemetry.McpToolDurationMs.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                LoanWorkflowTelemetry.BuildWorkflowTags(
                    executionId,
                    caseId,
                    executionMode: "hosted",
                    agentRole: agentRole,
                    agentName: agentName));
        }
    }
}
