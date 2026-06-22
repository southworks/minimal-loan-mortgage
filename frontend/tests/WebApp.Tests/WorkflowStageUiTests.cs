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
    [InlineData("Reject", "rec-deny")]
    [InlineData("Review", "rec-review")]
    public void MapsRecommendationToCssClass(string recommendation, string expectedCss)
    {
        Assert.Equal(expectedCss, WorkflowStageUi.RecommendationCssClass(recommendation));
    }

    [Theory]
    [InlineData("Complete", "rec-approve")]
    [InlineData("Reject", "rec-deny")]
    [InlineData("Approval Not Supported", "rec-review")]
    [InlineData("Additional Information Required", "rec-review")]
    public void MapsDecisionToCssClass(string decision, string expectedCss)
    {
        Assert.Equal(expectedCss, WorkflowStageUi.DecisionCssClass(decision));
    }

    [Fact]
    public void LoanSetupStatusLabel_ReflectsActionRequired()
    {
        var result = new Cohere.LoanProcessing.Shared.Contracts.Api.Cases.LoanSetupResultDto(
            DemoAccountId: null,
            SetupSummary: "Package prepared.",
            Status: "ActionRequired",
            RequiresAdditionalInformation: true,
            Decision: "Additional Information Required");

        Assert.Equal("Additional information required", WorkflowStageUi.LoanSetupStatusLabel(result));
        Assert.Equal("agent-status-warning", WorkflowStageUi.LoanSetupStatusCssClass(result));
    }
}
