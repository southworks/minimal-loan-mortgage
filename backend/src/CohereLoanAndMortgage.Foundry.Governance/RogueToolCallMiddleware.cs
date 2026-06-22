using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using AgentGovernance.Policy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CohereLoanAndMortgage.Foundry.Governance;

public sealed class RogueToolCallMiddleware
{
    private readonly RoguePolicyConfig _config;
    private readonly ILogger<RogueToolCallMiddleware>? _logger;
    private readonly Queue<string> _recentToolCalls = new();
    private readonly object _sync = new();

    public RogueToolCallMiddleware(RoguePolicyConfig config, ILogger<RogueToolCallMiddleware>? logger = null)
    {
        _config = config;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        AIAgent innerAgent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        string toolName = context.Function.Name;

        bool shouldBlock = false;
        int riskyCount;

        lock (_sync)
        {
            _recentToolCalls.Enqueue(toolName);
            while (_recentToolCalls.Count > _config.WindowSize)
            {
                _recentToolCalls.Dequeue();
            }

            riskyCount = _recentToolCalls.Count(call =>
                string.Equals(call, _config.RiskyTool, StringComparison.Ordinal));

            shouldBlock = riskyCount >= _config.TriggerCount
                && string.Equals(toolName, _config.RiskyTool, StringComparison.Ordinal);
        }

        if (shouldBlock)
        {
            _logger?.LogWarning(
                "Rogue tool call blocked: {ToolName} reached {Count}/{TriggerCount} in window {WindowSize}.",
                toolName,
                riskyCount,
                _config.TriggerCount,
                _config.WindowSize);

            context.Terminate = true;
            return $"Governance blocked risky tool '{toolName}' after repeated calls in the sliding window.";
        }

        return await next(context, cancellationToken).ConfigureAwait(false);
    }
}
