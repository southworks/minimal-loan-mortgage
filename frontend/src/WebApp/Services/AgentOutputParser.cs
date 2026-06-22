using System.Text.Json;
using System.Text.Json.Serialization;
using Cohere.LoanProcessing.Shared.Contracts.Agents;
using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.Services;

internal static class AgentOutputParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DocumentProcessingResultDto? ParseDocumentProcessing(string? rawOutput)
    {
        AgentStructuredPayload? payload = TryParsePayload(rawOutput);
        if (payload is null)
        {
            return null;
        }

        bool isComplete = !ContainsReviewDecision(payload.Decision);
        decimal completeness = isComplete ? 1m : 0.75m;

        return new DocumentProcessingResultDto(
            IsComplete: isComplete,
            CompletenessScore: completeness,
            MissingDocuments: payload.Anomalies?.ToList() ?? [],
            Inconsistencies: payload.Concerns?.ToList() ?? [],
            Summary: payload.Summary,
            Status: isComplete ? "Completed" : "ReviewRequired",
            RequiresHumanReview: !isComplete,
            EvidenceItems: BuildEvidenceItems(payload),
            Flags: BuildFlags(payload));
    }

    public static UnderwritingResultDto? ParseUnderwriting(string? rawOutput)
    {
        AgentStructuredPayload? payload = TryParsePayload(rawOutput);
        if (payload is null)
        {
            return null;
        }

        var reasons = new List<string>();
        if (payload.KeyFacts is not null)
        {
            reasons.AddRange(payload.KeyFacts);
        }

        if (!string.IsNullOrWhiteSpace(payload.Evidence))
        {
            reasons.Add(payload.Evidence);
        }

        var anomalies = payload.Anomalies?.ToList() ?? [];
        string recommendation = MapRecommendation(payload.Decision);
        bool requiresReview = recommendation.Equals("Review", StringComparison.OrdinalIgnoreCase)
            || recommendation.Equals("Deny", StringComparison.OrdinalIgnoreCase);

        return new UnderwritingResultDto(
            Recommendation: recommendation,
            Reasons: reasons,
            Evidence: payload.PolicyRefs?.ToList() ?? SplitEvidence(payload.Evidence),
            RiskSignals: payload.Anomalies?.ToList() ?? [],
            Anomalies: anomalies,
            Summary: payload.Summary,
            Scores: BuildScores(payload),
            EvidenceItems: BuildEvidenceItems(payload),
            RationaleItems: BuildRationaleItems(payload),
            RequiresHumanReview: requiresReview,
            HasCriticalAnomaly: anomalies.Any(item =>
                item.Contains("critical", StringComparison.OrdinalIgnoreCase)));
    }

    public static ResponsibleAiResultDto? ParseResponsibleAi(string? rawOutput)
    {
        AgentStructuredPayload? payload = TryParsePayload(rawOutput);
        if (payload is null)
        {
            return null;
        }

        bool passed = !ContainsReviewDecision(payload.Decision) && !ContainsDenyDecision(payload.Decision);
        var observations = payload.SupportingFacts?.ToList() ?? [];
        var fairnessFlags = payload.Concerns?.ToList() ?? payload.Recommendations?.ToList() ?? [];

        return new ResponsibleAiResultDto(
            Passed: passed,
            FairnessFlags: fairnessFlags,
            Observations: observations,
            Summary: payload.Summary,
            EscalationRecommended: !passed,
            RequiresHumanReview: !passed,
            FlagItems: BuildFlags(payload));
    }

    public static LoanSetupResultDto? ParseLoanSetup(string? rawOutput)
    {
        AgentStructuredPayload? payload = TryParsePayload(rawOutput);
        if (payload is null)
        {
            return null;
        }

        bool failed = ContainsDenyDecision(payload.Decision) || ContainsFailureDecision(payload.Decision);
        string? accountId = ExtractAccountId(payload);

        return new LoanSetupResultDto(
            DemoAccountId: accountId,
            SetupSummary: payload.Summary,
            Status: failed ? "Failed" : "Completed",
            OperationId: payload.Decision,
            CompletedAt: DateTimeOffset.UtcNow,
            EvidenceItems: BuildEvidenceItems(payload));
    }

    private static AgentStructuredPayload? TryParsePayload(string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return null;
        }

        foreach (string candidate in CollectJsonCandidates(rawOutput))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(candidate);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                JsonElement root = document.RootElement;
                string? summary = ReadString(root, "summary");
                string? decision = ReadString(root, "decision");
                string? evidence = ReadEvidence(root, "evidence");

                if (string.IsNullOrWhiteSpace(summary)
                    && string.IsNullOrWhiteSpace(decision)
                    && string.IsNullOrWhiteSpace(evidence))
                {
                    continue;
                }

                return new AgentStructuredPayload
                {
                    Summary = summary ?? rawOutput.Trim(),
                    Decision = decision ?? "Review",
                    Evidence = evidence ?? string.Empty,
                    RiskLevel = ReadString(root, "riskLevel"),
                    PolicyRefs = ReadStringArray(root, "policyRefs"),
                    Anomalies = ReadStringArray(root, "anomalies"),
                    KeyFacts = ReadStringArray(root, "keyFacts"),
                    ApprovalAssessment = ReadString(root, "approvalAssessment"),
                    BiasRisk = ReadString(root, "biasRisk"),
                    SupportingFacts = ReadStringArray(root, "supportingFacts"),
                    Concerns = ReadStringArray(root, "concerns"),
                    Recommendations = ReadStringArray(root, "recommendations")
                };
            }
            catch (JsonException)
            {
            }
        }

        return new AgentStructuredPayload
        {
            Summary = rawOutput.Trim(),
            Decision = "Review",
            Evidence = rawOutput.Trim()
        };
    }

    private static UnderwritingScoresDto? BuildScores(AgentStructuredPayload payload)
    {
        decimal? riskScore = null;
        if (!string.IsNullOrWhiteSpace(payload.RiskLevel))
        {
            riskScore = payload.RiskLevel.Contains("high", StringComparison.OrdinalIgnoreCase) ? 0.82m
                : payload.RiskLevel.Contains("low", StringComparison.OrdinalIgnoreCase) ? 0.25m
                : 0.55m;
        }

        return riskScore is null ? null : new UnderwritingScoresDto(RiskScore: riskScore);
    }

    private static IReadOnlyList<EvidenceItem> BuildEvidenceItems(AgentStructuredPayload payload)
    {
        var items = new List<EvidenceItem>();
        if (!string.IsNullOrWhiteSpace(payload.Evidence))
        {
            items.Add(new EvidenceItem(EvidenceSourceType.AgentOutput, "agent-output", payload.Evidence, 0.8));
        }

        if (payload.PolicyRefs is not null)
        {
            items.AddRange(payload.PolicyRefs.Select(reference =>
                new EvidenceItem(EvidenceSourceType.Policy, reference, reference, 0.75)));
        }

        return items;
    }

    private static IReadOnlyList<RationaleItem> BuildRationaleItems(AgentStructuredPayload payload)
    {
        var items = new List<RationaleItem>();
        if (payload.KeyFacts is not null)
        {
            items.AddRange(payload.KeyFacts.Select((fact, index) =>
                new RationaleItem($"fact-{index + 1}", fact, FlagSeverity.Info)));
        }

        if (payload.Concerns is not null)
        {
            items.AddRange(payload.Concerns.Select((concern, index) =>
                new RationaleItem($"concern-{index + 1}", concern, FlagSeverity.Warning)));
        }

        return items;
    }

    private static IReadOnlyList<FlagItem> BuildFlags(AgentStructuredPayload payload)
    {
        var flags = new List<FlagItem>();
        if (payload.Anomalies is not null)
        {
            flags.AddRange(payload.Anomalies.Select((anomaly, index) =>
                new FlagItem($"anomaly-{index + 1}", "Anomaly", anomaly, FlagSeverity.High, true)));
        }

        if (payload.Concerns is not null)
        {
            flags.AddRange(payload.Concerns.Select((concern, index) =>
                new FlagItem($"concern-{index + 1}", "Concern", concern, FlagSeverity.Warning, true)));
        }

        return flags;
    }

    private static IReadOnlyList<string> SplitEvidence(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return [];
        }

        return evidence.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string MapRecommendation(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return "Review";
        }

        string normalized = decision.Trim();
        if (ContainsDenyDecision(normalized))
        {
            return "Deny";
        }

        if (ContainsReviewDecision(normalized))
        {
            return "Review";
        }

        if (normalized.Contains("approve", StringComparison.OrdinalIgnoreCase))
        {
            return "Approve";
        }

        return "Review";
    }

    private static bool ContainsReviewDecision(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Contains("review", StringComparison.OrdinalIgnoreCase)
            || value.Contains("manual", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsDenyDecision(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Contains("deny", StringComparison.OrdinalIgnoreCase)
            || value.Contains("reject", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsFailureDecision(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains("fail", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractAccountId(AgentStructuredPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Decision)
            && payload.Decision.Contains("ACCT", StringComparison.OrdinalIgnoreCase))
        {
            return payload.Decision;
        }

        if (!string.IsNullOrWhiteSpace(payload.Evidence)
            && payload.Evidence.Contains("ACCT", StringComparison.OrdinalIgnoreCase))
        {
            return payload.Evidence;
        }

        return ContainsFailureDecision(payload.Decision) ? null : $"DEMO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }

    private static IEnumerable<string> CollectJsonCandidates(string rawOutput)
    {
        var candidates = new List<string>();
        string trimmed = rawOutput.Trim();
        candidates.Add(trimmed);

        int start = trimmed.IndexOf('{');
        int end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            candidates.Add(trimmed[start..(end + 1)]);
        }

        return candidates.Distinct(StringComparer.Ordinal);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
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
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
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

    private sealed class AgentStructuredPayload
    {
        public string Summary { get; init; } = string.Empty;

        public string Decision { get; init; } = string.Empty;

        public string Evidence { get; init; } = string.Empty;

        public string? RiskLevel { get; init; }

        public IReadOnlyList<string>? PolicyRefs { get; init; }

        public IReadOnlyList<string>? Anomalies { get; init; }

        public IReadOnlyList<string>? KeyFacts { get; init; }

        public string? ApprovalAssessment { get; init; }

        public string? BiasRisk { get; init; }

        public IReadOnlyList<string>? SupportingFacts { get; init; }

        public IReadOnlyList<string>? Concerns { get; init; }

        public IReadOnlyList<string>? Recommendations { get; init; }
    }
}
