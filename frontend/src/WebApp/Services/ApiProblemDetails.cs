using System.Net.Http.Json;
using System.Text.Json;

namespace Cohere.LoanProcessing.WebApp.Services;

public sealed record ApiProblemDetails(
    string? Title,
    string? Detail,
    string? TraceId,
    string? CaseId,
    string? ErrorCode)
{
    public string DisplayMessage
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Detail))
            {
                parts.Add(Detail);
            }
            else if (!string.IsNullOrWhiteSpace(Title))
            {
                parts.Add(Title);
            }

            if (!string.IsNullOrWhiteSpace(TraceId))
            {
                parts.Add($"Trace ID: {TraceId}");
            }

            if (!string.IsNullOrWhiteSpace(CaseId))
            {
                parts.Add($"Case ID: {CaseId}");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "The request failed.";
        }
    }

    public static async Task<ApiProblemDetails?> TryReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsProblemResponse(response))
        {
            return null;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;

            return new ApiProblemDetails(
                Title: GetString(root, "title"),
                Detail: GetString(root, "detail"),
                TraceId: GetExtensionString(root, "traceId"),
                CaseId: GetExtensionString(root, "caseId"),
                ErrorCode: GetExtensionString(root, "errorCode"));
        }
        catch
        {
            return null;
        }
    }

    public static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await TryReadAsync(response, cancellationToken);
        throw new ApiClientException(
            problem?.DisplayMessage ?? $"Request failed with status {(int)response.StatusCode}.",
            problem);
    }

    private static bool IsProblemResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType is "application/problem+json" or "application/json";
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetExtensionString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        if (root.TryGetProperty("extensions", out var extensions)
            && extensions.TryGetProperty(propertyName, out var nested)
            && nested.ValueKind == JsonValueKind.String)
        {
            return nested.GetString();
        }

        return null;
    }
}

public sealed class ApiClientException(string message, ApiProblemDetails? problem = null) : Exception(message)
{
    public ApiProblemDetails? Problem { get; } = problem;
}
