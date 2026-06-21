using System.Text.Json;
using LoanWorkflow.Mcp.Adapters;
using Microsoft.Extensions.AI;

namespace CohereLoanAndMortgage.Api.Host.Services;

public static class CaseWorkflowPayloadBuilder
{
    public static List<ChatMessage> CreateInitialMessages(
        string caseId,
        string executionId,
        IReadOnlyList<NormalizedCaseDocument> documents)
    {
        var payload = new
        {
            caseId,
            executionId,
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
                extractedText = document.ExtractedText,
                extractionMode = document.ExtractionMode,
                extractionSucceeded = document.ExtractionSucceeded,
                extractionMessage = document.ExtractionMessage
            })
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        string prompt =
            """
            Process this loan case using the normalized documents below. Documents were extracted once at workflow start. Only the document-processing step should consume this payload. Later steps must use processed outputs only. Each agent step must return JSON with summary, decision, and evidence. Use executionId for any unique indexing or embedding identity.

            Required document-processing steps:
            1. Call index_case_documents with the documents array from this payload so Cohere embed indexes workflow-payload evidence under sourceKey case:{caseId}.
            2. Call enrich_customer_context to load and index supporting customer-context evidence.
            3. Call search_case_evidence separately for workflow-payload and customer-context to retrieve reranked snippets for comparison.
            4. Return summary, decision, and evidence with concrete text snippets.

            Case payload:
            """ + json;

        return [new ChatMessage(ChatRole.User, prompt)];
    }
}
