using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.State;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class RecentExecutionsListStateTests
{
    [Fact]
    public void SetCases_OrdersByUpdatedAtDescending()
    {
        var state = new RecentExecutionsListState();
        var cases = new[]
        {
            CreateCase("older", DateTimeOffset.UtcNow.AddHours(-2)),
            CreateCase("newer", DateTimeOffset.UtcNow)
        };

        state.SetCases(cases);

        Assert.Equal("newer", state.AllCases[0].CaseId);
        Assert.Equal("older", state.AllCases[1].CaseId);
    }

    [Fact]
    public void SetCases_WithFiveCases_ShowsAllWithoutExpand()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(5));

        Assert.Equal(5, state.VisibleCount);
        Assert.False(state.CanExpand);
        Assert.Equal(5, state.VisibleCases.Count);
    }

    [Fact]
    public void SetCases_WithSixCases_ShowsAllWithoutExpand()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(6));

        Assert.Equal(6, state.VisibleCount);
        Assert.False(state.CanExpand);
    }

    [Fact]
    public void SetCases_WithEightCases_ShowsSixByDefault()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(8));

        Assert.True(state.CanExpand);
        Assert.False(state.IsExpanded);
        Assert.Equal(6, state.VisibleCount);
        Assert.Equal(2, state.HiddenCount);
        Assert.Equal(6, state.VisibleCases.Count);
    }

    [Fact]
    public void ShowMore_RevealsAllCases()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(10));

        state.ShowMore();

        Assert.True(state.IsExpanded);
        Assert.Equal(10, state.VisibleCount);
        Assert.Equal(10, state.VisibleCases.Count);
    }

    [Fact]
    public void ShowLess_CollapsesBackToDefaultCount()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(10));
        state.ShowMore();

        state.ShowLess();

        Assert.False(state.IsExpanded);
        Assert.Equal(6, state.VisibleCount);
    }

    [Fact]
    public void SetCases_ResetsExpandedState()
    {
        var state = new RecentExecutionsListState();
        state.SetCases(BuildCases(10));
        state.ShowMore();

        state.SetCases(BuildCases(10));

        Assert.False(state.IsExpanded);
        Assert.Equal(6, state.VisibleCount);
    }

    private static IReadOnlyList<CaseSummaryResponse> BuildCases(int count) =>
        Enumerable.Range(1, count)
            .Select(i => CreateCase($"case-{i:D2}", DateTimeOffset.UtcNow.AddMinutes(-i)))
            .ToList();

    private static CaseSummaryResponse CreateCase(string caseId, DateTimeOffset updatedAt) =>
        new(
            caseId,
            "happy_path_approved",
            "InReview",
            "Underwriting",
            "InReview",
            new ApplicantProfileDto("Jane Doe", "jane@example.com", "Employed", 85000m, "Good", 250000m, "Mortgage"),
            updatedAt.AddHours(-1),
            updatedAt);
}
