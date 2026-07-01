using System.Diagnostics;

namespace CohereLoanAndMortgage.HostedAgents.Observability;

internal static class HostedAgentTelemetry
{
    public const string ActivitySourceName = "CohereLoanAndMortgage.HostedAgents";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
