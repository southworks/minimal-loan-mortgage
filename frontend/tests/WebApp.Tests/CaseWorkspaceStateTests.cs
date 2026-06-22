using System.Net;
using System.Text.Json;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Contracts.Backend;
using Cohere.LoanProcessing.WebApp.Services;
using Cohere.LoanProcessing.WebApp.State;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class CaseWorkspaceStateTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task LoadCaseAsync_PopulatesCurrentCaseAndProgress()
    {
        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-001"),
            CreateHealthResponse()
        ]);

        var state = CreateState(handler);
        await state.LoadCaseAsync("APP-001");

        Assert.NotNull(state.CurrentCase);
        Assert.NotNull(state.WorkflowProgress);
        Assert.False(state.IsBusy);
        Assert.Null(state.Error);
        Assert.True(state.CanStartWorkflow);
        Assert.False(state.CanSubmitDecision);
    }

    [Fact]
    public async Task SubmitDecisionAsync_UpdatesCaseAfterApproval()
    {
        var awaitingStatus = CreateWorkflowStatus("exec-001", "AwaitingHumanApproval", underwriting: true);
        var resumedStatus = CreateWorkflowStatus("exec-001", "Completed", underwriting: true, responsibleAi: true, loanSetup: true);

        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-001"),
            CreateHealthResponse(),
            JsonContent.Create(awaitingStatus, options: JsonOptions),
            JsonContent.Create(awaitingStatus, options: JsonOptions),
            JsonContent.Create(awaitingStatus, options: JsonOptions),
            JsonContent.Create(resumedStatus, options: JsonOptions),
            JsonContent.Create(resumedStatus, options: JsonOptions),
            JsonContent.Create(resumedStatus, options: JsonOptions)
        ]);

        var state = CreateState(handler);
        await state.LoadCaseAsync("APP-001");
        await state.StartWorkflowAsync();
        await state.SubmitDecisionAsync(true, "Approved");

        Assert.NotNull(state.CurrentCase?.HumanDecision);
        Assert.True(state.CurrentCase.HumanDecision.Approved);
        Assert.False(state.CanSubmitDecision);
    }

    [Fact]
    public async Task StartWorkflowAsync_StartsPollingUntilWorkflowCompletes()
    {
        var runningStatus = CreateWorkflowStatus("exec-001", "Running", documentProcessing: true);
        var completedStatus = CreateWorkflowStatus(
            "exec-001",
            "Completed",
            documentProcessing: true,
            underwriting: true,
            responsibleAi: true,
            loanSetup: true);

        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-001"),
            CreateHealthResponse(),
            JsonContent.Create(runningStatus, options: JsonOptions),
            JsonContent.Create(runningStatus, options: JsonOptions),
            JsonContent.Create(runningStatus, options: JsonOptions),
            JsonContent.Create(completedStatus, options: JsonOptions),
            JsonContent.Create(completedStatus, options: JsonOptions),
            JsonContent.Create(completedStatus, options: JsonOptions)
        ]);

        var state = CreateState(handler, intervalSeconds: 0);
        await state.LoadCaseAsync("APP-001");
        var executionId = await state.StartWorkflowAsync();

        Assert.Equal("exec-001", executionId);
        Assert.Equal("Completed", state.CurrentCase?.Status);
        Assert.Equal("exec-001", state.ActiveWorkflowRun?.RunId);
        Assert.Equal("Succeeded", state.ActiveWorkflowRun?.Status);
        Assert.False(state.IsPollingWorkflow);
        Assert.NotNull(state.WorkflowProgress);
        Assert.Equal(5, state.WorkflowProgress!.Steps.Count);
        Assert.Equal("Document Review", state.WorkflowProgress.Steps[0].Name);
        Assert.Equal("Complete. Docs verified", state.WorkflowProgress.Steps[0].Summary);
    }

    [Fact]
    public async Task LoadCaseAsync_WithExecutionId_RestoresWorkflowState()
    {
        var awaitingStatus = CreateWorkflowStatus(
            "exec-017",
            "AwaitingHumanApproval",
            documentProcessing: true,
            underwriting: true);

        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-017"),
            JsonContent.Create(awaitingStatus, options: JsonOptions),
            JsonContent.Create(awaitingStatus, options: JsonOptions),
            CreateHealthResponse()
        ]);

        var state = CreateState(handler, intervalSeconds: 0);

        await state.LoadCaseAsync("APP-017", "exec-017");

        Assert.Equal("APP-017", state.CurrentCase?.CaseId);
        Assert.Equal("AwaitingHumanApproval", state.CurrentCase?.Status);
        Assert.Equal("exec-017", state.ActiveWorkflowRun?.RunId);
        Assert.Equal("Succeeded", state.ActiveWorkflowRun?.Status);
        Assert.True(state.CanSubmitDecision);
    }

    private static CaseWorkspaceState CreateState(
        HttpMessageHandler handler,
        int intervalSeconds = 2)
    {
        var catalog = TestSupport.CreateCatalog();
        var sessions = new CaseSessionStore();
        var api = new LoanApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5038/") },
            catalog,
            sessions);

        return new CaseWorkspaceState(
            api,
            Options.Create(new WorkflowPollingOptions
            {
                IntervalSeconds = intervalSeconds,
                MaxDurationMinutes = 1,
                TraceRefreshEveryNTicks = 1
            }));
    }

    private static BasicWorkflowStatusResponse CreateWorkflowStatus(
        string executionId,
        string status,
        bool documentProcessing = false,
        bool underwriting = false,
        bool responsibleAi = false,
        bool loanSetup = false) =>
        new()
        {
            ExecutionId = executionId,
            CaseId = "APP-001",
            Status = status,
            AgentOutputs = new BasicWorkflowAgentOutputsResponse
            {
                DocumentProcessing = documentProcessing
                    ? """{"summary":"Docs verified","decision":"Complete","evidence":"All required documents present."}"""
                    : null,
                Underwriting = underwriting
                    ? """{"summary":"Underwriting complete","decision":"Review","evidence":"Borderline DTI"}"""
                    : null,
                ResponsibleAi = responsibleAi
                    ? """{"summary":"Fairness review passed","decision":"Pass","evidence":"No blocking concerns."}"""
                    : null,
                LoanSetup = loanSetup
                    ? """{"summary":"Account created","decision":"ACCT-DEMO-001","evidence":"Setup completed."}"""
                    : null
            },
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

    private static JsonContent CreateDocumentsResponse(string caseId) =>
        JsonContent.Create(new CaseDocumentsResponse
        {
            CaseId = caseId,
            Documents =
            [
                new CaseDocumentResponse
                {
                    FileName = "loan_application.txt",
                    ContentType = "text/plain",
                    BlobName = $"cases/{caseId}/loan_application.txt",
                    Reference = $"https://example.test/{caseId}/loan_application.txt",
                    LastModifiedUtc = DateTimeOffset.UtcNow
                }
            ]
        }, options: JsonOptions);

    private static JsonContent CreateHealthResponse() =>
        JsonContent.Create(new { status = "ok" }, options: JsonOptions);

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpContent> _responses;

        public SequenceHandler(IEnumerable<HttpContent> responses) =>
            _responses = new Queue<HttpContent>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = _responses.Dequeue()
            });
        }
    }
}
