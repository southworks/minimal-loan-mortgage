using Azure.Core;
using Azure.Identity;

namespace CohereLoanAndMortgage.AgentProvisioning;

internal static class AzureCredentialFactory
{
    /// <summary>
    /// Uses managed identity in Azure (Container Apps Job) and developer credentials locally.
    /// Skips managed identity when not running in an Azure host to avoid failures from a
    /// disconnected Azure Arc / Connected Machine Agent on developer workstations.
    /// </summary>
    public static TokenCredential Create()
    {
        DefaultAzureCredentialOptions options = new();

        if (!IsRunningInAzure())
        {
            options.ExcludeManagedIdentityCredential = true;
            options.ExcludeWorkloadIdentityCredential = true;
        }

        return new DefaultAzureCredential(options);
    }

    private static bool IsRunningInAzure()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MSI_ENDPOINT"));
    }
}
