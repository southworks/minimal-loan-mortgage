using Azure;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using CohereLoanAndMortgage.AgentProvisioning.Models;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace CohereLoanAndMortgage.AgentProvisioning;

public sealed class FoundryAgentProvisioner
{
    private readonly AgentDefinitionBuilder _definitionBuilder = new();
    private readonly FoundryConnectionValidator _connectionValidator = new();

    public async Task<IReadOnlyList<AgentProvisionResult>> ProvisionAllAsync(
        ProvisioningSettings settings,
        IReadOnlyList<AgentAssetBundle> bundles,
        CancellationToken cancellationToken)
    {
        AIProjectClient projectClient = new(new Uri(settings.ProjectEndpoint), new DefaultAzureCredential());
        AgentAdministrationClient agentClient = new(new Uri(settings.ProjectEndpoint), new DefaultAzureCredential());

        IReadOnlyDictionary<string, AIProjectConnection> connections =
            await _connectionValidator.ResolveConnectionsAsync(projectClient, bundles, cancellationToken)
                .ConfigureAwait(false);

        List<AgentProvisionResult> results = [];
        foreach (AgentAssetBundle bundle in bundles.OrderBy(item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(await ProvisionAgentAsync(agentClient, settings, bundle, connections, cancellationToken)
                .ConfigureAwait(false));
        }

        if (results.Any(result => result.Outcome == ProvisionOutcome.Failed))
        {
            string details = string.Join("; ", results
                .Where(result => result.Outcome == ProvisionOutcome.Failed)
                .Select(result => $"{result.AgentName}: {result.Message}"));
            throw new InvalidOperationException($"One or more agents failed to provision. {details}");
        }

        return results;
    }

    private async Task<AgentProvisionResult> ProvisionAgentAsync(
        AgentAdministrationClient agentClient,
        ProvisioningSettings settings,
        AgentAssetBundle bundle,
        IReadOnlyDictionary<string, AIProjectConnection> connections,
        CancellationToken cancellationToken)
    {
        string agentName = bundle.Manifest.Name;

        try
        {
            string definitionJson = _definitionBuilder.BuildDefinitionJson(bundle, settings, connections);
            string desiredFingerprint = _definitionBuilder.ComputeFingerprint(definitionJson);

            ProjectsAgentVersion? existingVersion =
                await TryGetLatestAgentVersionAsync(agentClient, agentName, cancellationToken).ConfigureAwait(false);

            if (existingVersion is not null)
            {
                string existingDefinitionJson = JsonSerializer.Serialize(existingVersion.Definition);
                string existingFingerprint = _definitionBuilder.ComputeFingerprint(existingDefinitionJson);
                if (string.Equals(existingFingerprint, desiredFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    return new AgentProvisionResult
                    {
                        AgentName = agentName,
                        Outcome = ProvisionOutcome.Unchanged,
                        Message = $"Agent '{agentName}' version {existingVersion.Version} is already up to date."
                    };
                }
            }

            string requestJson = _definitionBuilder.BuildCreateVersionRequestJson(definitionJson);
            await agentClient.CreateAgentVersionAsync(
                agentName,
                BinaryContent.Create(BinaryData.FromString(requestJson)),
                foundryFeatures: null,
                options: new RequestOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);

            ProvisionOutcome outcome = existingVersion is null ? ProvisionOutcome.Created : ProvisionOutcome.Updated;
            return new AgentProvisionResult
            {
                AgentName = agentName,
                Outcome = outcome,
                Message = $"Agent '{agentName}' was {(outcome == ProvisionOutcome.Created ? "created" : "updated")}."
            };
        }
        catch (Exception ex)
        {
            return new AgentProvisionResult
            {
                AgentName = agentName,
                Outcome = ProvisionOutcome.Failed,
                Message = ex.Message
            };
        }
    }

    private static async Task<ProjectsAgentVersion?> TryGetLatestAgentVersionAsync(
        AgentAdministrationClient agentClient,
        string agentName,
        CancellationToken cancellationToken)
    {
        try
        {
            ClientResult<ProjectsAgentVersion> response = await agentClient
                .GetAgentVersionAsync(agentName, "latest", cancellationToken)
                .ConfigureAwait(false);

            return response.Value;
        }
        catch (Exception ex) when (IsMissingAgentVersion(ex))
        {
            return null;
        }
    }

    private static bool IsMissingAgentVersion(Exception exception)
    {
        if (exception is RequestFailedException { Status: 404 })
        {
            return true;
        }

        // Foundry returns HTTP 400 for agentVersion{latest} when the agent has no versions yet.
        return exception.Message.Contains("agentVersion{latest}", StringComparison.OrdinalIgnoreCase);
    }
}
