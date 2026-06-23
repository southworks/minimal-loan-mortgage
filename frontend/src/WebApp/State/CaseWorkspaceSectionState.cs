using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.Models;

namespace Cohere.LoanProcessing.WebApp.State;

public sealed class CaseWorkspaceSectionState
{
    public static class SectionIds
    {
        public const string Workflow = "workflow";
        public const string DocumentReview = "stage-document-review";
        public const string FinancialAssessment = "stage-financial-assessment";
        public const string HumanReview = "stage-human-review";
        public const string FairnessReview = "stage-fairness-review";
        public const string AccountSetup = "stage-account-setup";
        public const string Completed = "stage-completed";
        public const string Overview = "overview";
        public const string Audit = "audit";
        public const string TechnicalDetails = "technical-details";
    }

    private readonly HashSet<string> _defaultExpanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _userExpanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _userCollapsed = new(StringComparer.Ordinal);
    private string? _activeCaseId;

    public event Action? OnChange;

    public bool IsExpanded(string sectionId)
    {
        if (_userCollapsed.Contains(sectionId))
        {
            return false;
        }

        if (_userExpanded.Contains(sectionId))
        {
            return true;
        }

        return _defaultExpanded.Contains(sectionId);
    }

    public void Toggle(string sectionId)
    {
        if (IsExpanded(sectionId))
        {
            _userExpanded.Remove(sectionId);
            _userCollapsed.Add(sectionId);
        }
        else
        {
            _userCollapsed.Remove(sectionId);
            _userExpanded.Add(sectionId);
        }

        NotifyStateChanged();
    }

    public void Expand(string sectionId)
    {
        _userCollapsed.Remove(sectionId);
        _userExpanded.Add(sectionId);
        NotifyStateChanged();
    }

    public void ApplyDefaults(
        string caseId,
        CaseDetailResponse caseDetail,
        WorkflowProgressResponse? progress,
        bool canSubmitDecision,
        bool canContinueAccountSetup)
    {
        if (!string.Equals(_activeCaseId, caseId, StringComparison.Ordinal))
        {
            _userExpanded.Clear();
            _userCollapsed.Clear();
            _activeCaseId = caseId;
        }

        _defaultExpanded.Clear();

        NotifyStateChanged();
    }

    public void OnWorkflowStageAdvanced(string? previousStageKey, string? currentStageKey)
    {
        // Sections stay collapsed by default; users expand manually or via timeline anchors.
    }

    public static IReadOnlyCollection<string> ResolveDefaultExpanded(
        CaseDetailResponse caseDetail,
        WorkflowProgressResponse? progress,
        bool canSubmitDecision,
        bool canContinueAccountSetup)
    {
        _ = caseDetail;
        _ = progress;
        _ = canSubmitDecision;
        _ = canContinueAccountSetup;
        return Array.Empty<string>();
    }

    public static string? GetSectionIdForStageKey(string? stageKey) =>
        string.IsNullOrWhiteSpace(stageKey) ? null : WorkflowStageUi.GetAgentAnchor(stageKey);

    private void NotifyStateChanged() => OnChange?.Invoke();
}
