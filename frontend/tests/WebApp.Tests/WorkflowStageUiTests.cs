using Cohere.LoanProcessing.WebApp.Models;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class WorkflowStageUiTests
{
    [Theory]
    [InlineData("Succeeded", "stage-succeeded", "Completed")]
    [InlineData("InProgress", "stage-in-progress", "In progress")]
    [InlineData("Failed", "stage-failed", "Failed")]
    [InlineData("Blocked", "stage-blocked", "Blocked")]
    [InlineData("NotStarted", "stage-not-started", "Not started")]
    public void MapsExecutionStatusToUi(string status, string expectedCss, string expectedLabel)
    {
        Assert.Equal(expectedCss, WorkflowStageUi.ToCssClass(status));
        Assert.Equal(expectedLabel, WorkflowStageUi.ToLabel(status));
    }

    [Fact]
    public void MapsHumanApprovalPendingToAwaitingDecisionLabel()
    {
        Assert.Equal("Awaiting decision", WorkflowStageUi.ToLabel("InProgress", "Human Approval", "Pending"));
    }

    [Theory]
    [InlineData("Approve", "rec-approve")]
    [InlineData("Deny", "rec-deny")]
    [InlineData("Review", "rec-review")]
    public void MapsRecommendationToCssClass(string recommendation, string expectedCss)
    {
        Assert.Equal(expectedCss, WorkflowStageUi.RecommendationCssClass(recommendation));
    }
}
