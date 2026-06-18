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

    public async Task<IndexCaseDocumentsResponse> EnsureBlobDocumentsIndexedAsync(
        string caseId,
        string executionId,
        IReadOnlyList<LoadedCaseDocument> documents,
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
                EvidenceIndexAdapter.BlobDocumentSourceType,
                sourceKey: $"blob:{caseId}",
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Blob evidence indexing completed for case {CaseId}, execution {ExecutionId}. AlreadyIndexed={AlreadyIndexed}, ChunkCount={ChunkCount}.",
            caseId,
            executionId,
            response.AlreadyIndexed,
            response.ChunkCount);

        return response;
    }

    private static CaseDocument ToCaseDocument(LoadedCaseDocument document)
    {
        string contentForSearch = document.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            ? document.Content.ToString()
            : $"Binary document {document.FileName} with content type {document.ContentType}. Blob path: {document.BlobName}.";

        return new CaseDocument
        {
            DocumentId = Path.GetFileNameWithoutExtension(document.FileName),
            DocumentType = document.ContentType,
            Category = "blob-document",
            SourcePath = document.BlobName,
            Content = JsonSerializer.SerializeToElement(new
            {
                document.FileName,
                document.ContentType,
                document.BlobName,
                document.Reference,
                document.LastModifiedUtc
            }),
            SummaryText = string.Join(Environment.NewLine, [
                $"FileName: {document.FileName}",
                $"ContentType: {document.ContentType}",
                $"BlobName: {document.BlobName}",
                $"Reference: {document.Reference}",
                $"LastModifiedUtc: {document.LastModifiedUtc:O}",
                $"Content: {contentForSearch}"
            ])
        };
    }
}
