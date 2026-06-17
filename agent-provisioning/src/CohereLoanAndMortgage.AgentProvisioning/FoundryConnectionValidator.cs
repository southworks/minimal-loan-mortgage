using Azure;
using Azure.AI.Projects;
using CohereLoanAndMortgage.AgentProvisioning.Models;
using System.ClientModel;

namespace CohereLoanAndMortgage.AgentProvisioning;

public sealed class FoundryConnectionValidator
{
    public async Task<IReadOnlyDictionary<string, AIProjectConnection>> ResolveConnectionsAsync(
        AIProjectClient client,
        IEnumerable<AgentAssetBundle> bundles,
        CancellationToken cancellationToken)
    {
        HashSet<string> requiredConnectionNames = bundles
            .SelectMany(bundle => bundle.Mcp.Dependencies)
            .Where(dependency => dependency.Required)
            .Select(dependency => dependency.ConnectionName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> optionalConnectionNames = bundles
            .SelectMany(bundle => bundle.Mcp.Dependencies)
            .Where(dependency => !dependency.Required)
            .Select(dependency => dependency.ConnectionName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, AIProjectConnection> resolved = new(StringComparer.OrdinalIgnoreCase);

        foreach (string connectionName in requiredConnectionNames.Union(optionalConnectionNames))
        {
            try
            {
                ClientResult<AIProjectConnection> response = await client.Connections
                    .GetConnectionAsync(connectionName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                resolved[connectionName] = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                if (requiredConnectionNames.Contains(connectionName))
                {
                    throw new InvalidOperationException(
                        $"Required Foundry project connection '{connectionName}' was not found.",
                        ex);
                }
            }
        }

        return resolved;
    }
}
