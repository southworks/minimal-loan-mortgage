using System.ComponentModel;
using System.Text.Json;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Models;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp.Tools;

public sealed class DocumentRetrievalTools
{
    private readonly LocalCaseDataAdapter _caseDataAdapter;
    private readonly EvidenceIndexAdapter _evidenceIndexAdapter;

    public DocumentRetrievalTools(
        LocalCaseDataAdapter caseDataAdapter,
        EvidenceIndexAdapter evidenceIndexAdapter)
    {
        _caseDataAdapter = caseDataAdapter;
        _evidenceIndexAdapter = evidenceIndexAdapter;
    }

    [McpServerTool]
    [Description("Reads structured demo case documents for verification and completeness checks.")]
    public Task<GetCaseDocumentsResponse> GetCaseDocuments(
        string caseId,
        string executionId,
        CancellationToken cancellationToken)
        => _caseDataAdapter.GetCaseDocumentsAsync(caseId, executionId, cancellationToken);

    [McpServerTool]
    [Description("Chunks case documents, generates Cohere embeddings, and indexes evidence in Azure AI Search.")]
    public async Task<IndexCaseDocumentsResponse> IndexCaseDocuments(
        string caseId,
        string executionId,
        JsonElement documents,
        CancellationToken cancellationToken)
    {
        var normalizedDocuments = NormalizeDocuments(documents);
        if (normalizedDocuments.Count == 0)
        {
            var loaded = await _caseDataAdapter.GetCaseDocumentsAsync(caseId, executionId, cancellationToken);
            normalizedDocuments = loaded.Documents.ToList();
        }

        return await _evidenceIndexAdapter.IndexDocumentsAsync(
            caseId,
            executionId,
            normalizedDocuments,
            cancellationToken);
    }

    private static List<CaseDocument> NormalizeDocuments(JsonElement documents)
    {
        if (documents.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<CaseDocument>();

        foreach (var item in documents.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            results.Add(new CaseDocument
            {
                DocumentId = item.TryGetProperty("documentId", out var documentId)
                    ? documentId.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N"),
                DocumentType = item.TryGetProperty("documentType", out var documentType)
                    ? documentType.GetString() ?? "unknown"
                    : "unknown",
                Category = item.TryGetProperty("category", out var category)
                    ? category.GetString() ?? "unknown"
                    : "unknown",
                SourcePath = item.TryGetProperty("sourcePath", out var sourcePath)
                    ? sourcePath.GetString() ?? "workflow-payload"
                    : "workflow-payload",
                Content = item.TryGetProperty("content", out var content)
                    ? content.Clone()
                    : item.Clone(),
                SummaryText = item.TryGetProperty("summaryText", out var summaryText)
                    ? summaryText.GetString() ?? item.GetRawText()
                    : item.GetRawText()
            });
        }

        return results;
    }
}
