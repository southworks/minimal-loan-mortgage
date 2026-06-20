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
            Status = BasicWorkflowStatus.Running
        };
        _store.Save(execution);

        List<ChatMessage> input = BuildInput(caseId, executionId, documents);
        RunInBackground(executionId, input);

        return ToResponse(execution);
    }

    public BasicWorkflowStatusResponse GetBasicWorkflowStatus(string executionId) =>
        ToResponse(_store.GetRequired(executionId));

    private void RunInBackground(string executionId, IList<ChatMessage> input)
    {
        CancellationToken stopping = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(async () =>
        {
            BasicWorkflowExecution execution = _store.GetRequired(executionId);

            try
            {
                FoundryAgents agents = await _agentProvider.GetAgentsAsync(stopping).ConfigureAwait(false);
                AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, executionId);
                CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

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

    private async Task RunUntilDoneAsync(
        BasicWorkflowExecution execution,
        StreamingRun run,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RunTimeout);

        await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);

        while (execution.Status == BasicWorkflowStatus.Running)
        {
            bool sawEvent = false;

            using CancellationTokenSource idleCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            idleCts.CancelAfter(IdleTimeout);

            try
            {
                await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(idleCts.Token).ConfigureAwait(false))
                {
                    sawEvent = true;
                    idleCts.CancelAfter(IdleTimeout);
                    HandleEvent(execution, workflowEvent);

                    if (execution.Status is BasicWorkflowStatus.Completed or BasicWorkflowStatus.Failed)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
            {
            }

            if (execution.Status is BasicWorkflowStatus.Completed or BasicWorkflowStatus.Failed)
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
                MarkFailed(execution, message);
                break;

            case WorkflowOutputEvent:
                execution.Status = BasicWorkflowStatus.Completed;
                Touch(execution);
                break;
        }
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
    }

    private BasicWorkflowStatusResponse ToResponse(BasicWorkflowExecution execution)
    {
        _store.Save(execution);

        return new BasicWorkflowStatusResponse
        {
            ExecutionId = execution.ExecutionId,
            CaseId = execution.CaseId,
            Status = execution.Status.ToString(),
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
}
