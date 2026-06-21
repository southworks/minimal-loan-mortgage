using System.Text.Json;
using CohereLoanAndMortgage.Api.Host.Workflow;
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
            documents = documents.Select(document => workflowDocumentsPreIndexed
                ? (object)new
                {
                    documentId = Path.GetFileNameWithoutExtension(document.FileName),
                    fileName = document.FileName,
                    sourcePath = document.BlobName,
                    documentType = document.ContentType
                }
                : new
                {
                    documentId = Path.GetFileNameWithoutExtension(document.FileName),
                    fileName = document.FileName,
                    sourcePath = document.BlobName,
                    documentType = document.ContentType,
                    extractedText = document.ExtractedText,
                    extractionMode = document.ExtractionMode,
                    extractionSucceeded = document.ExtractionSucceeded,
                    extractionMessage = document.ExtractionMessage
                })
        };

        return CreateJsonMessages(payload);
    }

    public static ChatMessage CreateAgentTransitionMessage(
        string caseId,
        string executionId,
        AgentStepResult previousResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(previousResult);

        var payload = new
        {
            caseId,
            executionId,
            summary = previousResult.Summary,
            decision = previousResult.Decision,
            evidence = previousResult.Evidence
        };

        return CreateJsonMessage(payload);
    }

    private static List<ChatMessage> CreateJsonMessages(object payload)
    {
        return [CreateJsonMessage(payload)];
    }

    private static ChatMessage CreateJsonMessage(object payload)
    {
        string json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        return new ChatMessage(ChatRole.User, json);
    }
}
