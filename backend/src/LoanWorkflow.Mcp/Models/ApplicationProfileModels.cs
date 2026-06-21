namespace LoanWorkflow.Mcp.Models;

public sealed class ApplicationProfile
{
    public string? LoanProduct { get; init; }

    public string? LoanPurpose { get; init; }

    public string? OccupancyType { get; init; }

    public decimal? RequestedLoanAmount { get; init; }

    public decimal? PurchasePrice { get; init; }

    public decimal? DownPayment { get; init; }

    public decimal? DeclaredMonthlyIncome { get; init; }

    public decimal? ExistingMonthlyDebt { get; init; }
}

public sealed class GetApplicationProfileResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required ApplicationProfile Profile { get; init; }

    public string? SourceDocumentId { get; init; }

    public bool Found { get; init; }
}
