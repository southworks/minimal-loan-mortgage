using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CohereLoanAndMortgage.HostedAgents.Observability;

internal static class HostedAgentTelemetry
{
    public const string ActivitySourceName = "CohereLoanAndMortgage.HostedAgents";
    public const string MeterName = "CohereLoanAndMortgage.HostedAgents";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> RequestDurationMs =
        Meter.CreateHistogram<double>("loan.hosted.request.duration.ms", unit: "ms");
}
