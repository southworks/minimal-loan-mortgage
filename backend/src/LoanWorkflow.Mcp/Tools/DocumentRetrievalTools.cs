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
            sourceKey: EvidenceIndexAdapter.CreateCustomerContextSourceKey(caseId),
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
                    SummaryText = TruncatePreview(document.SummaryText)
                })
                .ToArray(),
            AvailableCategories = context.AvailableCategories,
            MissingCategories = context.MissingCategories
        };
    }

    [McpServerTool]
    [Description("Indexes workflow-payload evidence under sourceKey case:{caseId}. Skip this tool when workflowDocumentsPreIndexed is true in the payload; the workflow already indexed the case documents.")]
    public async Task<IndexCaseDocumentsResponse> IndexCaseDocuments(
        string caseId,
        string executionId,
        [Description("JSON array of normalized case documents from the workflow payload, or [] when workflowDocumentsPreIndexed is true.")]
        string documentsJson = "[]",
        CancellationToken cancellationToken = default)
    {
        var normalizedDocuments = IsEmptyDocumentsJson(documentsJson)
            ? []
            : NormalizeDocuments(ParseJson(documentsJson));

        return await _evidenceIndexAdapter.IndexDocumentsAsync(
            caseId,
            executionId,
            normalizedDocuments,
            EvidenceIndexAdapter.WorkflowPayloadSourceType,
            sourceKey: EvidenceIndexAdapter.CreateCaseSourceKey(caseId),
            cancellationToken: cancellationToken);
    }

    [McpServerTool]
    [Description("Searches indexed case evidence using Azure AI Search vector retrieval and Cohere rerank. Filter by sourceType workflow-payload or customer-context when comparing submitted documents against supporting evidence. The query parameter is required and must describe what to retrieve.")]
    public async Task<SearchCaseEvidenceResponse> SearchCaseEvidence(
        string caseId,
        string executionId,
        [Description("Required natural-language search query. Use key claims or topics from the submitted documents, such as applicant income, employment, property address, loan amount, or document category.")]
        string query,
        int topK = 2,
        [Description("Optional evidence source filter: workflow-payload or customer-context.")]
        string? sourceType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        var matches = await _evidenceIndexAdapter.SearchAsync(
            caseId,
            executionId,
            query,
            topK,
            sourceType,
            cancellationToken);

        return new SearchCaseEvidenceResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            Query = query,
            Matches = matches
        };
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static bool IsEmptyDocumentsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            && document.RootElement.GetArrayLength() == 0;
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

            string documentId = item.TryGetProperty("documentId", out var documentIdElement)
                ? documentIdElement.GetString() ?? Guid.NewGuid().ToString("N")
                : item.TryGetProperty("fileName", out var fileNameElement)
                    ? Path.GetFileNameWithoutExtension(fileNameElement.GetString() ?? Guid.NewGuid().ToString("N"))
                    : Guid.NewGuid().ToString("N");

            string documentType = item.TryGetProperty("documentType", out var documentTypeElement)
                ? documentTypeElement.GetString() ?? "unknown"
                : "unknown";

            string category = item.TryGetProperty("category", out var categoryElement)
                ? categoryElement.GetString() ?? EvidenceIndexAdapter.WorkflowPayloadSourceType
                : EvidenceIndexAdapter.WorkflowPayloadSourceType;

            string sourcePath = item.TryGetProperty("sourcePath", out var sourcePathElement)
                ? sourcePathElement.GetString() ?? "workflow-payload"
                : "workflow-payload";

            string summaryText = BuildSummaryText(item);

            results.Add(new CaseDocument
            {
                DocumentId = documentId,
                DocumentType = documentType,
                Category = category,
                SourcePath = sourcePath,
                Content = item.Clone(),
                SummaryText = summaryText
            });
        }

        return results;
    }

    private static string BuildSummaryText(JsonElement item)
    {
        if (item.TryGetProperty("extractedText", out var extractedTextElement)
            && extractedTextElement.ValueKind == JsonValueKind.String)
        {
            var builder = new System.Text.StringBuilder();

            if (item.TryGetProperty("fileName", out var fileNameElement))
            {
                builder.AppendLine($"FileName: {fileNameElement}");
            }

            if (item.TryGetProperty("documentType", out var documentTypeElement))
            {
                builder.AppendLine($"ContentType: {documentTypeElement}");
            }

            if (item.TryGetProperty("sourcePath", out var sourcePathElement))
            {
                builder.AppendLine($"SourcePath: {sourcePathElement}");
            }

            if (item.TryGetProperty("extractionMode", out var extractionModeElement))
            {
                builder.AppendLine($"ExtractionMode: {extractionModeElement}");
            }

            if (item.TryGetProperty("extractionSucceeded", out var extractionSucceededElement))
            {
                builder.AppendLine($"ExtractionSucceeded: {extractionSucceededElement}");
            }

            if (item.TryGetProperty("extractionMessage", out var extractionMessageElement)
                && extractionMessageElement.ValueKind == JsonValueKind.String)
            {
                builder.AppendLine($"ExtractionMessage: {extractionMessageElement.GetString()}");
            }

            builder.AppendLine($"Content: {extractedTextElement.GetString()}");
            return builder.ToString().Trim();
        }

        if (item.TryGetProperty("summaryText", out var summaryTextElement)
            && summaryTextElement.ValueKind == JsonValueKind.String)
        {
            return summaryTextElement.GetString() ?? item.GetRawText();
        }

        return item.GetRawText();
    }

    private static string TruncatePreview(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= ToolResponseLimits.MaxFactPreviewLength)
        {
            return text ?? string.Empty;
        }

        return text[..ToolResponseLimits.MaxFactPreviewLength] + "...";
    }
}
