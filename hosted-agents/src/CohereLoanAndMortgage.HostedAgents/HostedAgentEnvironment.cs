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

        // Model deployments are account-scoped. Hosted sandboxes inject the account root URL,
        // while local dev often uses a project URL — strip back to the account host in that case.
        if (endpoint.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(endpoint);
            return new Uri($"{builder.Scheme}://{builder.Host}");
        }

        return new Uri(endpoint);
    }

    public static string GetModelDeploymentName()
    {
        string? configuredModel = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME");
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel;
        }

        // Foundry injects AZURE_AI_MODEL_DEPLOYMENT_NAME with a platform default (often gpt-4o)
        // that does not exist in this project. Require the custom env var in hosted sandboxes.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")))
        {
            throw new InvalidOperationException(
                "MODEL_DEPLOYMENT_NAME must be set on the hosted agent version.");
        }

        return ReadRequired(
            "AZURE_AI_MODEL_DEPLOYMENT_NAME",
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
