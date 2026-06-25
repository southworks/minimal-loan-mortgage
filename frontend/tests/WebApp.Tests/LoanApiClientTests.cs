using System.Net;
using System.Text.Json;
using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.Contracts.Backend;
using Cohere.LoanProcessing.WebApp.Services;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class LoanApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetScenariosAsync_ReturnsDatasetSeedCases()
    {
        var client = CreateClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await client.GetScenariosAsync();

        Assert.NotEmpty(result);
        Assert.Contains(result, scenario => scenario.ScenarioId == "APP-001");
    }

    [Fact]
    public async Task CreateCaseAsync_InitializesCaseFromDatasetSeed()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.Contains("/documents", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new CaseDocumentsResponse
                    {
                        CaseId = "APP-001",
                        Documents =
                        [
                            new CaseDocumentResponse
                            {
                                FileName = "loan_application.txt",
                                ContentType = "text/plain",
                                SourcePath = "APP-001/loan_application.txt",
                                Reference = "https://example.test/loan_application.txt",
                                LastModifiedUtc = DateTimeOffset.UtcNow
                            }
                        ]
                    }, options: JsonOptions)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler);
        var result = await client.CreateCaseAsync("APP-001");

        Assert.NotNull(result);
        Assert.Equal("APP-001", result!.CaseId);
        Assert.Equal("Olivia Bennett", result.Applicant.FullName);
        Assert.Single(result.Documents);
        Assert.Contains("StartWorkflow", result.AllowedActions);
    }

    [Fact]
    public async Task StartWorkflowAsync_PostsToBasicWorkflowEndpoint()
    {
        var status = new BasicWorkflowStatusResponse
        {
            ExecutionId = "exec-001",
            CaseId = "APP-001",
            Status = "Running",
            AgentOutputs = new BasicWorkflowAgentOutputsResponse(),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-001"),
            JsonContent.Create(status, options: JsonOptions)
        ]);

        var client = CreateClient(handler);
        await client.CreateCaseAsync("APP-001");
        var result = await client.StartWorkflowAsync("APP-001");

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Contains("/api/loan-mortgage/applications/APP-001/workflow/basic/start", handler.LastRequest?.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.NotNull(result);
        Assert.Equal("exec-001", result!.RunId);
        Assert.True(result.IsAsync);
    }

    [Fact]
    public async Task SubmitDecisionAsync_PostsResumePayload()
    {
        var startStatus = new BasicWorkflowStatusResponse
        {
            ExecutionId = "exec-001",
            CaseId = "APP-001",
            Status = "AwaitingHumanApproval",
            AgentOutputs = new BasicWorkflowAgentOutputsResponse
            {
                Underwriting = """{"summary":"Review complete","decision":"Review","evidence":"Borderline DTI"}"""
            },
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var resumeResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new DelayedResumeHandler([
            CreateDocumentsResponse("APP-001"),
            JsonContent.Create(startStatus, options: JsonOptions)
        ], resumeResponse.Task);

        var client = CreateClient(handler);
        await client.CreateCaseAsync("APP-001");
        await client.StartWorkflowAsync("APP-001");

        Task<CaseDetailResponse?> submitTask = client.SubmitDecisionAsync("APP-001", true, "Approved in demo UI");
        Task completedTask = await Task.WhenAny(submitTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.NotSame(submitTask, completedTask);
        Assert.False(submitTask.IsCompleted);
        Assert.False(resumeResponse.Task.IsCompleted);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Contains("/resume", handler.LastRequest?.RequestUri?.AbsolutePath, StringComparison.Ordinal);

        resumeResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

        var result = await submitTask;
        Assert.NotNull(result);
        Assert.True(result!.HumanDecision?.Approved);
    }

    [Fact]
    public async Task GetWorkflowProgressAsync_UsesSessionState()
    {
        var status = new BasicWorkflowStatusResponse
        {
            ExecutionId = "exec-001",
            CaseId = "APP-001",
            Status = "Running",
            AgentOutputs = new BasicWorkflowAgentOutputsResponse
            {
                DocumentProcessing = """{"summary":"Docs verified","decision":"Complete","evidence":"All required documents present."}"""
            },
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        var handler = new SequenceHandler([
            CreateDocumentsResponse("APP-001"),
            JsonContent.Create(status, options: JsonOptions),
            JsonContent.Create(status, options: JsonOptions)
        ]);

        var client = CreateClient(handler);
        await client.CreateCaseAsync("APP-001");
        await client.StartWorkflowAsync("APP-001");

        var progress = await client.GetWorkflowProgressAsync("APP-001");

        Assert.NotNull(progress);
        Assert.Equal("APP-001", progress!.CaseId);
        Assert.Equal("InReview", progress.Status);
    }

    private static LoanApiClient CreateClient(HttpMessageHandler handler)
    {
        var catalog = TestSupport.CreateCatalog();
        var sessions = new CaseSessionStore();

        return new LoanApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5038/") },
            catalog,
            sessions);
    }

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
                    SourcePath = $"{caseId}/loan_application.txt",
                    Reference = $"https://example.test/{caseId}/loan_application.txt",
                    LastModifiedUtc = DateTimeOffset.UtcNow
                }
            ]
        }, options: JsonOptions);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpContent> _responses;

        public HttpRequestMessage? LastRequest { get; private set; }

        public SequenceHandler(IEnumerable<HttpContent> responses) =>
            _responses = new Queue<HttpContent>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

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

    private sealed class DelayedResumeHandler : HttpMessageHandler
    {
        private readonly Queue<HttpContent> _responses;
        private readonly Task<HttpResponseMessage> _resumeResponse;

        public HttpRequestMessage? LastRequest { get; private set; }

        public DelayedResumeHandler(IEnumerable<HttpContent> responses, Task<HttpResponseMessage> resumeResponse)
        {
            _responses = new Queue<HttpContent>(responses);
            _resumeResponse = resumeResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (request.RequestUri?.AbsolutePath.EndsWith("/resume", StringComparison.Ordinal) == true)
            {
                return _resumeResponse;
            }

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