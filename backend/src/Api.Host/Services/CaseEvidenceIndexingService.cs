using System.Text.Json;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Models;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class CaseEvidenceIndexingService
{
    private readonly SearchIndexInitializer _searchIndexInitializer;
    private readonly EvidenceIndexAdapter _evidenceIndexAdapter;
    private readonly ILogger<CaseEvidenceIndexingService> _logger;

    public CaseEvidenceIndexingService(
        SearchIndexInitializer searchIndexInitializer,
        EvidenceIndexAdapter evidenceIndexAdapter,
        ILogger<CaseEvidenceIndexingService> logger)
    {
        _searchIndexInitializer = searchIndexInitializer;
        _evidenceIndexAdapter = evidenceIndexAdapter;
        _logger = logger;
    }

    public Task<IndexCaseDocumentsResponse> EnsureCaseDocumentsIndexedAsync(
        string caseId,
        string executionId,
        IReadOnlyList<NormalizedCaseDocument> documents,
        CancellationToken cancellationToken) =>
        EnsureCaseDocumentsIndexedAsync(
            caseId,
            executionId,
            documents,
            EvidenceIndexAdapter.WorkflowPayloadSourceType,
            EvidenceIndexAdapter.CreateCaseSourceKey(caseId),
            cancellationToken);

    public async Task<IndexCaseDocumentsResponse> EnsureCaseDocumentsIndexedAsync(
        string caseId,
        string executionId,
        IReadOnlyList<NormalizedCaseDocument> documents,
        string sourceType,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        await _searchIndexInitializer.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<CaseDocument> caseDocuments = documents
            .Select(ToCaseDocument)
            .ToArray();

        IndexCaseDocumentsResponse response = await _evidenceIndexAdapter
            .IndexDocumentsAsync(
                caseId,
                executionId,
                caseDocuments,
                sourceType,
                sourceKey,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Case evidence indexing completed for case {CaseId}, execution {ExecutionId}, sourceType {SourceType}, sourceKey {SourceKey}. AlreadyIndexed={AlreadyIndexed}, ChunkCount={ChunkCount}.",
            caseId,
            executionId,
            sourceType,
            sourceKey,
            response.AlreadyIndexed,
            response.ChunkCount);

        return response;
    }

    private static CaseDocument ToCaseDocument(NormalizedCaseDocument document)
    {
        return new CaseDocument
        {
            DocumentId = Path.GetFileNameWithoutExtension(document.FileName),
            DocumentType = document.ContentType,
            Category = EvidenceIndexAdapter.WorkflowPayloadSourceType,
            SourcePath = document.SourcePath,
            Content = JsonSerializer.SerializeToElement(new
            {
                document.FileName,
                document.ContentType,
                document.SourcePath,
                document.Reference,
                document.LastModifiedUtc,
                document.ExtractionMode,
                document.ExtractionSucceeded,
                document.ExtractionMessage
            }),
            SummaryText = string.Join(Environment.NewLine, [
                $"FileName: {document.FileName}",
                $"ContentType: {document.ContentType}",
                $"SourcePath: {document.SourcePath}",
                $"Reference: {document.Reference}",
                $"LastModifiedUtc: {document.LastModifiedUtc:O}",
                $"ExtractionMode: {document.ExtractionMode}",
                $"ExtractionSucceeded: {document.ExtractionSucceeded}",
                $"ExtractionMessage: {document.ExtractionMessage ?? string.Empty}",
                $"Content: {document.ExtractedText}"
            ])
        };
    }
}
