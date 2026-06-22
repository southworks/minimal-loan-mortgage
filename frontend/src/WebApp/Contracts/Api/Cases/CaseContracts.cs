using Cohere.LoanProcessing.Shared.Contracts.Agents;

namespace Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

public sealed record ScenarioSummaryResponse(
    string ScenarioId,
    string Title,
    string Description,
    string ExpectedOutcome,
    string? DemoTagline = null);

public sealed record CreateCaseRequest(string ScenarioId);

public sealed record SubmitHumanDecisionRequest(bool Approved, string? Notes);

public sealed record ApplicantProfileDto(
    string FullName,
    string Email,
    string EmploymentStatus,
    decimal AnnualIncome,
    string CreditHistorySummary,
    decimal RequestedLoanAmount,
    string? ProductType = null);

public sealed record DocumentRecordDto(
    string DocumentId,
    string DocumentType,
    string FileName,
    bool IsSynthetic);

public sealed record DocumentProcessingResultDto(
    bool IsComplete,
    decimal CompletenessScore,
    IReadOnlyList<string> MissingDocuments,
    IReadOnlyList<string> Inconsistencies,
    string? Summary = null,
    string? Status = null,
    bool RequiresHumanReview = false,
    IReadOnlyList<EvidenceItem>? EvidenceItems = null,
    IReadOnlyList<FlagItem>? Flags = null,
    string? Decision = null,
    string? Evidence = null);

public sealed record UnderwritingScoresDto(
    decimal? RiskScore = null,
    decimal? DtiRatio = null,
    decimal? LtvRatio = null);

public sealed record UnderwritingResultDto(
    string Recommendation,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RiskSignals,
    IReadOnlyList<string> Anomalies,
    string? Summary = null,
    UnderwritingScoresDto? Scores = null,
    IReadOnlyList<EvidenceItem>? EvidenceItems = null,
    IReadOnlyList<RationaleItem>? RationaleItems = null,
    RetrievalSummaryDto? RetrievalSummary = null,
    bool RequiresHumanReview = false,
    bool HasCriticalAnomaly = false,
    string? Decision = null,
    string? EvidenceNarrative = null,
    string? RiskLevel = null,
    IReadOnlyList<string>? PolicyRefs = null,
    IReadOnlyList<string>? KeyFacts = null);

public sealed record ResponsibleAiResultDto(
    bool Passed,
    IReadOnlyList<string> FairnessFlags,
    IReadOnlyList<string> Observations,
    string? Summary = null,
    bool EscalationRecommended = false,
    bool RequiresHumanReview = false,
    IReadOnlyList<FlagItem>? FlagItems = null,
    string? Decision = null,
    string? Evidence = null,
    string? ApprovalAssessment = null,
    string? BiasRisk = null,
    IReadOnlyList<string>? SupportingFacts = null,
    IReadOnlyList<string>? Concerns = null,
    IReadOnlyList<string>? Recommendations = null,
    IReadOnlyList<string>? PolicyRefs = null);

public sealed record LoanSetupResultDto(
    string? DemoAccountId,
    string? SetupSummary,
    string? Status,
    string? OperationId = null,
    DateTimeOffset CompletedAt = default,
    IReadOnlyList<EvidenceItem>? EvidenceItems = null,
    string? Decision = null,
    string? Evidence = null,
    bool RequiresAdditionalInformation = false);

public sealed record HumanDecisionDto(
    bool Approved,
    string? Notes,
    DateTimeOffset DecidedAt);

public sealed record HumanReviewContextDto(
    IReadOnlyList<string> ForcedReviewReasons);

public sealed record CaseNoteDto(
    string Message,
    DateTimeOffset RecordedAt);

public sealed record CaseSummaryResponse(
    string CaseId,
    string ScenarioId,
    string Status,
    string CurrentWorkflowStage,
    string StatusLabel,
    ApplicantProfileDto Applicant,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ExecutionId = null);

public sealed record CaseDetailResponse(
    string CaseId,
    string ScenarioId,
    string Status,
    string CurrentWorkflowStage,
    string StatusLabel,
    ApplicantProfileDto Applicant,
    IReadOnlyList<DocumentRecordDto> Documents,
    DocumentProcessingResultDto? DocumentProcessing,
    UnderwritingResultDto? Underwriting,
    ResponsibleAiResultDto? ResponsibleAi,
    LoanSetupResultDto? LoanSetup,
    HumanDecisionDto? HumanDecision,
    HumanReviewContextDto? HumanReview,
    IReadOnlyList<CaseNoteDto> Notes,
    IReadOnlyList<string> AuditEvents,
    IReadOnlyList<string> AllowedActions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record WorkflowStageResponse(
    string Name,
    string StageKey,
    string ExecutionStatus,
    string? Summary);

public sealed record WorkflowProgressResponse(
    string CaseId,
    string Status,
    string CurrentWorkflowStage,
    string StatusLabel,
    IReadOnlyList<WorkflowStageResponse> Steps,
    IReadOnlyList<string> AllowedActions);

public sealed record WorkflowStartResponse(
    string CaseId,
    string RunId,
    bool IsAsync,
    string Status,
    CaseDetailResponse? Case = null);

public sealed record WorkflowRunStatusResponse(
    string RunId,
    string CaseId,
    string Status,
    string? FoundryThreadId,
    string? FoundryRunId,
    string? ErrorMessage,
    string? CurrentStage,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
