using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Microsoft.Agents.AI.Foundry.Hosting;

namespace CohereLoanAndMortgage.HostedAgents;

internal sealed class LocalSessionIsolationKeyProvider : HostedSessionIsolationKeyProvider
{
    public override ValueTask<HostedSessionContext?> GetKeysAsync(
        ResponseContext context,
        CreateResponse request,
        CancellationToken cancellationToken)
    {
        string userKey = !string.IsNullOrWhiteSpace(context?.Isolation?.UserIsolationKey)
            ? context.Isolation.UserIsolationKey
            : Environment.GetEnvironmentVariable("HOSTED_USER_ISOLATION_KEY") ?? "local-dev-user";

        string chatKey = !string.IsNullOrWhiteSpace(context?.Isolation?.ChatIsolationKey)
            ? context.Isolation.ChatIsolationKey
            : Environment.GetEnvironmentVariable("HOSTED_CHAT_ISOLATION_KEY") ?? "local-dev-chat";

        return new ValueTask<HostedSessionContext?>(new HostedSessionContext(userKey, chatKey));
    }
}
