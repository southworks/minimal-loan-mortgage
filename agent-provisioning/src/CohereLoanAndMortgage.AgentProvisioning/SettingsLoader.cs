using System.Text.Json;
using CohereLoanAndMortgage.AgentProvisioning.Models;
using Microsoft.Extensions.Configuration;

namespace CohereLoanAndMortgage.AgentProvisioning;

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ProvisioningSettings Load(string? configPath = null)
    {
        string resolvedConfigPath = ResolveConfigPath(configPath);
        if (!File.Exists(resolvedConfigPath))
        {
            throw new InvalidOperationException(
                $"Provisioning configuration file was not found at '{resolvedConfigPath}'.");
        }

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile(resolvedConfigPath, optional: false)
            .AddEnvironmentVariables()
            .Build();

        ProvisioningSettings settings = configuration.Get<ProvisioningSettings>()
            ?? throw new InvalidOperationException(
                $"Provisioning configuration at '{resolvedConfigPath}' could not be parsed.");

        if (string.IsNullOrWhiteSpace(settings.ProjectEndpoint))
        {
            settings.ProjectEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(settings.FoundryProjectResourceId))
        {
            settings.FoundryProjectResourceId =
                Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_RESOURCE_ID") ?? string.Empty;
        }

        Validate(settings);
        return settings;
    }

    public static string ResolveConfigPath(string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            return Path.GetFullPath(configPath);
        }

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "config", "provisioning.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "provisioning.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "agent-provisioning", "config", "provisioning.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "config", "provisioning.json"))
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static void Validate(ProvisioningSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProjectEndpoint))
        {
            throw new InvalidOperationException(
                "ProjectEndpoint is required. Set it in config/provisioning.json or AZURE_FOUNDRY_PROJECT_ENDPOINT.");
        }

        if (string.IsNullOrWhiteSpace(settings.FoundryProjectResourceId))
        {
            throw new InvalidOperationException(
                "FoundryProjectResourceId is required. Set it in config/provisioning.json or AZURE_FOUNDRY_PROJECT_RESOURCE_ID.");
        }

        if (string.IsNullOrWhiteSpace(settings.ModelDeploymentName))
        {
            throw new InvalidOperationException("ModelDeploymentName is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.EmbeddingDeploymentName))
        {
            settings.EmbeddingDeploymentName =
                Environment.GetEnvironmentVariable("EMBEDDING_DEPLOYMENT_NAME") ?? "cohere-embed-v4";
        }

        if (string.IsNullOrWhiteSpace(settings.MemoryStoreName))
        {
            throw new InvalidOperationException("MemoryStoreName is required.");
        }

        if (!Uri.TryCreate(settings.ProjectEndpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"ProjectEndpoint '{settings.ProjectEndpoint}' is not a valid absolute URI.");
        }
    }
}
