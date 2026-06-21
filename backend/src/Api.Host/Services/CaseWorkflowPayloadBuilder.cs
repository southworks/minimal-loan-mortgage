using System.Text.Json;
using LoanWorkflow.Mcp.Adapters;
using Microsoft.Extensions.AI;

namespace CohereLoanAndMortgage.Api.Host.Services;

public static class CaseWorkflowPayloadBuilder
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    public static List<ChatMessage> CreateInitialMessages(
        string caseId,
        string executionId,
        IReadOnlyList<NormalizedCaseDocument> documents,
        bool workflowDocumentsPreIndexed = false)
    {
        var payload = new
        {
            caseId,
            executionId,
            workflowDocumentsPreIndexed,
            indexingIdentity = new
            {
                sourceType = EvidenceIndexAdapter.WorkflowPayloadSourceType,
                sourceKey = EvidenceIndexAdapter.CreateCaseSourceKey(caseId)
            },
            documents = documents.Select(document => new
            {
                documentId = Path.GetFileNameWithoutExtension(document.FileName),
                documentType = document.ContentType,
                category = EvidenceIndexAdapter.WorkflowPayloadSourceType,
                sourcePath = document.BlobName,
                fileName = document.FileName,
                extractedText = workflowDocumentsPreIndexed ? null : document.ExtractedText,
                extractionMode = document.ExtractionMode,
                extractionSucceeded = document.ExtractionSucceeded,
                extractionMessage = document.ExtractionMessage
            })
        };

        string json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        string prompt = workflowDocumentsPreIndexed
            ? $"""
               Normalized case payload. workflowDocumentsPreIndexed is true; case documents are already indexed. Do not call index_case_documents. Start with enrich_customer_context, then call search_case_evidence exactly twice (topK 3).

               {json}
               """
            : $"""
               Normalized case payload for this workflow run. Document text was extracted once at workflow start.

               {json}
               """;

        return [new ChatMessage(ChatRole.User, prompt)];
    }
}
