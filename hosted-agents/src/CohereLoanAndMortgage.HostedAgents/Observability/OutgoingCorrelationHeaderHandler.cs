using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.HostedAgents.Observability;

internal sealed class HostedAgentCorrelationOptions
{
    public string AgentRole { get; set; } = "unknown";

    public string AgentName { get; set; } = "unknown-agent";
}

internal sealed class OutgoingCorrelationHeaderHandler : DelegatingHandler
{
    private readonly HostedAgentCorrelationOptions _correlationOptions;

    public OutgoingCorrelationHeaderHandler(IOptions<HostedAgentCorrelationOptions> correlationOptions)
    {
        _correlationOptions = correlationOptions.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SetHeaderIfMissing(request.Headers, "traceparent", Activity.Current?.Id);
        SetHeaderIfMissing(request.Headers, "tracestate", Activity.Current?.TraceStateString);

        string? caseId = Activity.Current?.GetBaggageItem("case.id")
            ?? Activity.Current?.GetBaggageItem("workflow.case_id")
            ?? Activity.Current?.GetTagItem("case.id")?.ToString();

        SetHeaderIfMissing(request.Headers, "X-Case-Id", caseId);
        SetHeaderIfMissing(request.Headers, "X-Agent-Role", _correlationOptions.AgentRole);
        SetHeaderIfMissing(request.Headers, "X-Agent-Name", _correlationOptions.AgentName);

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetHeaderIfMissing(HttpRequestHeaders headers, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || headers.Contains(name))
        {
            return;
        }

        headers.TryAddWithoutValidation(name, value);
    }
}

internal sealed class CorrelationHeaderHandlerFilter : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<OutgoingCorrelationHeaderHandler>());
            next(builder);
        };
    }
}
