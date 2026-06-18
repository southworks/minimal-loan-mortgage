namespace LoanWorkflow.Mcp.Models;

public sealed class EvidenceMatch
{
    public required string DocumentId { get; init; }

    public required string DocumentType { get; init; }

    public required string Category { get; init; }

    public required string Snippet { get; init; }

    public double Score { get; init; }
}

public sealed class SearchCaseEvidenceResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required string Query { get; init; }

    public required IReadOnlyList<EvidenceMatch> Matches { get; init; }
}

public sealed class UnderwritingCategoryContext
{
    public required string Category { get; init; }

    public required IReadOnlyList<EvidenceMatch> Matches { get; init; }
}

public sealed class GetUnderwritingContextResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required IReadOnlyList<UnderwritingCategoryContext> Categories { get; init; }
}
