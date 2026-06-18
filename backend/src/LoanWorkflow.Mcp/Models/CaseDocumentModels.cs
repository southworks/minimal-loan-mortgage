using System.Text.Json;

namespace LoanWorkflow.Mcp.Models;

public sealed class CaseDocument
{
    public required string DocumentId { get; init; }

    public required string DocumentType { get; init; }

    public required string Category { get; init; }

    public required string SourcePath { get; init; }

    public required JsonElement Content { get; init; }

    public string SummaryText { get; init; } = string.Empty;
}

public sealed class GetCaseDocumentsResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required IReadOnlyList<CaseDocument> Documents { get; init; }

    public required IReadOnlyList<string> AvailableCategories { get; init; }

    public required IReadOnlyList<string> MissingCategories { get; init; }

    public string Source { get; init; } = "local-demo-assets";
}

public sealed class IndexCaseDocumentsResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required string IndexName { get; init; }

    public required string SourceType { get; init; }

    public required string SourceKey { get; init; }

    public required string ContentHash { get; init; }

    public int IndexedDocumentCount { get; init; }

    public int ChunkCount { get; init; }

    public bool AlreadyIndexed { get; init; }
}

public sealed class CustomerContextFact
{
    public required string Category { get; init; }

    public required string DocumentId { get; init; }

    public required string DocumentType { get; init; }

    public required string SummaryText { get; init; }
}

public sealed class EnrichCustomerContextResponse
{
    public required string CaseId { get; init; }

    public required string ExecutionId { get; init; }

    public required string Source { get; init; }

    public required IndexCaseDocumentsResponse Indexing { get; init; }

    public required IReadOnlyList<CustomerContextFact> Facts { get; init; }

    public required IReadOnlyList<string> AvailableCategories { get; init; }

    public required IReadOnlyList<string> MissingCategories { get; init; }
}
