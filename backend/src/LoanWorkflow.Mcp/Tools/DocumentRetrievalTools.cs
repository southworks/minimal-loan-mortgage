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
    [Description("Loads customer context for a discovered caseId, ensures it is indexed for retrieval, and returns compact facts for document comparison.")]
    public async Task<EnrichCustomerContextResponse> EnrichCustomerContext(
        string caseId,
        string executionId,
        CancellationToken cancellationToken)
    {
        var context = await _caseDataAdapter.GetCaseDocumentsAsync(caseId, executionId, cancellationToken);
        var indexing = await _evidenceIndexAdapter.IndexDocumentsAsync(
            caseId,
            executionId,
            context.Documents,
            EvidenceIndexAdapter.CustomerContextSourceType,
            sourceKey: $"assets:{caseId}",
            cancellationToken);

        return new EnrichCustomerContextResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            Source = context.Source,
            Indexing = indexing,
            Facts = context.Documents
                .Select(document => new CustomerContextFact
                {
                    Category = document.Category,
                    DocumentId = document.DocumentId,
                    DocumentType = document.DocumentType,
                    SummaryText = document.SummaryText
                })
                .ToArray(),
            AvailableCategories = context.AvailableCategories,
            MissingCategories = context.MissingCategories
        };
    }

    [McpServerTool]
    [Description("Chunks case documents, generates Azure Foundry embeddings, and indexes evidence in Azure AI Search.")]
    public async Task<IndexCaseDocumentsResponse> IndexCaseDocuments(
        string caseId,
        string executionId,
        [Description("Optional JSON array of case documents. When omitted, demo case documents are loaded automatically.")]
        string? documentsJson = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedDocuments = string.IsNullOrWhiteSpace(documentsJson)
            ? []
            : NormalizeDocuments(ParseJson(documentsJson));
        if (normalizedDocuments.Count == 0)
        {
            var loaded = await _caseDataAdapter.GetCaseDocumentsAsync(caseId, executionId, cancellationToken);
            normalizedDocuments = loaded.Documents.ToList();
        }

        return await _evidenceIndexAdapter.IndexDocumentsAsync(
            caseId,
            executionId,
            normalizedDocuments,
            cancellationToken: cancellationToken);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
