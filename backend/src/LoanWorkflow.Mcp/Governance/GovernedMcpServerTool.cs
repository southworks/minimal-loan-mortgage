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

        string? agentRoleHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Agent-Role"].FirstOrDefault();
        if (!AgentCatalog.TryResolveRole(agentRoleHeader, out AgentRole role))
        {
            return CreateBlockedResult("A valid X-Agent-Role header is required for MCP access.");
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
