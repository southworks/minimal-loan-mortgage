namespace CohereLoanAndMortgage.Api.Host.Contracts;

public sealed class ProblemDetailsResponse
{
    public required string Title { get; init; }

    public required string Detail { get; init; }
}
