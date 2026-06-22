namespace Cohere.LoanProcessing.WebApp.Models;

public sealed record SeedCaseDefinition(
    string CaseId,
    string BorrowerName,
    string? CoBorrowerName,
    decimal RequestedLoanAmount,
    decimal? PurchasePrice,
    decimal? MonthlyIncome,
    decimal? MonthlyDebt,
    string? PropertyAddress,
    string? PropertyCityState,
    string? PropertyType,
    string? Product,
    string? Purpose,
    string ExpectedOutcome,
    string? PrimaryReason,
    string? OccupationTitle,
    string Description,
    string DemoTagline);
