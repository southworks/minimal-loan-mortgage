using System.Text.Json;
using CohereLoanAndMortgage.AgentProvisioning.Models;
using Microsoft.Extensions.Configuration;

namespace CohereLoanAndMortgage.AgentProvisioning;

public static class SettingsLoader
{

    public static ProvisioningSettings LoadFromEnvironmentVars()
    {


        var settings = new ProvisioningSettings
        {
            ProjectEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT"),
            ModelDeploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME"),
            McpBaseUrl = Environment.GetEnvironmentVariable("MCP_BASE_URL")
        };

        Validate(settings);
        return settings;
    }


    private static void Validate(ProvisioningSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProjectEndpoint))
        {
            throw new InvalidOperationException(
                "ProjectEndpoint is required. Set it in config/provisioning.json or AZURE_FOUNDRY_PROJECT_ENDPOINT.");
        }

        if (string.IsNullOrWhiteSpace(settings.ModelDeploymentName))
        {
            throw new InvalidOperationException("ModelDeploymentName is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.McpBaseUrl))
        {
            throw new InvalidOperationException(
                "McpBaseUrl is required. Set MCP_BASE_URL to the public MCP host URL.");
        }

        if (!Uri.TryCreate(settings.ProjectEndpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"ProjectEndpoint '{settings.ProjectEndpoint}' is not a valid absolute URI.");
        }

        if (!Uri.TryCreate(settings.McpBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"McpBaseUrl '{settings.McpBaseUrl}' is not a valid absolute URI.");
        }
    }
}
