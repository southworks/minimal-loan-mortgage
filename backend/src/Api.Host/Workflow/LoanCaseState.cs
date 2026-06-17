namespace CohereLoanAndMortgage.Api.Host.Workflow;

public sealed class LoanCaseState
{
    public required string CaseId { get; init; }

    public required LoanApplicationInput Application { get; set; }

    public LoanCaseStatus Status { get; set; } = LoanCaseStatus.Pending;

    public LoanWorkflowStep CurrentStep { get; set; } = LoanWorkflowStep.Submitted;

    public PendingApprovalInfo? PendingApproval { get; set; }

    public AgentStepResult? DocumentProcessing { get; set; }

    public AgentStepResult? Underwriting { get; set; }

    public AgentStepResult? ResponsibleAi { get; set; }

    public AgentStepResult? LoanSetup { get; set; }

    public List<StoredDocumentInfo> Documents { get; } = [];

    public string? FailureReason { get; set; }

    public List<TimelineEntry> Timeline { get; } = [];

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StoredDocumentInfo
{
    public required string Reference { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required DateTimeOffset UploadedAtUtc { get; init; }
}

public sealed class LoanApplicationInput
{
    public required string ApplicantName { get; init; }

    public required string Email { get; init; }

    public required decimal RequestedAmount { get; init; }

    public required int TermMonths { get; init; }

    public required decimal AnnualIncome { get; init; }

    public required string EmploymentStatus { get; init; }

    public required string PropertyAddress { get; init; }

    public string? Notes { get; init; }

    public List<string> DocumentReferences { get; } = [];
}

public sealed class PendingApprovalInfo
{
    public required ApprovalType ApprovalType { get; init; }

    public required string Summary { get; init; }

    public required string AgentOutput { get; init; }

    public string? RecommendedAction { get; init; }
}

public sealed class TimelineEntry
{
    public required LoanWorkflowStep Step { get; init; }

    public required string Message { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
