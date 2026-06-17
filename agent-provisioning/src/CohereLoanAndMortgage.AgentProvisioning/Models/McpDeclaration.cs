namespace CohereLoanAndMortgage.AgentProvisioning.Models;

public sealed class McpDeclaration
{
    public IReadOnlyList<McpDependency> Dependencies { get; init; } = [];
}

public sealed class McpDependency
{
    public required string ConnectionName { get; init; }

    public required string ServerLabel { get; init; }

    public bool Required { get; init; } = true;
}
