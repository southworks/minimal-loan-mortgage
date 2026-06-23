using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LoanWorkflow.Mcp.Observability;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddCloudFirstOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        string? applicationInsightsConnectionString =
            configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        bool hasAzureMonitorExporter = !string.IsNullOrWhiteSpace(applicationInsightsConnectionString);
        bool useConsoleFallback = !hasAzureMonitorExporter && environment.IsDevelopment();

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName)
                    .AddAttributes(
                    [
                        new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName)
                    ]);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(LoanWorkflowTelemetry.ActivitySourceNames);

                if (hasAzureMonitorExporter)
                {
                    tracing.AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = applicationInsightsConnectionString;
                    });
                }
                else if (useConsoleFallback)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(LoanWorkflowTelemetry.MeterName);

                if (hasAzureMonitorExporter)
                {
                    metrics.AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = applicationInsightsConnectionString;
                    });
                }
                else if (useConsoleFallback)
                {
                    metrics.AddConsoleExporter();
                }
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;

                if (hasAzureMonitorExporter)
                {
                    options.AddAzureMonitorLogExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = applicationInsightsConnectionString;
                    });
                }
                else if (useConsoleFallback)
                {
                    options.AddConsoleExporter();
                }
            });
        });

        return services;
    }

    public static WebApplication UseMcpCorrelationEnrichment(this WebApplication app)
    {
        app.Use(async (httpContext, next) =>
        {
            string caseId = httpContext.Request.Headers["X-Case-Id"].ToString();
            string agentRole = httpContext.Request.Headers["X-Agent-Role"].ToString();
            string agentName = httpContext.Request.Headers["X-Agent-Name"].ToString();

            if (string.IsNullOrWhiteSpace(agentName))
            {
                agentName = ResolveAgentName(agentRole, httpContext.Request.Path.Value);
            }

            if (System.Diagnostics.Activity.Current is { } current)
            {
                if (!string.IsNullOrWhiteSpace(caseId))
                {
                    current.SetTag("case.id", caseId);
                }

                if (!string.IsNullOrWhiteSpace(agentRole))
                {
                    current.SetTag("agent.role", agentRole);
                }

                if (!string.IsNullOrWhiteSpace(agentName))
                {
                    current.SetTag("agent.name", agentName);
                }
            }

            await next().ConfigureAwait(false);
        });

        return app;
    }

    private static string ResolveAgentName(string agentRole, string? path)
    {
        if (!string.IsNullOrWhiteSpace(agentRole))
        {
            return agentRole switch
            {
                "document-processing" => "document-processing-agent",
                "underwriting" => "underwriting-agent",
                "responsible-ai" => "responsible-ai-agent",
                "loan-setup" => "loan-setup-agent",
                _ => agentRole
            };
        }

        string normalizedPath = path ?? string.Empty;

        if (normalizedPath.Contains("/document-retrieval/", StringComparison.OrdinalIgnoreCase))
        {
            return "document-processing-agent";
        }

        if (normalizedPath.Contains("/underwriting-rules/", StringComparison.OrdinalIgnoreCase))
        {
            return "underwriting-agent";
        }

        if (normalizedPath.Contains("/policy-knowledge/", StringComparison.OrdinalIgnoreCase))
        {
            return "responsible-ai-agent";
        }

        if (normalizedPath.Contains("/loan-setup/", StringComparison.OrdinalIgnoreCase))
        {
            return "loan-setup-agent";
        }

        return "unknown-agent";
    }
}
