using Cohere.LoanProcessing.WebApp.Services;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class BackendWorkflowMapperTests
{
    [Fact]
    public void MapRunStatus_StopsPollingAtHumanApprovalGate()
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
            Status = "AwaitingHumanApproval",
            AgentOutputs = new Contracts.Backend.BasicWorkflowAgentOutputsResponse(),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var runStatus = BackendWorkflowMapper.ToRunStatus(session);

        Assert.Equal("Succeeded", runStatus.Status);
        Assert.Equal("AwaitingHumanApproval", BackendWorkflowMapper.ToDetail(session).Status);
        Assert.Contains("SubmitDecision", BackendWorkflowMapper.ToDetail(session).AllowedActions);
    }
}
