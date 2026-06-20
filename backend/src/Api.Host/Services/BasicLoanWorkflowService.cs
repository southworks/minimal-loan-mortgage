using System.Text;
using System.Text.Json;
using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class BasicLoanWorkflowService
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    private const string DocumentProcessingKey = "DocumentProcessing";
    private const string UnderwritingKey = "Underwriting";
    private const string ResponsibleAiKey = "ResponsibleAi";
    private const string LoanSetupKey = "LoanSetup";

    private readonly FoundryAgentProvider _agentProvider;
    private readonly LoanMortgageBasicWorkflowFactory _workflowFactory;
    private readonly InMemoryBasicWorkflowStore _store;
    private readonly BlobDocumentStorageService _documentStorage;
    private readonly CaseEvidenceIndexingService _caseEvidenceIndexingService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<BasicLoanWorkflowService> _logger;

    public BasicLoanWorkflowService(
        FoundryAgentProvider agentProvider,
        LoanMortgageBasicWorkflowFactory workflowFactory,
        InMemoryBasicWorkflowStore store,
        BlobDocumentStorageService documentStorage,
        CaseEvidenceIndexingService caseEvidenceIndexingService,
        IHostApplicationLifetime applicationLifetime,
        ILogger<BasicLoanWorkflowService> logger)
    {
        _agentProvider = agentProvider;
        _workflowFactory = workflowFactory;
        _store = store;
        _documentStorage = documentStorage;
        _caseEvidenceIndexingService = caseEvidenceIndexingService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    public async Task<BasicWorkflowStatusResponse> StartBasicWorkflowAsync(
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

        IReadOnlyList<LoadedCaseDocument> documents =
            await _documentStorage.LoadCaseDocumentsAsync(caseId, cancellationToken).ConfigureAwait(false);

        if (documents.Count == 0)
        {
            throw new KeyNotFoundException(
                $"Case '{caseId}' was not found in Blob Storage or has no documents under prefix '{BlobDocumentStorageService.GetCasePrefix(caseId)}'.");
        }

        await _caseEvidenceIndexingService
            .EnsureBlobDocumentsIndexedAsync(caseId, executionId, documents, cancellationToken)
            .ConfigureAwait(false);

        var execution = new BasicWorkflowExecution
        {
            ExecutionId = executionId,
            CaseId = caseId.Trim(),
            Status = BasicWorkflowStatus.Running,
            WorkflowCheckpointManager = CheckpointManager.CreateInMemory()
        };
        _store.Save(execution);

        List<ChatMessage> input = BuildInput(caseId, executionId, documents);
        RunInBackground(executionId, input);

        return ToResponse(execution);
    }

    public BasicWorkflowStatusResponse GetBasicWorkflowStatus(string executionId) =>
        ToResponse(_store.GetRequired(executionId));

    public async Task<BasicWorkflowStatusResponse> SubmitBasicDecisionAsync(
        string executionId,
        HumanDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.DecisionType, ApprovalType.Underwriting.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Decision type '{request.DecisionType}' is not supported. Supported types: {ApprovalType.Underwriting}.");
        }

        BasicWorkflowExecution execution = _store.GetRequired(executionId);

        if (execution.Status != BasicWorkflowStatus.WaitingForHuman)
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' is not waiting for human approval. Current status: {execution.Status}.");
        }

        if (execution.PendingCheckpoint is null ||
            execution.WorkflowCheckpointManager is null ||
            execution.PendingExternalRequest is null)
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' does not have a saved workflow checkpoint and cannot continue.");
        }

        var decision = new UnderwritingApprovalDecision
        {
            Approved = request.Approved,
            ReviewerComment = request.ReviewerComment
        };

        ExternalRequest pendingRequest = execution.PendingExternalRequest;
        CheckpointInfo pendingCheckpoint = execution.PendingCheckpoint;
        CheckpointManager checkpointManager = execution.WorkflowCheckpointManager;

        execution.UnderwritingDecisionSubmitted = true;
        execution.PendingApproval = null;
        execution.LastUpdatedUtc = DateTimeOffset.UtcNow;

        if (!request.Approved)
        {
            execution.Status = BasicWorkflowStatus.Rejected;
            _store.Save(execution);

            await SendRejectionResponseAsync(
                    execution,
                    pendingCheckpoint,
                    checkpointManager,
                    pendingRequest,
                    decision,
                    cancellationToken)
                .ConfigureAwait(false);

            ClearWorkflowResumeState(execution);
            _store.Save(execution);
            return ToResponse(execution);
        }

        execution.Status = BasicWorkflowStatus.Running;
        _store.Save(execution);
        StartResumeProcessing(execution, pendingCheckpoint, checkpointManager, pendingRequest, decision);

        return ToResponse(execution);
    }

    private void RunInBackground(string executionId, IList<ChatMessage> input)
    {
        CancellationToken stopping = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(async () =>
        {
            BasicWorkflowExecution execution = _store.GetRequired(executionId);

            try
            {
                FoundryAgents agents = await _agentProvider.GetAgentsAsync(stopping).ConfigureAwait(false);
                AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, execution.CaseId, executionId);
                CheckpointManager checkpoints = execution.WorkflowCheckpointManager
                    ?? CheckpointManager.CreateInMemory();
                execution.WorkflowCheckpointManager = checkpoints;

                await using StreamingRun run = await InProcessExecution
                    .RunStreamingAsync(workflow, input, checkpoints, executionId, stopping)
                    .ConfigureAwait(false);

                await RunUntilDoneAsync(execution, run, stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                MarkFailed(execution, "Workflow cancelled because the application is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basic workflow failed for execution {ExecutionId}.", executionId);
                MarkFailed(execution, ex.Message);
            }
        }, CancellationToken.None);
    }

    private void StartResumeProcessing(
        BasicWorkflowExecution execution,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision)
    {
        CancellationToken stopping = _applicationLifetime.ApplicationStopping;
        string executionId = execution.ExecutionId;

        _ = Task.Run(async () =>
        {
            BasicWorkflowExecution activeExecution = _store.GetRequired(executionId);

            try
            {
                FoundryAgents agents = await _agentProvider.GetAgentsAsync(stopping).ConfigureAwait(false);
                AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
                    agents,
                    activeExecution.CaseId,
                    activeExecution.ExecutionId);

                await ProcessResumeRunAsync(
                        activeExecution,
                        workflow,
                        pendingCheckpoint,
                        checkpointManager,
                        pendingRequest,
                        decision,
                        stopping)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                MarkFailed(activeExecution, "Workflow resume was cancelled because the application is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basic workflow resume failed for execution {ExecutionId}.", executionId);
                MarkFailed(activeExecution, ex.Message);
            }
        }, CancellationToken.None);
    }

    private async Task ProcessResumeRunAsync(
        BasicWorkflowExecution execution,
        AgentWorkflow workflow,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision,
        CancellationToken cancellationToken)
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
            "Resumed basic workflow after human approval for execution {ExecutionId}.",
            execution.ExecutionId);

        await RunUntilDoneAsync(execution, run, cancellationToken, resumeApproval).ConfigureAwait(false);

        if (!resumeApproval.ResponseSent &&
            execution.Status is not BasicWorkflowStatus.Completed
                and not BasicWorkflowStatus.Rejected
                and not BasicWorkflowStatus.Failed)
        {
            MarkFailed(
                execution,
                $"Resume for execution '{execution.ExecutionId}' did not receive a re-emitted underwriting approval request.");
        }
    }

    private async Task SendRejectionResponseAsync(
        BasicWorkflowExecution execution,
        CheckpointInfo pendingCheckpoint,
        CheckpointManager checkpointManager,
        ExternalRequest pendingRequest,
        UnderwritingApprovalDecision decision,
        CancellationToken cancellationToken)
    {
        FoundryAgents agents = await _agentProvider.GetAgentsAsync(cancellationToken).ConfigureAwait(false);
        AgentWorkflow workflow = _workflowFactory.CreateWorkflow(
            agents,
            execution.CaseId,
            execution.ExecutionId);

        await using StreamingRun run = await InProcessExecution
            .ResumeStreamingAsync(workflow, pendingCheckpoint, checkpointManager, cancellationToken)
            .ConfigureAwait(false);

        await run.SendResponseAsync(pendingRequest.CreateResponse(decision)).ConfigureAwait(false);
        execution.PendingExternalRequest = null;
        _store.Save(execution);
    }

    private async Task RunUntilDoneAsync(
        BasicWorkflowExecution execution,
        StreamingRun run,
        CancellationToken cancellationToken,
        ResumeApprovalState? resumeApproval = null)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RunTimeout);

        if (resumeApproval is null)
        {
            await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
        }

        while (execution.Status == BasicWorkflowStatus.Running)
        {
            bool sawEvent = false;
            bool exitAfterBatch = false;

            using CancellationTokenSource idleCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            idleCts.CancelAfter(IdleTimeout);

            try
            {
                await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(idleCts.Token).ConfigureAwait(false))
                {
                    sawEvent = true;
                    idleCts.CancelAfter(IdleTimeout);

                    if (resumeApproval is { ResponseSent: false } &&
                        workflowEvent is RequestInfoEvent requestInfoEvent &&
                        requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? _))
                    {
                        ExternalRequest requestToAnswer = requestInfoEvent.Request.RequestId == resumeApproval.PendingRequest.RequestId
                            ? requestInfoEvent.Request
                            : resumeApproval.PendingRequest;

                        await run.SendResponseAsync(requestToAnswer.CreateResponse(resumeApproval.Decision))
                            .ConfigureAwait(false);

                        execution.PendingExternalRequest = null;
                        execution.PendingApproval = null;
                        execution.LastUpdatedUtc = DateTimeOffset.UtcNow;
                        _store.Save(execution);
                        resumeApproval.ResponseSent = true;

                        _logger.LogInformation(
                            "Sent saved underwriting decision for basic workflow execution {ExecutionId}.",
                            execution.ExecutionId);
                    }

                    HandleEvent(execution, workflowEvent);

                    if (execution.Status is BasicWorkflowStatus.Completed or BasicWorkflowStatus.Failed or BasicWorkflowStatus.Rejected)
                    {
                        return;
                    }

                    if (execution.Status == BasicWorkflowStatus.WaitingForHuman &&
                        resumeApproval is not { ResponseSent: true })
                    {
                        exitAfterBatch = true;
                    }
                }
            }
            catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
            {
            }

            if (exitAfterBatch)
            {
                FinalizeHumanApprovalCheckpoint(execution, run);
                return;
            }

            if (resumeApproval is { ResponseSent: true, ContinuationTurnTokenSent: false })
            {
                await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
                resumeApproval.ContinuationTurnTokenSent = true;
                continue;
            }

            if (execution.Status is BasicWorkflowStatus.Completed or BasicWorkflowStatus.Failed or BasicWorkflowStatus.Rejected)
            {
                return;
            }

            RunStatus runStatus = await run.GetStatusAsync(timeoutCts.Token).ConfigureAwait(false);
            if (runStatus == RunStatus.Ended)
            {
                if (execution.Status == BasicWorkflowStatus.Running)
                {
                    execution.Status = BasicWorkflowStatus.Completed;
                    Touch(execution);
                }

                return;
            }

            if (!sawEvent && runStatus is not RunStatus.Running and not RunStatus.Idle and not RunStatus.PendingRequests)
            {
                return;
            }

            await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
        }
    }

    private void HandleEvent(BasicWorkflowExecution execution, WorkflowEvent workflowEvent)
    {
        switch (workflowEvent)
        {
            case AgentResponseUpdateEvent updateEvent:
                SaveAgentText(execution, updateEvent.ExecutorId, WorkflowTextExtractor.FromAgentResponse(updateEvent.AsResponse()));
                break;

            case AgentResponseEvent responseEvent:
                SaveAgentText(execution, responseEvent.ExecutorId, WorkflowTextExtractor.FromAgentResponse(responseEvent.Response));
                break;

            case ExecutorFailedEvent failedEvent:
                string message = failedEvent.Data?.Message
                    ?? $"Executor '{failedEvent.ExecutorId}' failed.";
                if (failedEvent.Data is not null)
                {
                    _logger.LogError(
                        failedEvent.Data,
                        "Basic workflow executor {ExecutorId} failed for execution {ExecutionId}.",
                        failedEvent.ExecutorId,
                        execution.ExecutionId);
                }

                MarkFailed(execution, message);
                break;

            case SuperStepCompletedEvent superStepCompleted when superStepCompleted.CompletionInfo?.Checkpoint is not null:
                execution.PendingCheckpoint = superStepCompleted.CompletionInfo.Checkpoint;
                if (superStepCompleted.CompletionInfo.HasPendingRequests)
                {
                    execution.HaltCheckpoint = superStepCompleted.CompletionInfo.Checkpoint;
                }

                Touch(execution);
                break;

            case RequestInfoEvent requestInfoEvent when requestInfoEvent.Request.TryGetDataAs(out UnderwritingApprovalPrompt? prompt):
                if (execution.UnderwritingDecisionSubmitted)
                {
                    break;
                }

                if (execution.Status == BasicWorkflowStatus.WaitingForHuman)
                {
                    execution.PendingExternalRequest ??= requestInfoEvent.Request;
                    Touch(execution);
                    break;
                }

                execution.PendingExternalRequest = requestInfoEvent.Request;
                execution.UnderwritingDecisionSubmitted = false;
                execution.Status = BasicWorkflowStatus.WaitingForHuman;
                execution.PendingApproval = new PendingApprovalInfo
                {
                    ApprovalType = ApprovalType.Underwriting,
                    Summary = prompt!.Summary,
                    AgentOutput = prompt.UnderwritingOutput,
                    RecommendedAction = "Review the underwriting output and approve or reject to continue."
                };

                _logger.LogInformation(
                    "Basic workflow execution {ExecutionId} paused for underwriting approval.",
                    execution.ExecutionId);
                Touch(execution);
                break;

            case WorkflowOutputEvent outputEvent when outputEvent.Data is UnderwritingApprovalDecision decision && !decision.Approved:
                execution.Status = BasicWorkflowStatus.Rejected;
                execution.PendingApproval = null;
                ClearWorkflowResumeState(execution);
                Touch(execution);
                break;

            case WorkflowOutputEvent:
                execution.Status = BasicWorkflowStatus.Completed;
                execution.PendingApproval = null;
                ClearWorkflowResumeState(execution);
                Touch(execution);
                break;
        }
    }

    private void FinalizeHumanApprovalCheckpoint(BasicWorkflowExecution execution, StreamingRun run)
    {
        CheckpointInfo? resolvedCheckpoint = execution.HaltCheckpoint
            ?? run.LastCheckpoint
            ?? run.Checkpoints.LastOrDefault()
            ?? execution.PendingCheckpoint;

        if (resolvedCheckpoint is not null)
        {
            execution.PendingCheckpoint = resolvedCheckpoint;
        }

        _logger.LogInformation(
            "Finalized HITL checkpoint for basic workflow execution {ExecutionId}. CheckpointId={CheckpointId}.",
            execution.ExecutionId,
            execution.PendingCheckpoint?.CheckpointId ?? "<none>");

        _store.Save(execution);
    }

    private static void ClearWorkflowResumeState(BasicWorkflowExecution execution)
    {
        execution.PendingCheckpoint = null;
        execution.HaltCheckpoint = null;
        execution.WorkflowCheckpointManager = null;
        execution.PendingExternalRequest = null;
        execution.UnderwritingDecisionSubmitted = false;
    }

    private static void SaveAgentText(BasicWorkflowExecution execution, string executorId, string text)
    {
        string? agentKey = MapExecutorToAgentKey(executorId);
        if (agentKey is null)
        {
            return;
        }

        string chunk = ExtractAssistantChunk(text);
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return;
        }

        StringBuilder buffer = execution.StreamingBuffers.TryGetValue(agentKey, out StringBuilder? existing)
            ? existing
            : execution.StreamingBuffers[agentKey] = new StringBuilder();

        string accumulated = buffer.ToString();

        if (chunk.Contains('{') && chunk.Contains('}') && chunk.Length >= accumulated.Length)
        {
            execution.AgentOutputs[agentKey] = chunk;
            Touch(execution);
            return;
        }

        if (accumulated.EndsWith(chunk, StringComparison.Ordinal))
        {
            return;
        }

        if (chunk.StartsWith(accumulated, StringComparison.Ordinal) && chunk.Length > accumulated.Length)
        {
            buffer.Clear();
            buffer.Append(chunk);
        }
        else
        {
            buffer.Append(chunk);
        }

        execution.AgentOutputs[agentKey] = buffer.ToString().Trim();
        Touch(execution);
    }

    private static string ExtractAssistantChunk(string text)
    {
        string trimmed = text.Trim();
        const string prefix = "[assistant]";

        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[prefix.Length..].TrimStart();
        }

        return trimmed;
    }

    private static string? MapExecutorToAgentKey(string executorId)
    {
        string id = executorId.Replace("_", "-");

        if (id.Contains("UnderwritingApproval", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (id.Contains("document-processing", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentProcessingKey;
        }

        if (id.Contains("underwriting", StringComparison.OrdinalIgnoreCase))
        {
            return UnderwritingKey;
        }

        if (id.Contains("responsible-ai", StringComparison.OrdinalIgnoreCase))
        {
            return ResponsibleAiKey;
        }

        if (id.Contains("loan-setup", StringComparison.OrdinalIgnoreCase))
        {
            return LoanSetupKey;
        }

        return null;
    }

    private void MarkFailed(BasicWorkflowExecution execution, string reason)
    {
        execution.Status = BasicWorkflowStatus.Failed;
        execution.FailureReason = reason;
        Touch(execution);
    }

    private static void Touch(BasicWorkflowExecution execution)
    {
        execution.LastUpdatedUtc = DateTimeOffset.UtcNow;
        // Caller persists via ToResponse or explicit Save.
    }

    private BasicWorkflowStatusResponse ToResponse(BasicWorkflowExecution execution)
    {
        _store.Save(execution);

        return new BasicWorkflowStatusResponse
        {
            ExecutionId = execution.ExecutionId,
            CaseId = execution.CaseId,
            Status = execution.Status.ToString(),
            PendingApproval = execution.PendingApproval is null
                ? null
                : new PendingApprovalResponse
                {
                    ApprovalType = execution.PendingApproval.ApprovalType.ToString(),
                    Summary = execution.PendingApproval.Summary,
                    AgentOutput = execution.PendingApproval.AgentOutput,
                    RecommendedAction = execution.PendingApproval.RecommendedAction
                },
            AgentOutputs = new BasicWorkflowAgentOutputsResponse
            {
                DocumentProcessing = GetOutput(execution, DocumentProcessingKey),
                Underwriting = GetOutput(execution, UnderwritingKey),
                ResponsibleAi = GetOutput(execution, ResponsibleAiKey),
                LoanSetup = GetOutput(execution, LoanSetupKey)
            },
            FailureReason = execution.FailureReason,
            LastUpdatedUtc = execution.LastUpdatedUtc
        };
    }

    private static string? GetOutput(BasicWorkflowExecution execution, string key) =>
        execution.AgentOutputs.TryGetValue(key, out string? value) ? value : null;

    private static List<ChatMessage> BuildInput(
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
                content = document.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                    ? document.Content.ToString()
                    : Convert.ToBase64String(document.Content.ToArray())
            })
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        string prompt =
            """
            Process this loan case using the loaded documents below. Each agent step must return JSON with summary, decision, evidence, and optional memoryUpdates.

            Case payload:
            """ + json;

        return [new ChatMessage(ChatRole.User, prompt)];
    }

    private static async Task SendTurnTokenAsync(StreamingRun run, CancellationToken cancellationToken)
    {
        if (!await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Failed to send TurnToken to the workflow.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class ResumeApprovalState
    {
        public required ExternalRequest PendingRequest { get; init; }

        public required UnderwritingApprovalDecision Decision { get; init; }

        public bool ResponseSent { get; set; }

        public bool ContinuationTurnTokenSent { get; set; }
    }
}
