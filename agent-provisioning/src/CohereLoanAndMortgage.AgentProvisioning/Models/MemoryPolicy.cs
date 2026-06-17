namespace CohereLoanAndMortgage.AgentProvisioning.Models;

public sealed class MemoryPolicy
{
    public bool Enabled { get; init; }

    public IReadOnlyList<string> AllowedWrites { get; init; } = [];

    public IReadOnlyList<string> ReadScopes { get; init; } = [];

    public IReadOnlyList<string> ForbiddenWrites { get; init; } = [];
}
