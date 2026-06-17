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
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' returned empty output. Expected JSON with summary, decision, evidence, and optional memoryUpdates.");
        }

        AgentStructuredOutput? structured = TryDeserialize(rawOutput.Trim())
            ?? TryDeserialize(ExtractJsonObject(rawOutput));

        if (structured is null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' did not return valid structured JSON. Expected properties: summary, decision, evidence, memoryUpdates.");
        }

        if (string.IsNullOrWhiteSpace(structured.Summary) ||
            string.IsNullOrWhiteSpace(structured.Decision) ||
            string.IsNullOrWhiteSpace(structured.Evidence))
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' returned JSON missing required fields (summary, decision, evidence).");
        }

        return new AgentStepResult
        {
            AgentName = agentName,
            Summary = structured.Summary,
            Decision = structured.Decision,
            Evidence = structured.Evidence,
            MemoryUpdates = structured.MemoryUpdates,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static AgentStructuredOutput? TryDeserialize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AgentStructuredOutput>(text, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
}
