using System.Text.Json;
using LoanWorkflow.Governance;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Governance;

public sealed class McpAgentRoleMiddleware
{
    private readonly RequestDelegate _next;

    public McpAgentRoleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        GovernanceSettings settings = context.RequestServices
            .GetRequiredService<IOptions<GovernanceSettings>>()
            .Value;

        if (!settings.EnableMcpToolGovernance
            || !settings.RequireMcpAgentRoleHeader
            || !IsMcpRequest(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        string? agentRoleHeader = context.Request.Headers["X-Agent-Role"].FirstOrDefault();
        if (!AgentCatalog.TryResolveRole(agentRoleHeader, out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "A valid X-Agent-Role header is required for MCP access." }),
                    context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsMcpRequest(PathString path)
    {
        string value = path.Value ?? string.Empty;
        return value.Contains("/document-retrieval/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/underwriting-rules/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/policy-knowledge/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/loan-setup/", StringComparison.OrdinalIgnoreCase);
    }
}
