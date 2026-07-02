using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.Shared.Contracts.Api.Governance;
using Cohere.LoanProcessing.Shared.Contracts.Api.Health;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Services;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.State;

public sealed class CaseWorkspaceState
{
    private readonly LoanApiClient _api;
    private readonly WorkflowPollingOptions _pollingOptions;
    private CancellationTokenSource? _pollCts;

    public CaseWorkspaceState(LoanApiClient api, IOptions<WorkflowPollingOptions> options)
    {
        _api = api;
        _pollingOptions = options.Value;
    }

    public CaseDetailResponse? CurrentCase { get; private set; }
    public WorkflowProgressResponse? WorkflowProgress { get; private set; }
    public WorkflowRunStatusResponse? ActiveWorkflowRun { get; private set; }
    public GovernanceSummaryResponse? GovernanceSummary { get; private set; }
    public ExecutionTraceResponse? ExecutionTrace { get; private set; }
    public ReadinessResponse? Readiness { get; private set; }
    public AgentRuntimeStatusDto? RuntimeStatus { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsPollingWorkflow { get; private set; }
    public string? PollingStatusMessage { get; private set; }
    public string? Error { get; private set; }
    public string? ErrorTraceId { get; private set; }
    public string? ErrorCaseId { get; private set; }
    public DateTimeOffset? LastRefreshUtc { get; private set; }

    public event Action? OnChange;

    public async Task LoadCaseAsync(
        string caseId,
        string? executionId = null,
        CancellationToken cancellationToken = default)
    {
        CancelPolling();
        IsBusy = true;
        ClearError();
        NotifyStateChanged();

        try
        {
            CurrentCase = await _api.GetCaseAsync(caseId, cancellationToken);
            if (CurrentCase is null)
            {
                SetError("Case not found.");
                return;
            }

            ActiveWorkflowRun = !string.IsNullOrWhiteSpace(executionId)
                ? await _api.GetWorkflowRunAsync(caseId, executionId, cancellationToken)
                : await _api.GetLatestWorkflowRunAsync(caseId, cancellationToken);
            WorkflowProgress = await _api.GetWorkflowProgressAsync(caseId, cancellationToken);
            CurrentCase = await _api.GetCaseAsync(caseId, cancellationToken);
            GovernanceSummary = await _api.GetGovernanceAsync(caseId, cancellationToken);
            ExecutionTrace = await _api.GetExecutionTraceAsync(caseId, cancellationToken);
            Readiness = await _api.GetReadinessAsync(cancellationToken);
            RuntimeStatus = Readiness?.Runtime;
            LastRefreshUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }

        if (CurrentCase is not null && ActiveWorkflowRun is not null && IsInProgressRun(ActiveWorkflowRun.Status))
        {
            _ = PollWorkflowRunAsync(caseId, ActiveWorkflowRun.RunId, cancellationToken);
        }
    }

    public async Task<string?> StartWorkflowAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentCase is null) return null;

        CancelPolling();
        IsBusy = true;
        ClearError();
        PollingStatusMessage = null;
        NotifyStateChanged();

        try
        {
            var start = await _api.StartWorkflowAsync(CurrentCase.CaseId, cancellationToken);
            if (start is null)
            {
                SetError("Workflow start returned no response.");
                return null;
            }

            CurrentCase = start.Case ?? CurrentCase;
            return start.RunId;
        }
        catch (Exception ex)
        {
            SetError(ex);
            return null;
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }
    }

    public async Task SubmitDecisionAsync(bool approved, string? notes, CancellationToken cancellationToken = default)
    {
        if (CurrentCase is null) return;

        IsBusy = true;
        ClearError();
        NotifyStateChanged();

        try
        {
            CurrentCase = await _api.SubmitDecisionAsync(CurrentCase.CaseId, approved, notes, cancellationToken);
            if (CurrentCase is not null)
            {
                ActiveWorkflowRun = await _api.GetLatestWorkflowRunAsync(CurrentCase.CaseId, cancellationToken);
                await RefreshCaseDetailsAsync(CurrentCase.CaseId, refreshTrace: true, cancellationToken);
            }

            LastRefreshUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }

        if (CurrentCase is not null && ActiveWorkflowRun is not null && IsInProgressRun(ActiveWorkflowRun.Status))
        {
            await PollWorkflowRunAsync(CurrentCase.CaseId, ActiveWorkflowRun.RunId, cancellationToken);
        }
    }

    public async Task ContinueAccountSetupAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentCase is null) return;

        IsBusy = true;
        ClearError();
        NotifyStateChanged();

        try
        {
            CurrentCase = await _api.ContinueAccountSetupAsync(CurrentCase.CaseId, cancellationToken);
            if (CurrentCase is not null)
            {
                await RefreshCaseDetailsAsync(CurrentCase.CaseId, refreshTrace: true, cancellationToken);
            }

            LastRefreshUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentCase is null) return;
        await LoadCaseAsync(CurrentCase.CaseId, ActiveWorkflowRun?.RunId, cancellationToken);
    }

    public void CancelPolling()
    {
        if (_pollCts is null)
        {
            return;
        }

        _pollCts.Cancel();
        _pollCts.Dispose();
        _pollCts = null;
        IsPollingWorkflow = false;
    }

    public bool CanStartWorkflow =>
        CurrentCase?.AllowedActions.Contains("StartWorkflow") == true && !IsPollingWorkflow;

    public bool CanSubmitDecision =>
        CurrentCase?.AllowedActions.Contains("SubmitDecision") == true;

    public bool CanContinueAccountSetup =>
        CurrentCase?.AllowedActions.Contains("ContinueAccountSetup") == true;

    private async Task PollWorkflowRunAsync(
        string caseId,
        string runId,
        CancellationToken cancellationToken)
    {
        CancelPolling();
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pollToken = _pollCts.Token;

        IsPollingWorkflow = true;
        PollingStatusMessage = null;
        NotifyStateChanged();

        var interval = _pollingOptions.IntervalSeconds <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(_pollingOptions.IntervalSeconds);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _pollingOptions.MaxDurationMinutes));
        var tick = 0;

        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                pollToken.ThrowIfCancellationRequested();

                var status = await _api.GetWorkflowRunAsync(caseId, runId, pollToken);
                if (status is null)
                {
                    return;
                }

                ActiveWorkflowRun = status;
                await RefreshWorkflowSnapshotAsync(caseId, tick, pollToken);
                tick++;
                NotifyStateChanged();

                if (IsTerminalRun(status.Status))
                {
                    await RefreshCaseDetailsAsync(caseId, refreshTrace: true, pollToken);
                    return;
                }

                await Task.Delay(interval, pollToken);
            }

            PollingStatusMessage = "Workflow is still running. Progress updates will resume when you revisit this case.";
            var finalStatus = await _api.GetWorkflowRunAsync(caseId, runId, pollToken);
            if (finalStatus is not null)
            {
                ActiveWorkflowRun = finalStatus;
                await RefreshCaseDetailsAsync(caseId, refreshTrace: true, pollToken);
            }
        }
        catch (OperationCanceledException) when (pollToken.IsCancellationRequested)
        {
        }
        finally
        {
            _pollCts?.Dispose();
            _pollCts = null;
            IsPollingWorkflow = false;
            NotifyStateChanged();
        }
    }

    private async Task RefreshWorkflowSnapshotAsync(string caseId, int tick, CancellationToken cancellationToken)
    {
        CurrentCase = await _api.GetCaseAsync(caseId, cancellationToken);
        WorkflowProgress = await _api.GetWorkflowProgressAsync(caseId, cancellationToken);
        LastRefreshUtc = DateTimeOffset.UtcNow;

        var traceEvery = Math.Max(1, _pollingOptions.TraceRefreshEveryNTicks);
        if (tick % traceEvery == 0)
        {
            GovernanceSummary = await _api.GetGovernanceAsync(caseId, cancellationToken);
            ExecutionTrace = await _api.GetExecutionTraceAsync(caseId, cancellationToken);
        }
    }

    private async Task RefreshCaseDetailsAsync(string caseId, bool refreshTrace, CancellationToken cancellationToken)
    {
        WorkflowProgress = await _api.GetWorkflowProgressAsync(caseId, cancellationToken);
        if (refreshTrace)
        {
            GovernanceSummary = await _api.GetGovernanceAsync(caseId, cancellationToken);
            ExecutionTrace = await _api.GetExecutionTraceAsync(caseId, cancellationToken);
        }

        LastRefreshUtc = DateTimeOffset.UtcNow;
    }

    private static bool IsInProgressRun(string status) =>
        status is "Queued" or "Running";

    private static bool IsTerminalRun(string status) =>
        status is "Succeeded" or "Failed" or "Cancelled";

    private void ClearError()
    {
        Error = null;
        ErrorTraceId = null;
        ErrorCaseId = null;
    }

    private void SetError(string message, string? traceId = null, string? caseId = null)
    {
        Error = message;
        ErrorTraceId = traceId;
        ErrorCaseId = caseId;
    }

    private void SetError(Exception exception)
    {
        if (exception is ApiClientException apiException && apiException.Problem is not null)
        {
            SetError(
                apiException.Problem.DisplayMessage,
                apiException.Problem.TraceId,
                apiException.Problem.CaseId);
            return;
        }

        SetError(exception.Message);
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
