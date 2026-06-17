namespace CohereLoanAndMortgage.AgentProvisioning.Models;

public sealed class AgentAssetBundle
{
    public required string AgentDirectory { get; init; }

    public required AgentManifest Manifest { get; init; }

    public required string Instructions { get; init; }

    public required string OutputSchemaJson { get; init; }

    public required MemoryPolicy MemoryPolicy { get; init; }

    public required McpDeclaration Mcp { get; init; }
}

public enum ProvisionOutcome
{
    Created,
    Updated,
    Unchanged,
    Failed
}

public sealed class AgentProvisionResult
{
    public required string AgentName { get; init; }

    public required ProvisionOutcome Outcome { get; init; }

    public string? Message { get; init; }
}
