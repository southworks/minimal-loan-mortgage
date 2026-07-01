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
    }

    public static T Execute<T>(
        string operationName,
        string caseId,
        string executionId,
        string agentRole,
        string agentName,
        Func<T> action)
    {
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
    }
}
