namespace CohereLoanAndMortgage.HostedAgents;

public static class HostedAgentEnvironment
{
    public static string GetAgentCatalogName() =>
        ReadRequired(
            "FOUNDRY_AGENT_NAME",
            "HOSTED_AGENT_CATALOG_NAME",
            "AGENT_NAME");

    public static Uri GetModelInferenceEndpoint()
    {
        string endpoint = ReadRequired(
            "FOUNDRY_PROJECT_ENDPOINT",
            "AZURE_AI_PROJECT_ENDPOINT",
            "AZURE_FOUNDRY_PROJECT_ENDPOINT");

        endpoint = endpoint.TrimEnd('/');

        // The Agent Framework expects the Foundry project endpoint. Hosted sandboxes can inject
        // the account root URL, so rebuild the project URL from the platform project metadata.
        if (endpoint.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(endpoint);
        }

        string? projectName = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_NAME");
        if (string.IsNullOrWhiteSpace(projectName))
        {
            string? projectArmId = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ARM_ID");
            if (!string.IsNullOrWhiteSpace(projectArmId))
            {
                projectName = projectArmId.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1];
            }
        }

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            return new Uri($"{endpoint}/api/projects/{projectName}");
        }

        throw new InvalidOperationException(
            "Could not resolve the Foundry project URL. Use a full project endpoint or set FOUNDRY_PROJECT_ARM_ID / FOUNDRY_PROJECT_NAME.");
    }

    public static string GetModelDeploymentName()
    {
        string? configuredModel = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME");
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel;
        }

        string? azureModel = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME");
        if (!string.IsNullOrWhiteSpace(azureModel))
        {
            return azureModel;
        }

        // Foundry injects AZURE_AI_MODEL_DEPLOYMENT_NAME with a platform default (often gpt-4o)
        // that does not exist in this project. Require an explicit model env var in hosted sandboxes.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")))
        {
            throw new InvalidOperationException(
                "MODEL_DEPLOYMENT_NAME or AZURE_AI_MODEL_DEPLOYMENT_NAME must be set on the hosted agent version.");
        }

        return ReadRequired(
            "FOUNDRY_MODEL",
            "AZURE_FOUNDRY_MODEL_DEPLOYMENT_NAME");
    }

    private static string ReadRequired(params string[] names)
    {
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Set one of these environment variables: {string.Join(", ", names)}.");
    }
}
