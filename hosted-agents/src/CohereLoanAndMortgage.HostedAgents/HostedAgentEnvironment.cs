namespace CohereLoanAndMortgage.HostedAgents;

public static class HostedAgentEnvironment
{
    public static string GetAgentCatalogName() =>
        ReadRequired(
            "HOSTED_AGENT_CATALOG_NAME",
            "AGENT_NAME");

    public static Uri GetProjectEndpoint()
    {
        string endpoint = ReadRequired(
            "FOUNDRY_PROJECT_ENDPOINT",
            "AZURE_AI_PROJECT_ENDPOINT",
            "AZURE_FOUNDRY_PROJECT_ENDPOINT");

        return new Uri(endpoint);
    }

    public static string GetModelDeploymentName() =>
        ReadRequired(
            "AZURE_AI_MODEL_DEPLOYMENT_NAME",
            "FOUNDRY_MODEL",
            "AZURE_FOUNDRY_MODEL_DEPLOYMENT_NAME");

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
