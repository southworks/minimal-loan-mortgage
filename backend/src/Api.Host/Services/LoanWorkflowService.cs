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

    public LoanCaseResponse CreateCase(CreateLoanApplicationRequest request)
    {
        string caseId = Guid.NewGuid().ToString("N");
        LoanApplicationInput application = LoanCaseMapper.ToApplicationInput(request);

        var state = new LoanCaseState
        {
            CaseId = caseId,
            Application = application,
            Status = LoanCaseStatus.Pending,
            CurrentStep = LoanWorkflowStep.Submitted
        };

        AddTimeline(state, LoanWorkflowStep.Submitted, "Loan application created.");

        var record = new LoanCaseRecord { State = state };
        _caseStore.Save(record);

        return LoanCaseMapper.ToResponse(state);
    }

    public async Task<UploadDocumentsResponse> UploadDocumentsAsync(
        string caseId,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        LoanCaseRecord record = _caseStore.GetRequired(caseId);

        if (record.State.Status != LoanCaseStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' cannot accept documents in status '{record.State.Status}'. Documents may only be uploaded before the workflow starts.");
        }

        if (files.Count == 0)
        {
            throw new InvalidOperationException("At least one document file is required.");
        }

        var uploaded = new List<DocumentReferenceResponse>();

        foreach (IFormFile file in files)
        {
            if (file.Length == 0)
            {
                throw new InvalidOperationException($"File '{file.FileName}' is empty.");
            }

            await using Stream stream = file.OpenReadStream();
            StoredDocumentReference stored = await _documentStorage.UploadAsync(
                caseId,
                file.FileName,
                file.ContentType,
                stream,
                cancellationToken).ConfigureAwait(false);

            var documentInfo = new StoredDocumentInfo
            {
                Reference = stored.Reference,
                FileName = stored.FileName,
                ContentType = stored.ContentType,
                UploadedAtUtc = stored.UploadedAtUtc
            };

            record.State.Documents.Add(documentInfo);
            record.State.Application.DocumentReferences.Add(stored.Reference);
            uploaded.Add(LoanCaseMapper.ToDocumentReference(documentInfo));
        }

        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        AddTimeline(record.State, LoanWorkflowStep.Submitted, $"{uploaded.Count} document(s) uploaded.");
        _caseStore.Save(record);

        return new UploadDocumentsResponse
        {
            CaseId = caseId,
            UploadedDocuments = uploaded,
            AllDocuments = record.State.Documents.Select(LoanCaseMapper.ToDocumentReference).ToArray()
        };
    }

    public async Task<LoanCaseResponse> StartWorkflowAsync(string caseId, CancellationToken cancellationToken)
    {
        LoanCaseRecord record = _caseStore.GetRequired(caseId);

        if (record.State.Status != LoanCaseStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' cannot start workflow in status '{record.State.Status}'.");
        }

        if (record.State.Documents.Count == 0)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' has no uploaded documents. Upload documents before starting the workflow.");
        }

        record.State.Status = LoanCaseStatus.Running;
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        AddTimeline(record.State, LoanWorkflowStep.Submitted, "Workflow started.");
        _caseStore.Save(record);

        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, caseId);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
        List<ChatMessage> input = CreateInitialMessages(record.State);

        await ProcessRunAsync(record, workflow, checkpointManager, input, caseId, cancellationToken)
            .ConfigureAwait(false);

        return LoanCaseMapper.ToResponse(record.State);
    }

    public LoanCaseResponse GetCase(string caseId) =>
        LoanCaseMapper.ToResponse(_caseStore.GetRequired(caseId).State);

    public LoanProgressResponse GetProgress(string caseId) =>
        LoanCaseMapper.ToProgressResponse(_caseStore.GetRequired(caseId).State);

    public async Task<LoanCaseResponse> SubmitDecisionAsync(
        string caseId,
        HumanDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.DecisionType, ApprovalType.Underwriting.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Decision type '{request.DecisionType}' is not supported. Supported types: {ApprovalType.Underwriting}.");
        }

        LoanCaseRecord record = _caseStore.GetRequired(caseId);

        if (record.State.Status != LoanCaseStatus.WaitingForHuman)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' is not waiting for human approval. Current status: {record.State.Status}.");
        }

        if (record.PendingCheckpoint is null)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' does not have a saved workflow checkpoint and cannot continue.");
        }

        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, caseId);
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
                _logger.LogInformation("Case {CaseId} paused for underwriting approval.", record.State.CaseId);
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
        _logger.LogError("Case {CaseId} failed: {Reason}", record.State.CaseId, reason);
    }

    private static List<ChatMessage> CreateInitialMessages(LoanCaseState state)
    {
        var payload = new
        {
            caseId = state.CaseId,
            application = state.Application,
            documentReferences = state.Application.DocumentReferences
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        string prompt =
            """
            Process this loan application using the provided document references. Each agent step must return JSON with summary, decision, evidence, and optional memoryUpdates.

            Case payload:
            """ + json;

        return [new ChatMessage(ChatRole.User, prompt)];
    }

    private static void AddTimeline(LoanCaseState state, LoanWorkflowStep step, string message) =>
        state.Timeline.Add(new TimelineEntry { Step = step, Message = message });
}
