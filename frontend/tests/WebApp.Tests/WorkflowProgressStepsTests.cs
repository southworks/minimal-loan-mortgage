using Cohere.LoanProcessing.WebApp.Services;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class WorkflowProgressStepsTests
{
    [Fact]
    public void ToProgress_ReturnsApiMappedStepsWithoutSyntheticCompletedStage()
    {
        var session = CaseSession.Create(new Models.SeedCaseDefinition(
            "APP-001",
            "Olivia Bennett",
            null,
            390_000m,
            520_000m,
            11_500m,
            2_100m,
            "2211 Birch Court, Denver, CO 80211",
            "Denver, CO",
            "Single Family",
            "Fixed Rate Mortgage",
            "Purchase",
            "approve",
            "meets_credit_income_collateral_policy",
            "Operations Manager",
            "Description",
            "Demo tagline"));

        BackendWorkflowMapper.ApplyWorkflowStatus(session, new Contracts.Backend.BasicWorkflowStatusResponse
        {
            ExecutionId = "exec-001",
            CaseId = "APP-001",
            Status = "Running",
            AgentOutputs = new Contracts.Backend.BasicWorkflowAgentOutputsResponse
            {
                DocumentProcessing = """{"summary":"Docs sufficient.","decision":"Complete","evidence":"Employment verified."}"""
            },
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var progress = BackendWorkflowMapper.ToProgress(session);

        Assert.Equal(5, progress.Steps.Count);
        Assert.DoesNotContain(progress.Steps, step => step.StageKey == "Completed");
        Assert.Equal("Document Review", progress.Steps[0].Name);
        Assert.Equal("Complete. Docs sufficient.", progress.Steps[0].Summary);
        Assert.Equal("Succeeded", progress.Steps[0].ExecutionStatus);
    }
}
