using System.Text.Json;
using System.Text.Json.Serialization;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

/// <summary>
/// Structured output contract expected from each Foundry agent at step completion.
/// Agents must be provisioned externally to return JSON matching this shape.
/// </summary>
public sealed class AgentStructuredOutput
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("decision")]
    public required string Decision { get; init; }

    [JsonPropertyName("evidence")]
    public required string Evidence { get; init; }

    [JsonPropertyName("memoryUpdates")]
    public IReadOnlyList<string> MemoryUpdates { get; init; } = [];
}

public sealed class AgentStepResult
{
    public required string AgentName { get; init; }

    public required string Summary { get; init; }

    public required string Decision { get; init; }

    public required string Evidence { get; init; }

    public IReadOnlyList<string> MemoryUpdates { get; init; } = [];

    public required DateTimeOffset CompletedAtUtc { get; init; }
}

public static class AgentStructuredOutputParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentStepResult Parse(string agentName, string rawOutput)
    {
        AgentStepResult? structured = TryParse(agentName, rawOutput);
        if (structured is not null)
        {
            return structured;
        }

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' returned empty output. Expected JSON with summary, decision, evidence, and optional memoryUpdates.");
        }

        string trimmedOutput = rawOutput.Trim();
        if (trimmedOutput.Contains("Error (", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' returned an error instead of structured JSON: {Truncate(trimmedOutput)}");
        }

        throw new InvalidOperationException(
            $"Agent '{agentName}' did not return valid structured JSON. Expected properties: summary, decision, evidence, memoryUpdates.");
    }

    public static AgentStepResult? TryParse(string agentName, string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return null;
        }

        string trimmedOutput = rawOutput.Trim();
        if (trimmedOutput.Contains("Error (", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string normalizedJson = NormalizeJsonPayload(rawOutput);
        return TryParseFlexible(agentName, normalizedJson)
            ?? TryParseFlexible(agentName, ExtractJsonObject(rawOutput) ?? string.Empty);
    }

    private static AgentStepResult? TryParseFlexible(string agentName, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        AgentStructuredOutput? strict = TryDeserializeStrict(json);
        if (strict is not null &&
            !string.IsNullOrWhiteSpace(strict.Summary) &&
            !string.IsNullOrWhiteSpace(strict.Decision) &&
            !string.IsNullOrWhiteSpace(strict.Evidence))
        {
            return ToStepResult(agentName, strict.Summary, strict.Decision, strict.Evidence, strict.MemoryUpdates);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            JsonElement root = document.RootElement;
            string? summary = ReadRequiredString(root, "summary");
            string? decision = ReadRequiredString(root, "decision");
            string? evidence = ReadEvidence(root, "evidence");

            if (string.IsNullOrWhiteSpace(summary) ||
                string.IsNullOrWhiteSpace(decision) ||
                string.IsNullOrWhiteSpace(evidence))
            {
                return null;
            }

            IReadOnlyList<string> memoryUpdates = ReadMemoryUpdates(root, "memoryUpdates");
            return ToStepResult(agentName, summary, decision, evidence, memoryUpdates);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AgentStructuredOutput? TryDeserializeStrict(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentStructuredOutput>(text, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static string? ReadEvidence(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<string> ReadMemoryUpdates(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var updates = new List<string>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    updates.Add(text);
                }
            }
        }

        return updates;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static AgentStepResult ToStepResult(
        string agentName,
        string summary,
        string decision,
        string evidence,
        IReadOnlyList<string> memoryUpdates) =>
        new()
        {
            AgentName = agentName,
            Summary = summary,
            Decision = decision,
            Evidence = evidence,
            MemoryUpdates = memoryUpdates,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

    private static string NormalizeJsonPayload(string text)
    {
        string normalized = text.Trim();

        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = normalized.IndexOf('\n');
            if (firstLineBreak >= 0)
            {
                normalized = normalized[(firstLineBreak + 1)..];
            }

            if (normalized.EndsWith("```", StringComparison.Ordinal))
            {
                normalized = normalized[..^3].TrimEnd();
            }
        }

        return ExtractJsonObject(normalized) ?? normalized.Trim();
    }

    private static string? ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }

    private static string Truncate(string value)
    {
        const int maxLength = 500;
        return value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "...");
    }
}
