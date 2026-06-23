using AgentGovernance.Audit;
using CohereLoanAndMortgage.Foundry.Governance;
using CohereLoanAndMortgage.Foundry.Governance.Audit;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Governance;

public static class FoundryAgentGovernanceRegistration
{
    public static IServiceCollection AddFoundryAgentGovernance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GovernanceSettings>(configuration.GetSection(GovernanceSettings.SectionName));

        services.AddSingleton<IAgentGovernanceAuditStore>(sp =>
        {
            GovernanceSettings options = sp.GetRequiredService<IOptions<GovernanceSettings>>().Value;
            return new FileAgentGovernanceAuditStore(options.AgentAuditStoreDirectory);
        });

        services.AddSingleton<FoundryAgentGovernanceBootstrap>(sp =>
        {
            GovernanceSettings options = sp.GetRequiredService<IOptions<GovernanceSettings>>().Value;
            IAgentGovernanceAuditStore auditStore = sp.GetRequiredService<IAgentGovernanceAuditStore>();
            var bootstrap = new FoundryAgentGovernanceBootstrap(
                enableFunctionInvocationLogging: options.LogFunctionInvocations,
                loggerFactory: sp.GetRequiredService<ILoggerFactory>());

            foreach (AgentRole role in AgentCatalog.AllRoles)
            {
                var kernel = bootstrap.GetKernel(role);
                kernel.OnAllEvents(evt => AppendAuditEvent(auditStore, evt));
            }

            return bootstrap;
        });

        services.AddSingleton<FoundryAgentGovernanceFactory>();
        services.AddSingleton<FoundryGovernedAgentsFactory>();

        return services;
    }

    private static void AppendAuditEvent(IAgentGovernanceAuditStore auditStore, GovernanceEvent evt)
    {
        GovernanceRunContext? runContext = GovernanceRunContext.CurrentValue;
        auditStore.Append(new AgentGovernanceAuditRecord(
            Seq: 0,
            TimestampUtc: evt.Timestamp,
            AgentId: evt.AgentId,
            Action: evt.Type.ToString(),
            Decision: evt.Data.TryGetValue("decision", out object? decision)
                ? decision?.ToString() ?? string.Empty
                : string.Empty,
            PreviousHash: string.Empty,
            Hash: string.Empty,
            CaseId: runContext?.CaseId,
            ExecutionId: runContext?.ExecutionId,
            EventType: evt.Type.ToString()));
    }
}
