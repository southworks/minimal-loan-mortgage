using Azure;
using Azure.AI.Projects;
using Azure.AI.Projects.Memory;
using Azure.Identity;
using CohereLoanAndMortgage.AgentProvisioning.Models;

namespace CohereLoanAndMortgage.AgentProvisioning;

public sealed class FoundryMemoryStoreProvisioner
{
    public async Task EnsureMemoryStoreAsync(
        ProvisioningSettings settings,
        CancellationToken cancellationToken)
    {
        AIProjectClient projectClient = new(new Uri(settings.ProjectEndpoint), new DefaultAzureCredential());

        try
        {
            _ = await projectClient.MemoryStores.GetMemoryStoreAsync(
                settings.MemoryStoreName,
                cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"Memory store '{settings.MemoryStoreName}' already exists.");
            return;
        }
        catch (Exception ex) when (IsMissingMemoryStore(ex))
        {
        }

        MemoryStoreDefaultDefinition definition = new(
            chatModel: settings.ModelDeploymentName,
            embeddingModel: settings.EmbeddingDeploymentName);

        MemoryStore memoryStore = await projectClient.MemoryStores.CreateMemoryStoreAsync(
            name: settings.MemoryStoreName,
            definition: definition,
            description: "Loan and mortgage demo agent memory store",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Created memory store '{memoryStore.Name}'.");
    }

    private static bool IsMissingMemoryStore(Exception exception)
    {
        if (exception is RequestFailedException { Status: 404 })
        {
            return true;
        }

        return exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("not_found", StringComparison.OrdinalIgnoreCase);
    }
}
