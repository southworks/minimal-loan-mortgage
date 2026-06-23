using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.State;

public enum ScenarioOutcomeFilter
{
    All,
    Approve,
    Review,
    Deny
}

public static class ScenarioPickerFilter
{
    public static IReadOnlyList<ScenarioSummaryResponse> Apply(
        IReadOnlyList<ScenarioSummaryResponse> scenarios,
        string searchQuery,
        ScenarioOutcomeFilter outcomeFilter)
    {
        IEnumerable<ScenarioSummaryResponse> query = scenarios;

        if (outcomeFilter != ScenarioOutcomeFilter.All)
        {
            var normalizedOutcome = outcomeFilter.ToString().ToLowerInvariant();
            query = query.Where(s => NormalizeOutcome(s.ExpectedOutcome) == normalizedOutcome);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(s => MatchesSearch(s, searchQuery));
        }

        return query
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool MatchesSearch(ScenarioSummaryResponse scenario, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return true;
        }

        return Contains(scenario.Title, searchQuery)
            || Contains(scenario.Description, searchQuery)
            || Contains(scenario.DemoTagline, searchQuery)
            || Contains(scenario.ScenarioId, searchQuery);
    }

    public static string NormalizeOutcome(string? expectedOutcome)
    {
        if (string.IsNullOrWhiteSpace(expectedOutcome))
        {
            return string.Empty;
        }

        var normalized = expectedOutcome.Trim().ToLowerInvariant();
        return normalized switch
        {
            "approved" => "approve",
            _ => normalized
        };
    }

    public static string FormatOutcomeLabel(string? expectedOutcome)
    {
        var normalized = NormalizeOutcome(expectedOutcome);
        return normalized switch
        {
            "approve" => "Approve",
            "review" => "Review",
            "deny" => "Deny",
            _ => string.IsNullOrWhiteSpace(expectedOutcome) ? "Unknown" : expectedOutcome
        };
    }

    private static bool Contains(string? value, string searchQuery) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
}
