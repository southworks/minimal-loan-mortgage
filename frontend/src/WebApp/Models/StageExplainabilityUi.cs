using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.Models;

internal static class StageExplainabilityUi
{
    public static string DocumentProcessingStatusLabel(DocumentProcessingResultDto result) =>
        result.Status switch
        {
            "ReviewRequired" => "Review required",
            "Invalid" => "Invalid",
            "Incomplete" => "Incomplete",
            _ when result.IsComplete => "Completed",
            _ => "Needs review"
        };

    public static string DocumentProcessingStatusCssClass(DocumentProcessingResultDto result) =>
        result.Status switch
        {
            "Invalid" => "agent-status-danger",
            "ReviewRequired" or "Incomplete" => "agent-status-warning",
            _ when result.IsComplete => "agent-status-success",
            _ => "agent-status-warning"
        };

    public static string? DocumentProcessingPrimaryReason(DocumentProcessingResultDto result)
    {
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            return result.Summary;
        }

        if (result.MissingDocuments.Count > 0)
        {
            return $"Missing required documents: {string.Join(", ", result.MissingDocuments)}.";
        }

        if (result.Inconsistencies.Count > 0)
        {
            return result.Inconsistencies[0];
        }

        return result.IsComplete
            ? "Documentation verified and sufficient to continue."
            : "Documentation requires follow-up before continuing.";
    }

    public static IReadOnlyList<string> UnderwritingInputFindings(UnderwritingResultDto result)
    {
        if (result.KeyFacts?.Count > 0)
        {
            return result.KeyFacts;
        }

        return result.Reasons
            .Where(reason => reason.Contains("DTI:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("LTV:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("credit score:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Credit band:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Risk score:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Income stability:", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Cashflow stability:", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<string> UnderwritingDecisionFindings(UnderwritingResultDto result)
    {
        var inputs = UnderwritingInputFindings(result).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var drivers = result.Reasons.Where(reason => !inputs.Contains(reason)).ToList();

        if (drivers.Count == 0 && !string.IsNullOrWhiteSpace(result.EvidenceNarrative))
        {
            drivers.Add(result.EvidenceNarrative);
        }

        return drivers;
    }

    public static IReadOnlyList<string> UnderwritingPolicyReferences(UnderwritingResultDto result) =>
        result.PolicyRefs?.Count > 0 ? result.PolicyRefs : result.Evidence;
}
