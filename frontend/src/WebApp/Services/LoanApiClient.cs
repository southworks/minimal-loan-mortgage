using System.Net.Http.Json;
using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.Shared.Contracts.Api.Governance;
using Cohere.LoanProcessing.Shared.Contracts.Api.Health;
using Cohere.LoanProcessing.WebApp.Contracts.Backend;
using Cohere.LoanProcessing.WebApp.Models;

namespace Cohere.LoanProcessing.WebApp.Services;

public sealed class LoanApiClient(
    HttpClient httpClient,
    DatasetSeedCatalogService catalog,
    CaseSessionStore sessions)
{
    private const string LoanMortgageBase = "api/loan-mortgage";

    public Task<IReadOnlyList<ScenarioSummaryResponse>> GetScenariosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScenarioSummaryResponse>>(
            catalog.GetAllCases().Select(BackendWorkflowMapper.ToScenario).ToList());

    public Task<IReadOnlyList<CaseSummaryResponse>> GetCasesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(sessions.GetSummaries());

    public async Task<CaseDetailResponse?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        CaseSession session = await OpenCaseSessionAsync(caseId, cancellationToken);
        return BackendWorkflowMapper.ToDetail(session);
    }

    public async Task<CaseDetailResponse?> CreateCaseAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        CaseSession session = await OpenCaseSessionAsync(scenarioId, cancellationToken);
        return BackendWorkflowMapper.ToDetail(session);
    }

    public async Task<WorkflowStartResponse?> StartWorkflowAsync(string caseId, CancellationToken cancellationToken = default)
    {
        string normalizedCaseId = ResolveCaseId(caseId);
        CaseSession session = sessions.TryGet(normalizedCaseId)
            ?? await OpenCaseSessionAsync(caseId, cancellationToken);

        using var response = await httpClient.PostAsync(
            $"{LoanMortgageBase}/applications/{Uri.EscapeDataString(normalizedCaseId)}/workflow/basic/start",
            null,
            cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);

        BasicWorkflowStatusResponse? status = await response.Content.ReadFromJsonAsync<BasicWorkflowStatusResponse>(cancellationToken);
        if (status is null)
        {
            return null;
        }

        sessions.ApplyWorkflowStatus(session, status);

        return new WorkflowStartResponse(
            normalizedCaseId,
            status.ExecutionId,
            true,
            MapQueuedStatus(status.Status),
            BackendWorkflowMapper.ToDetail(session));
    }

    public async Task<WorkflowRunStatusResponse?> GetWorkflowRunAsync(
        string caseId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        CaseSession session = await OpenCaseSessionAsync(caseId, cancellationToken);

        BasicWorkflowStatusResponse? status = await GetBasicWorkflowStatusAsync(runId, cancellationToken);
        if (status is null)
        {
            return null;
        }

        sessions.ApplyWorkflowStatus(session, status);
        return BackendWorkflowMapper.ToRunStatus(session);
    }

    public async Task<WorkflowRunStatusResponse?> GetLatestWorkflowRunAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        CaseSession? session = TryGetSession(caseId);
        if (session?.ExecutionId is null)
        {
            return null;
        }

        return await GetWorkflowRunAsync(caseId, session.ExecutionId, cancellationToken);
    }

    public async Task<CaseDetailResponse?> SubmitDecisionAsync(
        string caseId,
        bool approved,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        CaseSession? session = TryGetSession(caseId);
        if (session?.ExecutionId is null)
        {
            throw new InvalidOperationException("No active workflow execution is available for this case.");
        }

        string normalizedCaseId = session.SeedCase.CaseId;
        var request = new BasicWorkflowApprovalRequest
        {
            Approved = approved,
            ReviewerComment = notes
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"{LoanMortgageBase}/applications/{Uri.EscapeDataString(normalizedCaseId)}/workflow/basic/executions/{Uri.EscapeDataString(session.ExecutionId)}/resume",
            request,
            cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        sessions.ApplyHumanDecision(session, approved, notes);

        return BackendWorkflowMapper.ToDetail(session);
    }

    public async Task<CaseDetailResponse?> ContinueAccountSetupAsync(string caseId, CancellationToken cancellationToken = default)
    {
        CaseSession? session = TryGetSession(caseId);
        if (session?.ExecutionId is null)
        {
            return null;
        }

        BasicWorkflowStatusResponse? status = await GetBasicWorkflowStatusAsync(session.ExecutionId, cancellationToken);
        if (status is not null)
        {
            sessions.ApplyWorkflowStatus(session, status);
        }

        return BackendWorkflowMapper.ToDetail(session);
    }

    public async Task<WorkflowProgressResponse?> GetWorkflowProgressAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        CaseSession? session = TryGetSession(caseId);
        if (session is null)
        {
            return null;
        }

        if (session.ExecutionId is not null)
        {
            BasicWorkflowStatusResponse? status = await GetBasicWorkflowStatusAsync(session.ExecutionId, cancellationToken);
            if (status is not null)
            {
                sessions.ApplyWorkflowStatus(session, status);
            }
        }

        return BackendWorkflowMapper.ToProgress(session);
    }

    public Task<GovernanceSummaryResponse?> GetGovernanceAsync(string caseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<GovernanceSummaryResponse?>(null);

    public Task<ExecutionTraceResponse?> GetExecutionTraceAsync(string caseId, CancellationToken cancellationToken = default)
    {
        CaseSession? session = TryGetSession(caseId);
        if (session?.ExecutionId is null)
        {
            return Task.FromResult<ExecutionTraceResponse?>(null);
        }

        WorkflowRunStatusResponse run = BackendWorkflowMapper.ToRunStatus(session);
        return Task.FromResult<ExecutionTraceResponse?>(new ExecutionTraceResponse(
            caseId,
            HasWorkflowSpan: true,
            TraceEvidencePresent: session.DocumentProcessing is not null || session.Underwriting is not null,
            ExecutedStages: BuildExecutedStages(session),
            ExecutedAgentRoles: BuildExecutedAgentRoles(session),
            McpToolsInvoked: [],
            McpToolInvocationCount: 0,
            LatestWorkflowRun: run,
            AgentMemoryEntries: [],
            RetrievalEvents: []));
    }

    public async Task<ReadinessResponse?> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("health", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ReadinessResponse(
                "degraded",
                "loan-mortgage-api",
                [new DependencyHealthDto("api", "Unavailable", $"Status {(int)response.StatusCode}")],
                new AgentRuntimeStatusDto("Foundry", "Foundry", true, "Backend health check failed."),
                DateTimeOffset.UtcNow);
        }

        HealthStatusResponse? health = await response.Content.ReadFromJsonAsync<HealthStatusResponse>(cancellationToken);
        bool isHealthy = string.Equals(health?.Status, "ok", StringComparison.OrdinalIgnoreCase);

        return new ReadinessResponse(
            isHealthy ? "ready" : "degraded",
            "loan-mortgage-api",
            [new DependencyHealthDto("api", isHealthy ? "Healthy" : "Degraded", health?.Status ?? "unknown")],
            new AgentRuntimeStatusDto(
                "Foundry",
                "Foundry",
                !isHealthy,
                isHealthy
                    ? "Backend API is reachable."
                    : "Backend API responded but did not report ready status."),
            DateTimeOffset.UtcNow);
    }

    internal async Task<CaseDocumentsResponse?> TryGetCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"{LoanMortgageBase}/applications/{Uri.EscapeDataString(caseId)}/documents",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CaseDocumentsResponse>(cancellationToken);
    }

    private async Task<CaseSession> OpenCaseSessionAsync(string caseId, CancellationToken cancellationToken)
    {
        SeedCaseDefinition? seedCase = catalog.TryGetCase(caseId)
            ?? throw new InvalidOperationException($"Case '{caseId}' was not found in the dataset seed catalog.");

        CaseSession session = sessions.GetOrCreate(seedCase);

        if (session.Documents.Count == 0)
        {
            CaseDocumentsResponse? documents = await TryGetCaseDocumentsAsync(seedCase.CaseId, cancellationToken);
            if (documents is not null)
            {
                sessions.ApplyDocuments(session, documents);
            }
        }

        return session;
    }

    private string ResolveCaseId(string caseId) =>
        catalog.TryGetCase(caseId.Trim())?.CaseId
        ?? throw new InvalidOperationException($"Case '{caseId}' was not found in the dataset seed catalog.");

    private CaseSession? TryGetSession(string caseId) =>
        sessions.TryGet(ResolveCaseId(caseId));

    private async Task<BasicWorkflowStatusResponse?> GetBasicWorkflowStatusAsync(
        string executionId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"{LoanMortgageBase}/executions/{Uri.EscapeDataString(executionId)}/basic/status",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BasicWorkflowStatusResponse>(cancellationToken);
    }

    private static string MapQueuedStatus(string backendStatus) =>
        backendStatus switch
        {
            "Pending" => "Queued",
            "Running" => "Running",
            _ => backendStatus
        };

    private static IReadOnlyList<string> BuildExecutedStages(CaseSession session)
    {
        var stages = new List<string>();
        if (session.DocumentProcessing is not null)
        {
            stages.Add("DocumentProcessing");
        }

        if (session.Underwriting is not null)
        {
            stages.Add("Underwriting");
        }

        if (session.HumanDecision is not null)
        {
            stages.Add("HumanDecision");
        }

        if (session.ResponsibleAi is not null)
        {
            stages.Add("ResponsibleAiReview");
        }

        if (session.LoanSetup is not null)
        {
            stages.Add("LoanSetup");
        }

        return stages;
    }

    private static IReadOnlyList<string> BuildExecutedAgentRoles(CaseSession session) =>
        BuildExecutedStages(session);
}
