using CohereLoanAndMortgage.Api.Host.Options;
using LoanWorkflow.Mcp.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public static class StartupConfigurationValidator
{
    public static void Validate(IConfiguration configuration)
    {
        var foundryOptions = new AzureFoundryOptions();
        configuration.GetSection(AzureFoundryOptions.SectionName).Bind(foundryOptions);

        string? foundryEndpoint = configuration["AZURE_FOUNDRY_PROJECT_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT")
            ?? foundryOptions.ProjectEndpoint;

        if (string.IsNullOrWhiteSpace(foundryEndpoint))
        {
            throw new InvalidOperationException(
                "Azure Foundry configuration is missing. Set AzureFoundry:ProjectEndpoint or AZURE_FOUNDRY_PROJECT_ENDPOINT.");
        }

        var datasetOptions = new DatasetOptions();
        configuration.GetSection(DatasetOptions.SectionName).Bind(datasetOptions);

        if (string.IsNullOrWhiteSpace(datasetOptions.RootPath))
        {
            throw new InvalidOperationException(
                "Dataset configuration is missing. Set Dataset:RootPath or Dataset__RootPath.");
        }
    }
}
