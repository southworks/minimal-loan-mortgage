using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Options;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoanWorkflowService
{
    private static readonly TimeSpan WorkflowProcessingTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan WorkflowEventIdleTimeout = TimeSpan.FromSeconds(30);

    private readonly FoundryAgentProvider _agentProvider;
    private readonly LoanMortgageWorkflowFactory _workflowFactory;
    private readonly InMemoryLoanCaseStore _caseStore;
    private readonly BlobDocumentStorageService _documentStorage;
    private readonly DocumentTextExtractionService _documentTextExtractionService;
    private readonly CaseEvidenceIndexingService _caseEvidenceIndexingService;
    private readonly CaseWorkflowOptions _caseWorkflowOptions;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<LoanWorkflowService> _logger;

    public LoanWorkflowService(
        FoundryAgentProvider agentProvider,
        LoanMortgageWorkflowFactory workflowFactory,
        InMemoryLoanCaseStore caseStore,
        BlobDocumentStorageService documentStorage,
        DocumentTextExtractionService documentTextExtractionService,
        CaseEvidenceIndexingService caseEvidenceIndexingService,
        Microsoft.Extensions.Options.IOptions<CaseWorkflowOptions> caseWorkflowOptions,
        IHostApplicationLifetime applicationLifetime,
        ILogger<LoanWorkflowService> logger)
    {
        _agentProvider = agentProvider;
        _workflowFactory = workflowFactory;
        _caseStore = caseStore;
        _documentStorage = documentStorage;
        _documentTextExtractionService = documentTextExtractionService;
        _caseEvidenceIndexingService = caseEvidenceIndexingService;
        _caseWorkflowOptions = caseWorkflowOptions.Value;
        _applicationLifetime = applicationLifetime;
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

        IReadOnlyList<NormalizedCaseDocument> normalizedDocuments = await _documentTextExtractionService
            .ExtractAsync(loadedDocuments, cancellationToken)
            .ConfigureAwait(false);

        if (_caseWorkflowOptions.PreIndexCaseDocuments)
        {
            await _caseEvidenceIndexingService
                .EnsureCaseDocumentsIndexedAsync(caseId, executionId, normalizedDocuments, cancellationToken)
                .ConfigureAwait(false);
        }

        RefreshCaseDocuments(record, loadedDocuments);
        record.State.Status = LoanCaseStatus.Running;
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        AddTimeline(record.State, LoanWorkflowStep.Submitted, $"Workflow started with {loadedDocuments.Count} document(s).");
        record.WorkflowSessionId = executionId;
        record.WorkflowCheckpointManager = CheckpointManager.CreateInMemory();
        _caseStore.Save(record);

        List<ChatMessage> input = CaseWorkflowPayloadBuilder.CreateInitialMessages(caseId, executionId, normalizedDocuments);
        StartWorkflowProcessing(record, input, executionId);

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

        if (record.PendingCheckpoint is null ||
            record.WorkflowCheckpointManager is null ||
            record.PendingExternalRequest is null)
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' does not have a saved workflow checkpoint and cannot continue.");
        }

        var decision = new UnderwritingApprovalDecision
        {
            Approved = request.Approved,
            ReviewerComment = request.ReviewerComment
        };

        ExternalRequest pendingRequest = record.PendingExternalRequest;
        CheckpointInfo pendingCheckpoint = record.PendingCheckpoint;
        CheckpointManager checkpointManager = record.WorkflowCheckpointManager;

        record.UnderwritingDecisionSubmitted = true;
        record.PendingUnderwritingDecision = decision;
        ApplyUnderwritingDecision(record, decision, request);
        _caseStore.Save(record);

        if (!request.Approved)
        {
            await SendRejectionResponseAsync(
                    record,
                    pendingCheckpoint,
                    checkpointManager,
                    pendingRequest,
                    decision,
                    cancellationToken)
                .ConfigureAwait(false);
            ClearWorkflowResumeState(record);
            _caseStore.Save(record);
            return LoanCaseMapper.ToResponse(record.State);
        }

        StartResumeWorkflowProcessing(record, pendingCheckpoint, checkpointManager, pendingRequest, decision);
        return LoanCaseMapper.ToResponse(record.State);
    }

    private static void ApplyUnderwritingDecision(
        LoanCaseRecord record,
        UnderwritingApprovalDecision decision,
        HumanDecisionRequest request)
    {
        _ = decision;

        record.State.PendingApproval = null;
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;

        if (request.Approved)
        {
            AddTimeline(record.State, LoanWorkflowStep.WaitingForHumanApproval, "Underwriting approved by human reviewer.");
            record.State.Status = LoanCaseStatus.Running;
            // Next pipeline step after HITL; Responsible AI start is confirmed via ExecutorInvokedEvent.
            record.State.CurrentStep = LoanWorkflowStep.ResponsibleAi;
            return;
        }

        AddTimeline(
            record.State,
            LoanWorkflowStep.Rejected,
            string.IsNullOrWhiteSpace(request.ReviewerComment)
                ? "Underwriting rejected by human reviewer."
                : $"Underwriting rejected by human reviewer: {request.ReviewerComment}");
        record.State.Status = LoanCaseStatus.Rejected;
        record.State.CurrentStep = LoanWorkflowStep.Rejected;
    }

    private static void ClearWorkflowResumeState(LoanCaseRecord record)
    {
        record.PendingCheckpoint = null;
        record.HaltCheckpoint = null;
        record.WorkflowCheckpointManager = null;
        record.PendingExternalRequest = null;
        record.PendingUnderwritingDecision = null;
        record.UnderwritingDecisionSubmitted = false;
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

    private void StartWorkflowProcessing(
        LoanCaseRecord record,
        IList<ChatMessage> input,
        string executionId)
    {
        CancellationToken applicationStopping = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(
            async () =>
            {
                LoanCaseRecord activeRecord = _caseStore.GetRequired(executionId);

                try
                {
                    FoundryAgents agents = await _agentProvider.GetAgentsAsync(applicationStopping).ConfigureAwait(false);
                    AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
                        agents,
                        activeRecord.State.CaseId,
                        activeRecord.State.ExecutionId);
                    await ProcessRunAsync(
                            activeRecord,
                            workflow,
                            activeRecord.WorkflowCheckpointManager!,
                            input,
                            executionId,
                            applicationStopping)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
                {
                    MarkFailed(activeRecord, "Workflow processing was cancelled because the application is stopping.");
                }
                catch (Exception ex)
                {
                    if (activeRecord.State.Status != LoanCaseStatus.Failed)
                    {
                        MarkFailed(activeRecord, ex.Message);
                    }

                    _logger.LogError(
                        ex,
                        "Background workflow processing failed for execution {ExecutionId}.",
                        executionId);
                }
            },
            CancellationToken.None);
    }

    private void StartResumeWorkflowProcessing(
        LoanCaseRecord record,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision)
    {
        CancellationToken applicationStopping = _applicationLifetime.ApplicationStopping;
        string executionId = record.State.ExecutionId;

        _ = Task.Run(
            async () =>
            {
                LoanCaseRecord activeRecord = _caseStore.GetRequired(executionId);

                try
                {
                    FoundryAgents agents = await _agentProvider.GetAgentsAsync(applicationStopping).ConfigureAwait(false);
                    AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
                        agents,
                        activeRecord.State.CaseId,
                        activeRecord.State.ExecutionId);
                    await ProcessResumeRunAsync(
                            activeRecord,
                            workflow,
                            pendingCheckpoint,
                            checkpointManager,
                            pendingRequest,
                            decision,
                            executionId,
                            applicationStopping)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
                {
                    MarkFailed(activeRecord, "Workflow resume was cancelled because the application is stopping.");
                }
                catch (Exception ex)
                {
                    if (activeRecord.State.Status != LoanCaseStatus.Failed)
                    {
                        MarkFailed(activeRecord, ex.Message);
                    }

                    _logger.LogError(
                        ex,
                        "Background workflow resume failed for execution {ExecutionId}.",
                        executionId);
                }
            },
            CancellationToken.None);
    }

    private async Task SendRejectionResponseAsync(
        LoanCaseRecord record,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision,
        CancellationToken cancellationToken)
    {
        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken).ConfigureAwait(false);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
            agents,
            record.State.CaseId,
            record.State.ExecutionId);

        await using StreamingRun run = await InProcessExecution
            .ResumeStreamingAsync(workflow, pendingCheckpoint, checkpointManager, cancellationToken)
            .ConfigureAwait(false);

        await run.SendResponseAsync(pendingRequest.CreateResponse(decision)).ConfigureAwait(false);
        record.PendingExternalRequest = null;
        _caseStore.Save(record);
    }

    private sealed class ResumeApprovalState
    {
        public required ExternalRequest PendingRequest { get; init; }

        public required UnderwritingApprovalDecision Decision { get; init; }

        public bool ResponseSent { get; set; }

        public bool ContinuationTurnTokenSent { get; set; }
    }

    private async Task ProcessResumeRunAsync(
        LoanCaseRecord record,
        AgentWorkflow workflow,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision,
        string executionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using StreamingRun run = await InProcessExecution
                .ResumeStreamingAsync(workflow, pendingCheckpoint, checkpointManager, cancellationToken)
                .ConfigureAwait(false);

            var resumeApproval = new ResumeApprovalState
            {
                PendingRequest = pendingRequest,
                Decision = decision
            };

            _logger.LogInformation(
                "Resumed workflow after human approval for execution {ExecutionId}. Waiting for re-emitted RequestInfoEvent.",
                executionId);

            await RunWorkflowStreamUntilTerminalAsync(
                    record,
                    run,
                    executionId,
                    cancellationToken,
                    sendInitialTurnToken: false,
                    trackAgentResponse: null,
                    resumeApproval: resumeApproval)
                .ConfigureAwait(false);

            if (!resumeApproval.ResponseSent &&
                record.State.Status is not LoanCaseStatus.Completed
                    and not LoanCaseStatus.Rejected
                    and not LoanCaseStatus.Failed)
            {
                MarkFailed(
                    record,
                    $"Resume for execution '{executionId}' did not receive a re-emitted underwriting approval request.");
            }
        }
        catch (TimeoutException ex)
        {
            MarkFailed(record, ex.Message);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkFailed(record, "Workflow resume was cancelled.");
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkFailed(record, ex.Message);
            throw;
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

            UpdateWorkflowProgress(record, LoanWorkflowStep.DocumentProcessing, "Document processing in progress.");

            _logger.LogInformation(
                "Starting workflow stream for execution {ExecutionId}.",
                sessionId);

            bool receivedAgentResponse = false;

            await RunWorkflowStreamUntilTerminalAsync(
                    record,
                    run,
                    sessionId,
                    cancellationToken,
                    sendInitialTurnToken: true,
                    trackAgentResponse: value => receivedAgentResponse = receivedAgentResponse || value)
                .ConfigureAwait(false);

            if (!receivedAgentResponse &&
                record.State.Status == LoanCaseStatus.Running &&
                !record.UnderwritingDecisionSubmitted)
            {
                const string message =
                    "Workflow completed without any agent responses. Verify Foundry prompt agents are provisioned and that a TurnToken was sent to start processing.";
                MarkFailed(record, message);
                throw new InvalidOperationException(message);
            }
        }
        catch (TimeoutException ex)
        {
            MarkFailed(record, ex.Message);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkFailed(record, "Workflow processing was cancelled.");
            throw;
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
                TryUpdateAgentOutput(record, agentResponseEvent.ExecutorId, agentResponseEvent.Response);
                break;

            case AgentResponseUpdateEvent agentResponseUpdateEvent:
                TryUpdateAgentOutput(record, agentResponseUpdateEvent.ExecutorId, agentResponseUpdateEvent.AsResponse());
                break;

            case ExecutorFailedEvent executorFailedEvent:
                string errorMessage = executorFailedEvent.Data?.Message
                    ?? $"Workflow executor '{executorFailedEvent.ExecutorId}' failed without an exception message.";
                MarkFailed(record, $"Executor '{executorFailedEvent.ExecutorId}' failed: {errorMessage}");
                break;

            case ExecutorInvokedEvent executorInvokedEvent:
                MarkExecutorStarted(record, executorInvokedEvent.ExecutorId);
                break;

            case ExecutorCompletedEvent executorCompletedEvent:
                if (IsAgentExecutor(executorCompletedEvent.ExecutorId) &&
                    executorCompletedEvent.Data is not null)
                {
                    try
                    {
                        UpdateAgentOutputFromExecutorData(
                            record,
                            executorCompletedEvent.ExecutorId,
                            executorCompletedEvent.Data);
                        AdvanceStepAfterExecutorCompleted(record, executorCompletedEvent.ExecutorId);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to parse structured output from completed executor {ExecutorId}. Raw output preview: {RawOutputPreview}",
                            executorCompletedEvent.ExecutorId,
                            TruncateForLog(DescribeExecutorData(executorCompletedEvent.Data)));
                        MarkFailed(record, ex.Message);
                    }
                }
                else
                {
                    TryUpdateAgentOutput(record, executorCompletedEvent.ExecutorId, executorCompletedEvent.Data);
                }

                _logger.LogDebug(
                    "Workflow executor {ExecutorId} completed for execution {ExecutionId} with data type {DataType}.",
                    executorCompletedEvent.ExecutorId,
                    record.State.ExecutionId,
                    executorCompletedEvent.Data?.GetType().FullName ?? "<null>");
                break;

            case SuperStepCompletedEvent superStepCompleted when superStepCompleted.CompletionInfo?.Checkpoint is not null:
                record.PendingCheckpoint = superStepCompleted.CompletionInfo.Checkpoint;
                if (superStepCompleted.CompletionInfo.HasPendingRequests)
                {
                    record.HaltCheckpoint = superStepCompleted.CompletionInfo.Checkpoint;
                }

                break;

            case RequestInfoEvent requestInfoEvent when requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? prompt):
                if (record.UnderwritingDecisionSubmitted)
                {
                    _logger.LogDebug(
                        "Ignoring re-emitted underwriting approval request for execution {ExecutionId} because a human decision was already submitted.",
                        record.State.ExecutionId);
                    break;
                }

                if (record.State.Status == LoanCaseStatus.WaitingForHuman)
                {
                    record.PendingExternalRequest ??= requestInfoEvent.Request;
                    break;
                }

                record.PendingExternalRequest = requestInfoEvent.Request;
                record.UnderwritingDecisionSubmitted = false;
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
                ClearWorkflowResumeState(record);
                AddTimeline(record.State, LoanWorkflowStep.Rejected, "Workflow rejected after human decision.");
                break;

            case WorkflowOutputEvent:
                record.State.Status = LoanCaseStatus.Completed;
                record.State.CurrentStep = LoanWorkflowStep.Completed;
                record.State.PendingApproval = null;
                ClearWorkflowResumeState(record);
                AddTimeline(record.State, LoanWorkflowStep.Completed, "Loan workflow completed.");
                break;
        }

        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _caseStore.Save(record);
    }

    private async Task RunWorkflowStreamUntilTerminalAsync(
        LoanCaseRecord record,
        StreamingRun run,
        string executionId,
        CancellationToken cancellationToken,
        bool sendInitialTurnToken,
        Action<bool>? trackAgentResponse,
        ResumeApprovalState? resumeApproval = null)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WorkflowProcessingTimeout);

        try
        {
            if (sendInitialTurnToken)
            {
                await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
            }

            while (record.State.Status == LoanCaseStatus.Running)
            {
                bool exitAfterBatch = false;

                using CancellationTokenSource streamIdleCts =
                    CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
                streamIdleCts.CancelAfter(WorkflowEventIdleTimeout);

                try
                {
                    await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(streamIdleCts.Token).ConfigureAwait(false))
                    {
                        streamIdleCts.CancelAfter(WorkflowEventIdleTimeout);

                        if (resumeApproval is { ResponseSent: false } &&
                            workflowEvent is RequestInfoEvent requestInfoEvent &&
                            requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? _))
                        {
                            ExternalRequest requestToAnswer = requestInfoEvent.Request.RequestId == resumeApproval.PendingRequest.RequestId
                                ? requestInfoEvent.Request
                                : resumeApproval.PendingRequest;

                            await run.SendResponseAsync(requestToAnswer.CreateResponse(resumeApproval.Decision))
                                .ConfigureAwait(false);
                            record.PendingExternalRequest = null;
                            record.PendingUnderwritingDecision = null;
                            record.State.Status = LoanCaseStatus.Running;
                            record.State.CurrentStep = LoanWorkflowStep.ResponsibleAi;
                            record.State.PendingApproval = null;
                            record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
                            _caseStore.Save(record);
                            resumeApproval.ResponseSent = true;

                            _logger.LogInformation(
                                "Sent saved underwriting decision in response to re-emitted RequestInfoEvent for execution {ExecutionId}.",
                                executionId);
                        }

                    bool agentResponse = workflowEvent is AgentResponseEvent or AgentResponseUpdateEvent ||
                            (workflowEvent is ExecutorCompletedEvent completed &&
                             IsAgentExecutor(completed.ExecutorId) &&
                             completed.Data is not null);

                        trackAgentResponse?.Invoke(agentResponse);

                        _logger.LogDebug(
                            "Workflow event for execution {ExecutionId}: {EventType}",
                            executionId,
                            workflowEvent.GetType().Name);

                        ProcessWorkflowEvent(record, workflowEvent, run);

                        if (record.State.Status == LoanCaseStatus.Failed)
                        {
                            return;
                        }

                        if (record.State.Status is LoanCaseStatus.Completed or LoanCaseStatus.Rejected)
                        {
                            return;
                        }

                        if (record.State.Status == LoanCaseStatus.WaitingForHuman &&
                            resumeApproval is not { ResponseSent: true })
                        {
                            exitAfterBatch = true;
                        }
                    }
                }
                catch (OperationCanceledException) when (streamIdleCts.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
                {
                    _logger.LogDebug(
                        "Workflow stream produced no events for {IdleTimeoutSeconds} seconds for execution {ExecutionId}. Checking run status.",
                        WorkflowEventIdleTimeout.TotalSeconds,
                        executionId);
                }

                if (exitAfterBatch)
                {
                    FinalizeHumanApprovalCheckpoint(record, run, executionId);
                    return;
                }

                if (resumeApproval is { ResponseSent: true, ContinuationTurnTokenSent: false })
                {
                    _logger.LogInformation(
                        "Underwriting decision response was queued for execution {ExecutionId}. Sending TurnToken to continue after HITL.",
                        executionId);

                    await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
                    resumeApproval.ContinuationTurnTokenSent = true;
                    continue;
                }

                if (record.State.Status != LoanCaseStatus.Running)
                {
                    return;
                }

                RunStatus runStatus = await run.GetStatusAsync(timeoutCts.Token).ConfigureAwait(false);
                if (runStatus == RunStatus.Ended)
                {
                    if (record.State.Status is LoanCaseStatus.Completed or LoanCaseStatus.Rejected or LoanCaseStatus.Failed)
                    {
                        return;
                    }

                    MarkFailed(record, "Workflow run ended before reaching a terminal state.");
                    return;
                }

                if (runStatus == RunStatus.Running)
                {
                    _logger.LogDebug(
                        "Workflow is still running for execution {ExecutionId}; continuing to watch for events.",
                        executionId);
                    continue;
                }

                if (runStatus is RunStatus.Idle or RunStatus.PendingRequests)
                {
                    _logger.LogInformation(
                        "Workflow superstep halted for execution {ExecutionId}. RunStatus={RunStatus}. Sending TurnToken to continue.",
                        executionId,
                        runStatus);

                    await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
                    continue;
                }

                _logger.LogInformation(
                    "Workflow superstep halted for execution {ExecutionId}. RunStatus={RunStatus}. Sending TurnToken to continue.",
                    executionId,
                    runStatus);

                await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Workflow processing for execution '{executionId}' exceeded {WorkflowProcessingTimeout.TotalMinutes:0} minutes.");
        }
    }

    private static void EnsureDocumentProcessingCompleted(LoanCaseRecord record)
    {
        if (record.State.Timeline.Any(entry =>
                entry.Step == LoanWorkflowStep.DocumentProcessing &&
                entry.Message.Contains("in progress", StringComparison.OrdinalIgnoreCase)))
        {
            record.State.CurrentStep = LoanWorkflowStep.DocumentProcessing;
            AddTimeline(record.State, LoanWorkflowStep.DocumentProcessing, "Document processing completed.");
            record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    private void UpdateWorkflowProgress(LoanCaseRecord record, LoanWorkflowStep step, string message)
    {
        if (record.State.Status != LoanCaseStatus.Running)
        {
            return;
        }

        record.State.CurrentStep = step;
        AddTimeline(record.State, step, message);
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _caseStore.Save(record);
    }

    private void MarkExecutorStarted(LoanCaseRecord record, string executorId)
    {
        if (!IsAgentExecutor(executorId) || record.State.Status != LoanCaseStatus.Running)
        {
            return;
        }

        string normalizedExecutorId = NormalizeExecutorId(executorId);

        if (normalizedExecutorId.Contains("underwriting", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDocumentProcessingCompleted(record);

            const string message = "Underwriting in progress.";
            if (record.State.CurrentStep == LoanWorkflowStep.Underwriting &&
                record.State.Timeline.LastOrDefault()?.Message == message)
            {
                return;
            }

            UpdateWorkflowProgress(record, LoanWorkflowStep.Underwriting, message);
            return;
        }

        if (normalizedExecutorId.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWorkflowProgress(record, LoanWorkflowStep.ResponsibleAi, "Responsible AI review in progress.");
            return;
        }

        if (normalizedExecutorId.Contains("loan-setup", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWorkflowProgress(record, LoanWorkflowStep.LoanSetup, "Loan setup in progress.");
        }
    }

    private void AdvanceStepAfterExecutorCompleted(LoanCaseRecord record, string executorId)
    {
        if (!IsAgentExecutor(executorId) || record.State.Status != LoanCaseStatus.Running)
        {
            return;
        }

        string normalizedExecutorId = NormalizeExecutorId(executorId);

        if (normalizedExecutorId.Contains("document-processing", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWorkflowProgress(record, LoanWorkflowStep.DocumentProcessing, "Document processing completed.");
            return;
        }

        if (normalizedExecutorId.Contains("underwriting", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWorkflowProgress(
                record,
                LoanWorkflowStep.Underwriting,
                "Underwriting completed. Preparing human approval request.");
            return;
        }

        if (normalizedExecutorId.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWorkflowProgress(record, LoanWorkflowStep.LoanSetup, "Loan setup in progress.");
        }
    }

    private void UpdateAgentOutput(LoanCaseRecord record, string executorId, AgentResponse response)
    {
        string rawOutput = WorkflowTextExtractor.FromAgentResponse(response);
        UpdateAgentOutput(record, executorId, rawOutput);
    }

    private void UpdateAgentOutput(LoanCaseRecord record, string executorId, string rawOutput)
    {
        AgentStepResult result = AgentStructuredOutputParser.Parse(executorId, rawOutput);
        string normalizedExecutorId = NormalizeExecutorId(executorId);

        if (normalizedExecutorId.Contains("document-processing", StringComparison.OrdinalIgnoreCase))
        {
            record.State.DocumentProcessing = result;
            record.State.CurrentStep = LoanWorkflowStep.DocumentProcessing;
            AddTimeline(record.State, LoanWorkflowStep.DocumentProcessing, result.Summary);
        }
        else if (normalizedExecutorId.Contains("underwriting", StringComparison.OrdinalIgnoreCase))
        {
            record.State.Underwriting = result;
            record.State.CurrentStep = LoanWorkflowStep.Underwriting;
            AddTimeline(record.State, LoanWorkflowStep.Underwriting, result.Summary);
        }
        else if (normalizedExecutorId.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase))
        {
            record.State.ResponsibleAi = result;
            record.State.CurrentStep = LoanWorkflowStep.ResponsibleAi;
            AddTimeline(record.State, LoanWorkflowStep.ResponsibleAi, result.Summary);
        }
        else if (normalizedExecutorId.Contains("loan-setup", StringComparison.OrdinalIgnoreCase))
        {
            record.State.LoanSetup = result;
            record.State.CurrentStep = LoanWorkflowStep.LoanSetup;
            AddTimeline(record.State, LoanWorkflowStep.LoanSetup, result.Summary);
        }
    }

    private void UpdateAgentOutputFromExecutorData(LoanCaseRecord record, string executorId, object? data)
    {
        switch (data)
        {
            case AgentResponse agentResponse:
                UpdateAgentOutput(record, executorId, agentResponse);
                break;

            case IList<ChatMessage> messages:
                UpdateAgentOutput(record, executorId, WorkflowTextExtractor.FromChatMessages(messages));
                break;

            case ChatMessage message:
                UpdateAgentOutput(record, executorId, WorkflowTextExtractor.FromChatMessages([message]));
                break;

            case string text:
                UpdateAgentOutput(record, executorId, text);
                break;

            default:
                throw new InvalidOperationException(
                    $"Executor '{executorId}' completed with unsupported data type '{data?.GetType().FullName ?? "null"}'.");
        }
    }

    private void TryUpdateAgentOutput(LoanCaseRecord record, string executorId, object? data)
    {
        if (data is null || !IsAgentExecutor(executorId))
        {
            return;
        }

        try
        {
            UpdateAgentOutputFromExecutorData(record, executorId, data);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogTrace(
                ex,
                "Ignoring non-final or non-structured agent update from executor {ExecutorId}.",
                executorId);
        }
    }

    private static bool IsAgentExecutor(string executorId)
    {
        if (IsWorkflowControlExecutor(executorId))
        {
            return false;
        }

        string normalizedExecutorId = NormalizeExecutorId(executorId);

        return normalizedExecutorId.Contains("document-processing", StringComparison.OrdinalIgnoreCase)
            || normalizedExecutorId.Contains("underwriting", StringComparison.OrdinalIgnoreCase)
            || normalizedExecutorId.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase)
            || normalizedExecutorId.Contains("loan-setup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkflowControlExecutor(string executorId)
    {
        string compactId = executorId.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return compactId.Contains("UnderwritingApproval", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExecutorId(string executorId) =>
        executorId.Replace('_', '-');

    private static string DescribeExecutorData(object? data) =>
        data switch
        {
            AgentResponse agentResponse => WorkflowTextExtractor.FromAgentResponse(agentResponse),
            IList<ChatMessage> messages => WorkflowTextExtractor.FromChatMessages(messages),
            ChatMessage message => WorkflowTextExtractor.FromChatMessages([message]),
            string text => text,
            _ => data?.ToString() ?? string.Empty
        };

    private static string TruncateForLog(string value)
    {
        const int maxLength = 2_000;
        return value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "...");
    }

    private void MarkFailed(LoanCaseRecord record, string reason)
    {
        record.State.Status = LoanCaseStatus.Failed;
        record.State.CurrentStep = LoanWorkflowStep.Failed;
        record.State.FailureReason = reason;
        record.State.PendingApproval = null;
        ClearWorkflowResumeState(record);
        AddTimeline(record.State, LoanWorkflowStep.Failed, reason);
        record.State.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _caseStore.Save(record);
        _logger.LogError(
            "Case {CaseId} execution {ExecutionId} failed: {Reason}",
            record.State.CaseId,
            record.State.ExecutionId,
            reason);
    }

    private static void AddTimeline(LoanCaseState state, LoanWorkflowStep step, string message) =>
        state.Timeline.Add(new TimelineEntry { Step = step, Message = message });

    private void FinalizeHumanApprovalCheckpoint(LoanCaseRecord record, StreamingRun run, string executionId)
    {
        CheckpointInfo? resolvedCheckpoint = record.HaltCheckpoint
            ?? run.LastCheckpoint
            ?? run.Checkpoints.LastOrDefault()
            ?? record.PendingCheckpoint;

        if (resolvedCheckpoint is not null)
        {
            record.PendingCheckpoint = resolvedCheckpoint;
        }

        _logger.LogInformation(
            "Finalized HITL checkpoint for execution {ExecutionId}. CheckpointId={CheckpointId}, UsedHaltCheckpoint={UsedHaltCheckpoint}, CheckpointCount={CheckpointCount}.",
            executionId,
            record.PendingCheckpoint?.CheckpointId ?? "<none>",
            record.HaltCheckpoint is not null,
            run.Checkpoints.Count);

        _caseStore.Save(record);
    }

    private static async Task SendTurnTokenAsync(StreamingRun run, CancellationToken cancellationToken)
    {
        bool sent = await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

        if (!sent)
        {
            throw new InvalidOperationException("Failed to send TurnToken to start workflow agent processing.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
