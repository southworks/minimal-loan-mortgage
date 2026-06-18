using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace CohereLoanAndMortgage.AgentProvisioning;

public sealed class FoundryMcpConnectionUpdater
{
    private static readonly string[] ConnectionNames =
    [
        "document-retrieval-mcp",
        "underwriting-rules-mcp",
        "policy-knowledge-mcp",
        "loan-setup-mcp"
    ];

    private static readonly Dictionary<string, string> ConnectionPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["document-retrieval-mcp"] = "/document-retrieval/mcp",
        ["underwriting-rules-mcp"] = "/underwriting-rules/mcp",
        ["policy-knowledge-mcp"] = "/policy-knowledge/mcp",
        ["loan-setup-mcp"] = "/loan-setup/mcp"
    };

    private readonly DefaultAzureCredential _credential = new();

    public async Task UpdateConnectionsAsync(
        string projectResourceId,
        string mcpBaseUrl,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectResourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpBaseUrl);

        string normalizedBaseUrl = mcpBaseUrl.TrimEnd('/');
        using HttpClient httpClient = new();
        AccessToken token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]),
            cancellationToken);

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);

        foreach (string connectionName in ConnectionNames)
        {
            string targetUrl = $"{normalizedBaseUrl}{ConnectionPaths[connectionName]}";
            string requestUrl =
                $"https://management.azure.com{projectResourceId}/connections/{connectionName}?api-version=2025-06-01";

            var body = new
            {
                properties = new
                {
                    category = "GenericHttp",
                    authType = "None",
                    target = targetUrl
                }
            };

            using HttpRequestMessage request = new(HttpMethod.Put, requestUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Failed to update Foundry MCP connection '{connectionName}'. Status: {(int)response.StatusCode}. {errorBody}");
            }

            Console.WriteLine($"Updated MCP connection '{connectionName}' -> {targetUrl}");
        }
    }
}
