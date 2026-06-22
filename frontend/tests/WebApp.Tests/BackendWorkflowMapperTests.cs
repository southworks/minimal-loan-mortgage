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

    [Fact]
    public void BuildSteps_UsesStructuredAgentSummariesAndBlockedLoanSetup()
    {
        var session = CaseSession.Create(new Models.SeedCaseDefinition(
            "APP-020",
            "Alexander Ward",
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
            "reject",
            "borderline_case",
            "Business Development Director",
            "Description",
            "Demo tagline"));

        BackendWorkflowMapper.ApplyWorkflowStatus(session, new Contracts.Backend.BasicWorkflowStatusResponse
        {
            ExecutionId = "exec-020",
            CaseId = "APP-020",
            Status = "Running",
            AgentOutputs = new Contracts.Backend.BasicWorkflowAgentOutputsResponse
            {
                DocumentProcessing = """{"summary":"Docs sufficient.","decision":"Complete","evidence":"Employment verified."}""",
                Underwriting = """{"summary":"Borderline case.","decision":"Reject","evidence":"LTV exceeds threshold.","keyFacts":["LTV: 83.6%"],"policyRefs":["UW-100"]}""",
                ResponsibleAi = """{"summary":"Override lacks rationale.","decision":"Approval Not Supported","approvalAssessment":"Not Supported"}""",
                LoanSetup = """{"summary":"Package prepared.","decision":"Additional Information Required","evidence":"Missing override rationale."}"""
            },
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var progress = BackendWorkflowMapper.ToProgress(session);

        Assert.Equal("Complete. Docs sufficient.", progress.Steps[0].Summary);
        Assert.Equal("Reject: Borderline case.", progress.Steps[1].Summary);
        Assert.Equal("Approval Not Supported. Override lacks rationale.", progress.Steps[3].Summary);
        Assert.Equal("Additional Information Required", progress.Steps[4].Summary);
        Assert.Equal("Blocked", progress.Steps[4].ExecutionStatus);
    }
}
