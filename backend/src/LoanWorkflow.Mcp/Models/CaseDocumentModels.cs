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

    public int IndexedDocumentCount { get; init; }

    public int ChunkCount { get; init; }
}
