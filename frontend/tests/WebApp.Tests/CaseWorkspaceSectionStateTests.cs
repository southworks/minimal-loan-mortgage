using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.State;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class CaseWorkspaceSectionStateTests
{
    private static readonly string[] AllSectionIds =
    [
        CaseWorkspaceSectionState.SectionIds.Workflow,
        CaseWorkspaceSectionState.SectionIds.DocumentReview,
        CaseWorkspaceSectionState.SectionIds.FinancialAssessment,
        CaseWorkspaceSectionState.SectionIds.HumanReview,
        CaseWorkspaceSectionState.SectionIds.FairnessReview,
        CaseWorkspaceSectionState.SectionIds.AccountSetup,
        CaseWorkspaceSectionState.SectionIds.Completed,
        CaseWorkspaceSectionState.SectionIds.Overview,
        CaseWorkspaceSectionState.SectionIds.Audit,
        CaseWorkspaceSectionState.SectionIds.TechnicalDetails
    ];

    [Fact]
    public void ResolveDefaultExpanded_ReturnsEmptySet()
    {
        var caseDetail = CreateDetail(["SubmitDecision"]) with { Status = "AwaitingHumanApproval" };

        var expanded = CaseWorkspaceSectionState.ResolveDefaultExpanded(caseDetail, null, canSubmitDecision: true, canContinueAccountSetup: false);

        Assert.Empty(expanded);
    }

    [Fact]
    public void ApplyDefaults_AllSectionsCollapsedInitially()
    {
        var state = new CaseWorkspaceSectionState();
        var caseDetail = CreateDetail(["StartWorkflow"]) with
        {
            Status = "InReview",
            CurrentWorkflowStage = "Underwriting"
        };

        var progress = new WorkflowProgressResponse(
            caseDetail.CaseId,
            "InReview",
            "Underwriting",
            "InReview",
            [
                new WorkflowStageResponse("Document Review", "DocumentProcessing", "Succeeded", "Done"),
                new WorkflowStageResponse("Financial Assessment", "Underwriting", "InProgress", "Running")
            ],
            []);

        state.ApplyDefaults("case-001", caseDetail, progress, false, false);

        AssertAllSectionsCollapsed(state);
    }

    [Fact]
    public void ApplyDefaults_UserToggleOverridesUntilCaseChanges()
    {
        var state = new CaseWorkspaceSectionState();
        var caseDetail = CreateDetail(["StartWorkflow"]);

        state.ApplyDefaults("case-001", caseDetail, null, false, false);
        Assert.False(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.Workflow));

        state.Toggle(CaseWorkspaceSectionState.SectionIds.Workflow);
        Assert.True(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.Workflow));

        state.ApplyDefaults("case-001", caseDetail, null, false, false);
        Assert.True(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.Workflow));

        state.ApplyDefaults("case-002", caseDetail with { CaseId = "case-002" }, null, false, false);
        AssertAllSectionsCollapsed(state);
    }

    [Fact]
    public void Expand_OpensSectionForAnchorNavigation()
    {
        var state = new CaseWorkspaceSectionState();
        var caseDetail = CreateDetail(["StartWorkflow"]) with
        {
            Status = "InReview",
            CurrentWorkflowStage = "Underwriting"
        };

        var progress = new WorkflowProgressResponse(
            caseDetail.CaseId,
            "InReview",
            "Underwriting",
            "InReview",
            [
                new WorkflowStageResponse("Document Review", "DocumentProcessing", "Succeeded", "Done"),
                new WorkflowStageResponse("Financial Assessment", "Underwriting", "InProgress", "Running")
            ],
            []);

        state.ApplyDefaults("case-001", caseDetail, progress, false, false);
        Assert.False(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.DocumentReview));

        state.Expand(CaseWorkspaceSectionState.SectionIds.DocumentReview);
        Assert.True(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.DocumentReview));
    }

    [Fact]
    public void IsExpanded_HumanReviewCollapsedUntilUserExpands()
    {
        var state = new CaseWorkspaceSectionState();
        var caseDetail = CreateDetail(["SubmitDecision"]) with { Status = "AwaitingHumanApproval" };

        state.ApplyDefaults("case-001", caseDetail, null, canSubmitDecision: true, canContinueAccountSetup: false);
        Assert.False(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.HumanReview));

        state.Expand(CaseWorkspaceSectionState.SectionIds.HumanReview);
        Assert.True(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.HumanReview));
    }

    [Fact]
    public void OnWorkflowStageAdvanced_DoesNotAutoExpandSections()
    {
        var state = new CaseWorkspaceSectionState();
        var caseDetail = CreateDetail([]) with
        {
            Status = "InReview",
            CurrentWorkflowStage = "DocumentProcessing"
        };

        state.ApplyDefaults("case-001", caseDetail, null, false, false);
        state.OnWorkflowStageAdvanced("DocumentProcessing", "Underwriting");

        Assert.False(state.IsExpanded(CaseWorkspaceSectionState.SectionIds.FinancialAssessment));
    }

    [Fact]
    public void ApplyDefaults_SwitchingCasesClearsAllUserSectionState()
    {
        var state = new CaseWorkspaceSectionState();
        var firstCase = CreateDetail(["StartWorkflow"]);
        var secondCase = CreateDetail(["SubmitDecision"]) with
        {
            CaseId = "case-002",
            Status = "AwaitingHumanApproval"
        };

        state.ApplyDefaults(firstCase.CaseId, firstCase, null, false, false);
        foreach (var sectionId in AllSectionIds)
        {
            state.Expand(sectionId);
            Assert.True(state.IsExpanded(sectionId));
        }

        state.ApplyDefaults(secondCase.CaseId, secondCase, null, canSubmitDecision: true, canContinueAccountSetup: false);

        AssertAllSectionsCollapsed(state);
    }

    private static CaseDetailResponse CreateDetail(IReadOnlyList<string> allowedActions) =>
        new(
            "case-001",
            "happy_path_approved",
            "Created",
            "None",
            "Created",
            new ApplicantProfileDto("Jane Doe", "jane@example.com", "Employed", 85000m, "Good", 250000m, "Mortgage"),
            [new DocumentRecordDto("doc-1", "Identity", "identity.pdf", true)],
            null, null, null, null, null, null,
            [],
            [],
            allowedActions,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1);

    private static void AssertAllSectionsCollapsed(CaseWorkspaceSectionState state)
    {
        foreach (var sectionId in AllSectionIds)
        {
            Assert.False(state.IsExpanded(sectionId));
        }
    }
}
