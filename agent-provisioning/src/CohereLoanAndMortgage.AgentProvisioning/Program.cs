using CohereLoanAndMortgage.AgentProvisioning.Models;

namespace CohereLoanAndMortgage.AgentProvisioning;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            string? configPath = null;
            string? agentsRoot = null;

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--config" when index + 1 < args.Length:
                        configPath = args[++index];
                        break;
                    case "--agents" when index + 1 < args.Length:
                        agentsRoot = args[++index];
                        break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[index]}");
                        PrintUsage();
                        return 1;
                }
            }

            ProvisioningSettings settings = SettingsLoader.Load(configPath);
            AgentAssetLoader assetLoader = new(AgentAssetLoader.ResolveAgentsRoot(agentsRoot));
            IReadOnlyList<AgentAssetBundle> bundles = assetLoader.LoadAll();

            string? mcpBaseUrl = Environment.GetEnvironmentVariable("MCP_BASE_URL");
            if (!string.IsNullOrWhiteSpace(mcpBaseUrl))
            {
                Console.WriteLine($"Updating Foundry MCP connection targets to '{mcpBaseUrl.TrimEnd('/')}'...");
                FoundryMcpConnectionUpdater connectionUpdater = new();
                await connectionUpdater.UpdateConnectionsAsync(
                    settings.FoundryProjectResourceId,
                    mcpBaseUrl,
                    CancellationToken.None).ConfigureAwait(false);
            }

            Console.WriteLine($"Provisioning {bundles.Count} agents to {settings.ProjectEndpoint}");
            Console.WriteLine($"Model deployment: {settings.ModelDeploymentName}");

            if (bundles.Any(bundle => bundle.MemoryPolicy.Enabled))
            {
                FoundryMemoryStoreProvisioner memoryStoreProvisioner = new();
                await memoryStoreProvisioner.EnsureMemoryStoreAsync(settings, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine("Agent memory is disabled; skipping memory store provisioning.");
            }

            FoundryAgentProvisioner provisioner = new();
            IReadOnlyList<AgentProvisionResult> results =
                await provisioner.ProvisionAllAsync(settings, bundles, CancellationToken.None).ConfigureAwait(false);

            foreach (AgentProvisionResult result in results)
            {
                Console.WriteLine($"{result.Outcome,-10} {result.AgentName} - {result.Message}");
            }

            Console.WriteLine("Agent provisioning completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Agent provisioning failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Cohere Loan and Mortgage Agent Provisioning");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning");
        Console.WriteLine("    [--config path/to/provisioning.json]");
        Console.WriteLine("    [--agents path/to/agents]");
        Console.WriteLine();
        Console.WriteLine("Environment variables:");
        Console.WriteLine("  AZURE_FOUNDRY_PROJECT_ENDPOINT");
        Console.WriteLine("  AZURE_FOUNDRY_PROJECT_RESOURCE_ID");
        Console.WriteLine("  MCP_BASE_URL");
    }
}
