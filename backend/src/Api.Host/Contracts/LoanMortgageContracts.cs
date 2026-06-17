namespace CohereLoanAndMortgage.Api.Host.Contracts;

public sealed class CreateLoanApplicationRequest
{
    public required string ApplicantName { get; init; }

    public required string Email { get; init; }

    public required decimal RequestedAmount { get; init; }

    public required int TermMonths { get; init; }

    public required decimal AnnualIncome { get; init; }

    public required string EmploymentStatus { get; init; }

    public required string PropertyAddress { get; init; }

    public string? Notes { get; init; }
}

public sealed class HumanDecisionRequest
{
    public required string DecisionType { get; init; }

    public required bool Approved { get; init; }

    public string? ReviewerComment { get; init; }
}

public sealed class LoanCaseResponse
{
    public required string CaseId { get; init; }

    public required string Status { get; init; }

    public required string CurrentStep { get; init; }

    public PendingApprovalResponse? PendingApproval { get; init; }

    public required IReadOnlyList<TimelineEntryResponse> Timeline { get; init; }

    public required IReadOnlyList<DocumentReferenceResponse> Documents { get; init; }

    public required AgentOutputsResponse AgentOutputs { get; init; }

    public string? FailureReason { get; init; }

    public required DateTimeOffset LastUpdatedUtc { get; init; }
}

public sealed class LoanProgressResponse
{
    public required string CaseId { get; init; }

    public required string Status { get; init; }

    public required string CurrentStep { get; init; }

    public PendingApprovalResponse? PendingApproval { get; init; }

    public required IReadOnlyList<TimelineEntryResponse> Timeline { get; init; }

    public required DateTimeOffset LastUpdatedUtc { get; init; }
}

public sealed class UploadDocumentsResponse
{
    public required string CaseId { get; init; }

    public required IReadOnlyList<DocumentReferenceResponse> UploadedDocuments { get; init; }

    public required IReadOnlyList<DocumentReferenceResponse> AllDocuments { get; init; }
}

public sealed class DocumentReferenceResponse
{
    public required string Reference { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required DateTimeOffset UploadedAtUtc { get; init; }
}

public sealed class PendingApprovalResponse
{
    public required string ApprovalType { get; init; }

    public required string Summary { get; init; }

    public required string AgentOutput { get; init; }

    public string? RecommendedAction { get; init; }

    public bool CanApprove { get; init; } = true;

    public bool CanReject { get; init; } = true;
}

public sealed class TimelineEntryResponse
{
    public required string Step { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }
}

public sealed class AgentOutputsResponse
{
    public AgentStepResultResponse? DocumentProcessing { get; init; }

    public AgentStepResultResponse? Underwriting { get; init; }

    public AgentStepResultResponse? ResponsibleAi { get; init; }

    public AgentStepResultResponse? LoanSetup { get; init; }
}

public sealed class AgentStepResultResponse
{
    public required string AgentName { get; init; }

    public required string Summary { get; init; }

    public required string Decision { get; init; }

    public required string Evidence { get; init; }

    public required IReadOnlyList<string> MemoryUpdates { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }
}

public sealed class ProblemDetailsResponse
{
    public required string Title { get; init; }

    public required string Detail { get; init; }
}
