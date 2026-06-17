using System.Text.Json;
using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoanWorkflowService
{
    private readonly FoundryAgentProvider _agentProvider;
    private readonly LoanMortgageWorkflowFactory _workflowFactory;
    private readonly InMemoryLoanCaseStore _caseStore;
    private readonly BlobDocumentStorageService _documentStorage;
    private readonly ILogger<LoanWorkflowService> _logger;

    public LoanWorkflowService(
        FoundryAgentProvider agentProvider,
        LoanMortgageWorkflowFactory workflowFactory,
        InMemoryLoanCaseStore caseStore,
        BlobDocumentStorageService documentStorage,
        ILogger<LoanWorkflowService> logger)
    {
        _agentProvider = agentProvider;
        _workflowFactory = workflowFactory;
        _caseStore = caseStore;
        _documentStorage = documentStorage;
        _logger = logger;
    }

    public async Task<LoanCaseResponse> StartWorkflowAsync(
        string caseId,
        string executionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new InvalidOperationException("CaseId is required.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new InvalidOperationException("ExecutionId is required.");
        }

        LoanCaseRecord record = CreateBootstrapRecord(caseId, executionId);

        _logger.LogInformation(
            "Bootstrapped execution {ExecutionId} for case {CaseId}. Loading documents from prefix {CasePrefix}.",
            executionId,
            caseId,
            BlobDocumentStorageService.GetCasePrefix(caseId));

        IReadOnlyList<LoadedCaseDocument> loadedDocuments =
            await _documentStorage.LoadCaseDocumentsAsync(caseId, cancellationToken).ConfigureAwait(false);

        if (loadedDocuments.Count == 0)
        {
            throw new KeyNotFoundException(
                $"Case '{caseId}' was not found in Blob Storage or has no documents under prefix '{BlobDocumentStorageService.GetCasePrefix(caseId)}'.");
        }

        RefreshCaseDocuments(record, loadedDocuments);
        record.State.Status = LoanCaseStatus.Running;
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        AddTimeline(record.State, LoanWorkflowStep.Submitted, $"Workflow started with {loadedDocuments.Count} document(s).");
        _caseStore.Save(record);

        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, caseId, executionId);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
        List<ChatMessage> input = CreateInitialMessages(caseId, executionId, loadedDocuments);

        await ProcessRunAsync(record, workflow, checkpointManager, input, executionId, cancellationToken)
            .ConfigureAwait(false);

        return LoanCaseMapper.ToResponse(record.State);
    }

    public LoanCaseResponse GetExecution(string executionId) =>
        LoanCaseMapper.ToResponse(_caseStore.GetRequired(executionId).State);

    public LoanProgressResponse GetProgress(string executionId) =>
        LoanCaseMapper.ToProgressResponse(_caseStore.GetRequired(executionId).State);

    public async Task<LoanCaseResponse> SubmitDecisionAsync(
        string executionId,
        HumanDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.DecisionType, ApprovalType.Underwriting.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Decision type '{request.DecisionType}' is not supported. Supported types: {ApprovalType.Underwriting}.");
        }

        LoanCaseRecord record = _caseStore.GetRequired(executionId);

        if (record.State.Status != LoanCaseStatus.WaitingForHuman)
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' is not waiting for human approval. Current status: {record.State.Status}.");
        }

        if (record.PendingCheckpoint is null)
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' does not have a saved workflow checkpoint and cannot continue.");
        }

        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
            agents,
            record.State.CaseId,
            record.State.ExecutionId);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await InProcessExecution
            .ResumeStreamingAsync(workflow, record.PendingCheckpoint, checkpointManager, cancellationToken)
            .ConfigureAwait(false);

        bool responded = false;

        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!responded &&
                workflowEvent is RequestInfoEvent requestInfoEvent &&
                requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? _))
            {
                var decision = new UnderwritingApprovalDecision
                {
                    Approved = request.Approved,
                    ReviewerComment = request.ReviewerComment
                };

                record.State.PendingApproval = null;
                record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;

                if (request.Approved)
                {
                    AddTimeline(record.State, LoanWorkflowStep.WaitingForHumanApproval, "Underwriting approved by human reviewer.");
                    record.State.Status = LoanCaseStatus.Running;
                    record.State.CurrentStep = LoanWorkflowStep.ResponsibleAi;
                }
                else
                {
                    AddTimeline(
                        record.State,
                        LoanWorkflowStep.Rejected,
                        string.IsNullOrWhiteSpace(request.ReviewerComment)
                            ? "Underwriting rejected by human reviewer."
                            : $"Underwriting rejected by human reviewer: {request.ReviewerComment}");
                    record.State.Status = LoanCaseStatus.Rejected;
                    record.State.CurrentStep = LoanWorkflowStep.Rejected;
                }

                await run.SendResponseAsync(requestInfoEvent.Request.CreateResponse(decision)).ConfigureAwait(false);
                responded = true;
                _caseStore.Save(record);

                if (!request.Approved)
                {
                    record.PendingCheckpoint = null;
                    _caseStore.Save(record);
                    return LoanCaseMapper.ToResponse(record.State);
                }

                continue;
            }

            ProcessWorkflowEvent(record, workflowEvent, run);

            if (record.State.Status is LoanCaseStatus.Completed or LoanCaseStatus.Rejected or LoanCaseStatus.WaitingForHuman or LoanCaseStatus.Failed)
            {
                break;
            }
        }

        if (!responded)
        {
            throw new InvalidOperationException(
                "The workflow did not re-emit an underwriting approval request after the human decision was submitted.");
        }

        return LoanCaseMapper.ToResponse(record.State);
    }

    private static LoanCaseRecord CreateBootstrapRecord(string caseId, string executionId)
    {
        var state = new LoanCaseState
        {
            CaseId = caseId.Trim(),
            ExecutionId = executionId,
            Status = LoanCaseStatus.Pending,
            CurrentStep = LoanWorkflowStep.Submitted
        };

        return new LoanCaseRecord { State = state };
    }

    private static void RefreshCaseDocuments(LoanCaseRecord record, IReadOnlyList<LoadedCaseDocument> loadedDocuments)
    {
        record.State.Documents.Clear();

        foreach (LoadedCaseDocument document in loadedDocuments)
        {
            record.State.Documents.Add(LoanCaseMapper.ToStoredDocumentInfo(document));
        }
    }

    private async Task ProcessRunAsync(
        LoanCaseRecord record,
        AgentWorkflow workflow,
        CheckpointManager checkpointManager,
        IList<ChatMessage> input,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using StreamingRun run = await InProcessExecution
                .RunStreamingAsync(workflow, input, checkpointManager, sessionId, cancellationToken)
                .ConfigureAwait(false);

            await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                ProcessWorkflowEvent(record, workflowEvent, run);

                if (record.State.Status is LoanCaseStatus.Completed or LoanCaseStatus.Rejected or LoanCaseStatus.WaitingForHuman or LoanCaseStatus.Failed)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkFailed(record, ex.Message);
            throw;
        }
    }

    private void ProcessWorkflowEvent(
        LoanCaseRecord record,
        WorkflowEvent workflowEvent,
        StreamingRun run)
    {
        switch (workflowEvent)
        {
            case AgentResponseEvent agentResponseEvent:
                try
                {
                    UpdateAgentOutput(record, agentResponseEvent.ExecutorId, agentResponseEvent.Response);
                }
                catch (InvalidOperationException ex)
                {
                    MarkFailed(record, ex.Message);
                }

                break;

            case SuperStepCompletedEvent superStepCompleted when superStepCompleted.CompletionInfo?.Checkpoint is not null:
                record.PendingCheckpoint = superStepCompleted.CompletionInfo.Checkpoint;
                break;

            case RequestInfoEvent requestInfoEvent when requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? prompt):
                record.PendingCheckpoint ??= run.Checkpoints.LastOrDefault();
                record.State.Status = LoanCaseStatus.WaitingForHuman;
                record.State.CurrentStep = LoanWorkflowStep.WaitingForHumanApproval;
                record.State.PendingApproval = new PendingApprovalInfo
                {
                    ApprovalType = ApprovalType.Underwriting,
                    Summary = prompt!.Summary,
                    AgentOutput = prompt.UnderwritingOutput,
                    RecommendedAction = "Review the underwriting output and approve or reject to continue."
                };
                AddTimeline(record.State, LoanWorkflowStep.WaitingForHumanApproval, "Waiting for human underwriting approval.");
                _logger.LogInformation(
                    "Case {CaseId} execution {ExecutionId} paused for underwriting approval.",
                    record.State.CaseId,
                    record.State.ExecutionId);
                break;

            case WorkflowOutputEvent outputEvent when outputEvent.Data is UnderwritingApprovalDecision decision && !decision.Approved:
                record.State.Status = LoanCaseStatus.Rejected;
                record.State.CurrentStep = LoanWorkflowStep.Rejected;
                record.PendingCheckpoint = null;
                AddTimeline(record.State, LoanWorkflowStep.Rejected, "Workflow rejected after human decision.");
                break;

            case WorkflowOutputEvent:
                record.State.Status = LoanCaseStatus.Completed;
                record.State.CurrentStep = LoanWorkflowStep.Completed;
                record.State.PendingApproval = null;
                record.PendingCheckpoint = null;
                AddTimeline(record.State, LoanWorkflowStep.Completed, "Loan workflow completed.");
                break;
        }

        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _caseStore.Save(record);
    }

    private void UpdateAgentOutput(LoanCaseRecord record, string executorId, AgentResponse response)
    {
        string rawOutput = WorkflowTextExtractor.FromAgentResponse(response);
        AgentStepResult result = AgentStructuredOutputParser.Parse(executorId, rawOutput);

        if (executorId.Contains("document-processing", StringComparison.OrdinalIgnoreCase))
        {
            record.State.DocumentProcessing = result;
            record.State.CurrentStep = LoanWorkflowStep.DocumentProcessing;
            AddTimeline(record.State, LoanWorkflowStep.DocumentProcessing, result.Summary);
        }
        else if (executorId.Contains("underwriting", StringComparison.OrdinalIgnoreCase))
        {
            record.State.Underwriting = result;
            record.State.CurrentStep = LoanWorkflowStep.Underwriting;
            AddTimeline(record.State, LoanWorkflowStep.Underwriting, result.Summary);
        }
        else if (executorId.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase))
        {
            record.State.ResponsibleAi = result;
            record.State.CurrentStep = LoanWorkflowStep.ResponsibleAi;
            AddTimeline(record.State, LoanWorkflowStep.ResponsibleAi, result.Summary);
        }
        else if (executorId.Contains("loan-setup", StringComparison.OrdinalIgnoreCase))
        {
            record.State.LoanSetup = result;
            record.State.CurrentStep = LoanWorkflowStep.LoanSetup;
            AddTimeline(record.State, LoanWorkflowStep.LoanSetup, result.Summary);
        }
    }

    private void MarkFailed(LoanCaseRecord record, string reason)
    {
        record.State.Status = LoanCaseStatus.Failed;
        record.State.CurrentStep = LoanWorkflowStep.Failed;
        record.State.FailureReason = reason;
        record.State.PendingApproval = null;
        record.PendingCheckpoint = null;
        AddTimeline(record.State, LoanWorkflowStep.Failed, reason);
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _caseStore.Save(record);
        _logger.LogError(
            "Case {CaseId} execution {ExecutionId} failed: {Reason}",
            record.State.CaseId,
            record.State.ExecutionId,
            reason);
    }

    private static List<ChatMessage> CreateInitialMessages(
        string caseId,
        string executionId,
        IReadOnlyList<LoadedCaseDocument> documents)
    {
        var payload = new
        {
            caseId,
            executionId,
            documents = documents.Select(document => new
            {
                fileName = document.FileName,
                contentType = document.ContentType,
                blobName = document.BlobName,
                content = FormatDocumentContent(document)
            })
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        string prompt =
            """
            Process this loan case using the loaded documents below. Documents were loaded once from Blob Storage at workflow start. Only the document-processing step should consume raw document content. Later steps must use processed outputs only. Each agent step must return JSON with summary, decision, evidence, and optional memoryUpdates. Use executionId for any unique indexing or embedding identity.

            Case payload:
            """ + json;

        return [new ChatMessage(ChatRole.User, prompt)];
    }

    private static string FormatDocumentContent(LoadedCaseDocument document)
    {
        if (document.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return document.Content.ToString();
        }

        return Convert.ToBase64String(document.Content.ToArray());
    }

    private static void AddTimeline(LoanCaseState state, LoanWorkflowStep step, string message) =>
        state.Timeline.Add(new TimelineEntry { Step = step, Message = message });
}
