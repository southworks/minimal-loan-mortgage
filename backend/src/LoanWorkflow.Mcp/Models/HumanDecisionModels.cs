namespace LoanWorkflow.Mcp.Models;

public sealed class ValidateHumanDecisionResponse
{
    public bool IsStructurallyValid { get; init; }

    public required string ConsistencyStatus { get; init; }

    public required IReadOnlyList<string> Flags { get; init; }

    public required IReadOnlyList<string> PolicyRefs { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}
