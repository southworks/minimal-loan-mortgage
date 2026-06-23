using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.State;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class ScenarioPickerFilterTests
{
    private static readonly IReadOnlyList<ScenarioSummaryResponse> SixScenarios =
    [
        new("happy_path_approved", "Happy Path", "Straightforward approval.", "approve", "Straight-through approval journey"),
        new("approved_with_conditions", "Approved With Conditions", "Nuanced recommendation.", "review", "Nuanced recommendation with supporting evidence"),
        new("missing_documentation", "Missing Documentation", "Document gaps.", "review", "Document evidence and gap handling"),
        new("high_risk_denied", "High Risk Denied", "Elevated risk signals.", "deny", "Risk controls and denial rationale"),
        new("fairness_review_needed", "Fairness Review", "Fairness checkpoint.", "review", "Governance and fairness checkpoint"),
        new("manual_review_borderline", "Manual Review", "Borderline profile.", "review", "Mandatory human decision checkpoint")
    ];

    [Fact]
    public void Apply_WithNoFilters_ReturnsAllScenariosOrderedByTitle()
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, string.Empty, ScenarioOutcomeFilter.All);

        Assert.Equal(6, result.Count);
        Assert.Equal("Approved With Conditions", result[0].Title);
        Assert.Equal("Fairness Review", result[1].Title);
    }

    [Fact]
    public void Apply_WithApproveFilter_ReturnsOnlyApproveScenarios()
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, string.Empty, ScenarioOutcomeFilter.Approve);

        Assert.Single(result);
        Assert.Equal("happy_path_approved", result[0].ScenarioId);
    }

    [Fact]
    public void Apply_WithDenyFilter_ReturnsOnlyDenyScenarios()
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, string.Empty, ScenarioOutcomeFilter.Deny);

        Assert.Single(result);
        Assert.Equal("high_risk_denied", result[0].ScenarioId);
    }

    [Fact]
    public void Apply_WithReviewFilter_ReturnsReviewScenarios()
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, string.Empty, ScenarioOutcomeFilter.Review);

        Assert.Equal(4, result.Count);
        Assert.All(result, s => Assert.Equal("review", ScenarioPickerFilter.NormalizeOutcome(s.ExpectedOutcome)));
    }

    [Theory]
    [InlineData("fairness", "fairness_review_needed")]
    [InlineData("missing", "missing_documentation")]
    [InlineData("happy_path", "happy_path_approved")]
    public void Apply_WithSearch_FiltersByTitleTaglineOrScenarioId(string query, string expectedScenarioId)
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, query, ScenarioOutcomeFilter.All);

        Assert.Single(result);
        Assert.Equal(expectedScenarioId, result[0].ScenarioId);
    }

    [Fact]
    public void Apply_WithSearchAndOutcomeFilter_AppliesBoth()
    {
        var result = ScenarioPickerFilter.Apply(SixScenarios, "human", ScenarioOutcomeFilter.Review);

        Assert.Single(result);
        Assert.Equal("manual_review_borderline", result[0].ScenarioId);
    }

    [Fact]
    public void Apply_WithTwentyScenarios_ScalesWithoutLosingMatches()
    {
        var scenarios = BuildTwentyScenarios();

        var result = ScenarioPickerFilter.Apply(scenarios, "case-", ScenarioOutcomeFilter.All);

        Assert.Equal(20, result.Count);
        Assert.Equal("Demo Case 01", result[0].Title);
        Assert.Equal("Demo Case 20", result[19].Title);
    }

    [Fact]
    public void Apply_WithTwentyScenarios_AndNoMatches_ReturnsEmpty()
    {
        var scenarios = BuildTwentyScenarios();

        var result = ScenarioPickerFilter.Apply(scenarios, "nonexistent-query", ScenarioOutcomeFilter.All);

        Assert.Empty(result);
    }

    [Fact]
    public void ScenarioPickerState_SetSearch_SelectsFirstFilteredResult()
    {
        var state = new ScenarioPickerState();
        state.SetScenarios(SixScenarios);
        state.SelectScenario("happy_path_approved");

        state.SetSearch("fairness");

        Assert.Equal("fairness_review_needed", state.SelectedScenarioId);
        Assert.Single(state.FilteredScenarios);
    }

    [Fact]
    public void ScenarioPickerState_ClearFilters_RestoresFullListAndSelection()
    {
        var state = new ScenarioPickerState();
        state.SetScenarios(SixScenarios);
        state.SetSearch("fairness");
        state.ClearFilters();

        Assert.Equal(6, state.FilteredScenarios.Count);
        Assert.NotNull(state.SelectedScenarioId);
    }

    [Theory]
    [InlineData("Approved", "approve")]
    [InlineData("approve", "approve")]
    [InlineData("DENY", "deny")]
    public void NormalizeOutcome_MapsValuesConsistently(string input, string expected)
    {
        Assert.Equal(expected, ScenarioPickerFilter.NormalizeOutcome(input));
    }

    private static IReadOnlyList<ScenarioSummaryResponse> BuildTwentyScenarios()
    {
        var outcomes = new[] { "approve", "review", "deny" };
        var scenarios = new List<ScenarioSummaryResponse>(20);

        for (var i = 1; i <= 20; i++)
        {
            var id = $"demo_case_{i:D2}";
            scenarios.Add(new ScenarioSummaryResponse(
                id,
                $"Demo Case {i:D2}",
                $"Description for demo case {i}.",
                outcomes[i % outcomes.Length],
                $"Tagline for case-{i:D2}"));
        }

        return scenarios;
    }
}
