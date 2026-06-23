using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.Shared.Contracts.Api.Governance;

namespace Cohere.LoanProcessing.WebApp.Models;

public sealed record StageTechnicalEvidenceModel(
    string StageKey,
    int AllowedCount,
    int DeniedCount,
    int RequireApprovalCount,
    IReadOnlyList<GovernanceDecisionRecordDto> GovernanceDecisions,
    IReadOnlyList<string> ToolsAssociated,
    IReadOnlyList<string> ExecutedAgentRoles,
    IReadOnlyList<string> ExecutedStages)
{
    public bool HasEvidence =>
        GovernanceDecisions.Count > 0
        || ToolsAssociated.Count > 0
        || ExecutedAgentRoles.Count > 0
        || ExecutedStages.Count > 0;
}

public static class StageTechnicalEvidenceUi
{
    public static StageTechnicalEvidenceModel Build(
        string stageKey,
        GovernanceSummaryResponse? governanceSummary,
        ExecutionTraceResponse? executionTrace)
    {
        var governanceDecisions = governanceSummary?.AgtIntegration?.RecentDecisions
            .Where(decision => MatchesStage(stageKey, decision.AgentRole))
            .OrderByDescending(decision => decision.TimestampUtc)
            .ToList()
            ?? [];

        var toolsAssociated = executionTrace?.McpToolsInvoked
            .Where(toolName => MatchesStage(stageKey, toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        var executedAgentRoles = executionTrace?.ExecutedAgentRoles
            .Where(agentRole => MatchesStage(stageKey, agentRole))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(agentRole => agentRole, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        var executedStages = executionTrace?.ExecutedStages
            .Where(stage => MatchesStage(stageKey, stage))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stage => stage, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        return new StageTechnicalEvidenceModel(
            stageKey,
            CountDecisions(governanceDecisions, "Allow"),
            CountDecisions(governanceDecisions, "Deny"),
            governanceDecisions.Count(IsRequireApprovalDecision),
            governanceDecisions,
            toolsAssociated,
            executedAgentRoles,
            executedStages);
    }

    private static int CountDecisions(IEnumerable<GovernanceDecisionRecordDto> decisions, string expectedDecision) =>
        decisions.Count(decision => string.Equals(decision.Decision, expectedDecision, StringComparison.OrdinalIgnoreCase));

    private static bool IsRequireApprovalDecision(GovernanceDecisionRecordDto decision) =>
        decision.Decision.Contains("Require", StringComparison.OrdinalIgnoreCase)
        || decision.Decision.Contains("Approval", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesStage(string stageKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Normalize(value);
        return GetStageTokens(stageKey).Any(normalized.Contains);
    }

    private static IReadOnlyList<string> GetStageTokens(string stageKey) => stageKey switch
    {
        "DocumentProcessing" =>
        [
            "document", "identity", "paystub", "income", "employment", "bankstatement", "appraisal",
            "rawdocuments", "requiredevidence", "normalizedevidence", "evidencebundle"
        ],
        "Underwriting" =>
        [
            "underwriting", "risk", "dossier", "metrics", "lendingrules", "policy", "bankstatementsummary"
        ],
        "ResponsibleAiReview" =>
        [
            "responsible", "rai", "fairness", "explanation", "flag"
        ],
        "LoanSetup" =>
        [
            "loansetup", "setup", "account"
        ],
        "HumanDecision" =>
        [
            "human", "decision", "approval"
        ],
        _ => [Normalize(stageKey)]
    };

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant);
        return new string(chars.ToArray());
    }
}
