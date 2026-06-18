using System.Text.Json;

namespace LoanWorkflow.Mcp.Models;

public sealed class AccountSetupDraft
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required string SetupStatus { get; init; }

    public required JsonElement ApplicationSummary { get; init; }

    public required JsonElement UnderwritingSummary { get; init; }

    public required JsonElement HumanDecisionSummary { get; init; }

    public required JsonElement ResponsibleAiSummary { get; init; }

    public required IReadOnlyList<string> OperationalRequirements { get; init; }
}

public sealed class BuildAccountSetupDraftResponse
{
    public required AccountSetupDraft Draft { get; init; }

    public required IReadOnlyList<string> MissingFields { get; init; }

    public required IReadOnlyList<string> BlockingIssues { get; init; }
}
