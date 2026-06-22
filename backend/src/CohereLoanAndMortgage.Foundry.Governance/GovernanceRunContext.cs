namespace CohereLoanAndMortgage.Foundry.Governance;

public sealed class GovernanceRunContext
{
    private static readonly AsyncLocal<GovernanceRunContext?> Current = new();

    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public static GovernanceRunContext? CurrentValue => Current.Value;

    public static IDisposable Begin(string caseId, string executionId)
    {
        GovernanceRunContext previous = Current.Value ?? new GovernanceRunContext
        {
            CaseId = string.Empty,
            ExecutionId = string.Empty
        };

        Current.Value = new GovernanceRunContext
        {
            CaseId = caseId,
            ExecutionId = executionId
        };

        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly GovernanceRunContext _previous;

        public Scope(GovernanceRunContext previous) => _previous = previous;

        public void Dispose() => Current.Value = _previous;
    }
}
