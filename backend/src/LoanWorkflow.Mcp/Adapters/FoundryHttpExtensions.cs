namespace LoanWorkflow.Mcp.Adapters;

internal static class FoundryHttpExtensions
{
    public static async Task EnsureFoundrySuccessAsync(
        this HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : $" Response body: {Truncate(body, 1024)}";

        throw new HttpRequestException(
            $"Foundry {operation} request failed with {(int)response.StatusCode} ({response.ReasonPhrase}).{detail}");
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
