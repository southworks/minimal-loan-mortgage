namespace CohereLoanAndMortgage.Api.Host.Workflow;

public sealed class UnderwritingApprovalPrompt
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required string Summary { get; init; }

    public required string UnderwritingOutput { get; init; }
}

public sealed class UnderwritingApprovalDecision
{
    public required bool Approved { get; init; }

    public string? ReviewerComment { get; init; }
}
