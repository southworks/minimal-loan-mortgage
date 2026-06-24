using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CohereLoanAndMortgage.HostedAgents.Observability;

internal static class HostedAgentOpenTelemetryExtensions
{
    public static IServiceCollection AddHostedAgentOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string environmentName)
    {
        string? applicationInsightsConnectionString =
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        bool hasAzureMonitorExporter = !string.IsNullOrWhiteSpace(applicationInsightsConnectionString);
        bool isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        bool useConsoleFallback = !hasAzureMonitorExporter && isDevelopment;

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName)
                    .AddAttributes(
                    [
                        new KeyValuePair<string, object>("deployment.environment", environmentName)
                    ]);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(HostedAgentTelemetry.ActivitySourceName)
                    .AddSource("Microsoft.Agents")
                    .AddSource("Microsoft.Agents.AI")
                    .AddSource("Microsoft.Agents.AI.Foundry");

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
                    .AddMeter(HostedAgentTelemetry.MeterName);

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
}
