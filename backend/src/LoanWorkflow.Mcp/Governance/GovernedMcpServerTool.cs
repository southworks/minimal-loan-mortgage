using System.Text.Json;
using LoanWorkflow.Governance;
using LoanWorkflow.Governance.Mcp;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp.Governance;

public sealed class GovernedMcpServerTool : DelegatingMcpServerTool
{
    private readonly McpToolGovernanceCoordinator _coordinator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GovernedMcpServerTool(
        McpServerTool innerTool,
        McpToolGovernanceCoordinator coordinator,
        IHttpContextAccessor httpContextAccessor)
        : base(innerTool)
    {
        _coordinator = coordinator;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        GovernanceSettings settings = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IOptions<GovernanceSettings>>()?.Value
            ?? new GovernanceSettings();

        if (!settings.EnableMcpToolGovernance)
        {
            return await base.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        string? mcpPath = _httpContextAccessor.HttpContext?.Request.Path.Value;
        if (!AgentCatalog.TryResolveRoleFromMcpPath(mcpPath, out AgentRole role))
        {
            return CreateBlockedResult("Unable to resolve agent role from MCP route.");
        }

        string toolName = request.Params?.Name ?? ProtocolTool.Name;
        McpToolGovernanceDecision decision = _coordinator.EvaluateToolCall(
            role,
            toolName,
            request.Params?.Arguments);

        if (!decision.Allowed)
        {
            return CreateBlockedResult(decision.Reason);
        }

        return await base.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static CallToolResult CreateBlockedResult(string message) =>
        new()
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = message
                }
            ]
        };
}
