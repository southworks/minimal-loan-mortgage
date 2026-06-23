using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.Shared.Contracts.Api.Governance;

public sealed record GovernanceDecisionRecordDto(
    string DecisionId,
    string CaseId,
    string AgentId,
    string? AgentRole,
    string ToolName,
    string Decision,
    string? PolicyBundleId,
    string? PolicyVersion,
    string? Rationale,
    DateTimeOffset TimestampUtc)
{
    public string? MatchedRule => PolicyBundleId;

    public string? Reason => Rationale;
}

public sealed record GovernanceEvidenceSummaryDto(
    int UnderwritingEvidenceCount,
    int RaiFlagCount,
    bool? RaiPassed,
    bool TraceEvidencePresent,
    IReadOnlyList<string> ExecutedAgentRoles,
    int McpToolInvocationCount);

public sealed record AgtIntegrationSummaryDto(
    string ToolkitName,
    string ToolkitVersion,
    string PolicyBundleVersion,
    int AllowedCount,
    int DeniedCount,
    int RequireApprovalCount,
    IReadOnlyList<GovernanceDecisionRecordDto> RecentDecisions);

public sealed record GovernanceSummaryResponse(
    bool IsAvailable,
    IReadOnlyList<string> PolicyEvaluations,
    GovernanceEvidenceSummaryDto EvidenceSummary,
    string GovernanceToolkitVersion,
    AgtIntegrationSummaryDto? AgtIntegration);

public sealed record ExecutionTraceResponse(
    string CaseId,
    bool HasWorkflowSpan,
    bool TraceEvidencePresent,
    IReadOnlyList<string> ExecutedStages,
    IReadOnlyList<string> ExecutedAgentRoles,
    IReadOnlyList<string> McpToolsInvoked,
    int McpToolInvocationCount,
    WorkflowRunStatusResponse? LatestWorkflowRun,
    IReadOnlyList<string> AgentMemoryEntries,
    IReadOnlyList<Cohere.LoanProcessing.Shared.Contracts.Agents.RetrievalEventDto> RetrievalEvents);
