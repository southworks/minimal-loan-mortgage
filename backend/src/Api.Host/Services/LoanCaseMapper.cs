using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Services;

public static class LoanCaseMapper
{
    public static LoanCaseResponse ToResponse(LoanCaseState state) => new()
    {
        CaseId = state.CaseId,
        ExecutionId = state.ExecutionId,
        Status = state.Status.ToString(),
        CurrentStep = state.CurrentStep.ToString(),
        PendingApproval = state.PendingApproval is null
            ? null
            : new PendingApprovalResponse
            {
                ApprovalType = state.PendingApproval.ApprovalType.ToString(),
                Summary = state.PendingApproval.Summary,
                AgentOutput = state.PendingApproval.AgentOutput,
                RecommendedAction = state.PendingApproval.RecommendedAction
            },
        Timeline = state.Timeline.Select(ToTimelineEntry).ToArray(),
        Documents = state.Documents.Select(ToDocumentReference).ToArray(),
        AgentOutputs = ToAgentOutputs(state),
        FailureReason = state.FailureReason,
        LastUpdatedUtc = state.LastUpdatedUtc
    };

    public static LoanProgressResponse ToProgressResponse(LoanCaseState state) => new()
    {
        CaseId = state.CaseId,
        ExecutionId = state.ExecutionId,
        Status = state.Status.ToString(),
        CurrentStep = state.CurrentStep.ToString(),
        PendingApproval = state.PendingApproval is null
            ? null
            : new PendingApprovalResponse
            {
                ApprovalType = state.PendingApproval.ApprovalType.ToString(),
                Summary = state.PendingApproval.Summary,
                AgentOutput = state.PendingApproval.AgentOutput,
                RecommendedAction = state.PendingApproval.RecommendedAction
            },
        Timeline = state.Timeline.Select(ToTimelineEntry).ToArray(),
        LastUpdatedUtc = state.LastUpdatedUtc
    };

    public static DocumentReferenceResponse ToDocumentReference(StoredDocumentInfo document) => new()
    {
        Reference = document.Reference,
        FileName = document.FileName,
        ContentType = document.ContentType,
        UploadedAtUtc = document.UploadedAtUtc
    };

    public static StoredDocumentInfo ToStoredDocumentInfo(LoadedCaseDocument document) => new()
    {
        Reference = document.Reference,
        FileName = document.FileName,
        ContentType = document.ContentType,
        UploadedAtUtc = document.LastModifiedUtc
    };

    private static AgentOutputsResponse ToAgentOutputs(LoanCaseState state) => new()
    {
        DocumentProcessing = ToAgentStepResult(state.DocumentProcessing),
        Underwriting = ToAgentStepResult(state.Underwriting),
        ResponsibleAi = ToAgentStepResult(state.ResponsibleAi),
        LoanSetup = ToAgentStepResult(state.LoanSetup)
    };

    private static AgentStepResultResponse? ToAgentStepResult(AgentStepResult? output) =>
        output is null
            ? null
            : new AgentStepResultResponse
            {
                AgentName = output.AgentName,
                Summary = output.Summary,
                Decision = output.Decision,
                Evidence = output.Evidence,
                MemoryUpdates = output.MemoryUpdates,
                CompletedAtUtc = output.CompletedAtUtc
            };

    private static TimelineEntryResponse ToTimelineEntry(TimelineEntry entry) => new()
    {
        Step = entry.Step.ToString(),
        Message = entry.Message,
        TimestampUtc = entry.TimestampUtc
    };
}
