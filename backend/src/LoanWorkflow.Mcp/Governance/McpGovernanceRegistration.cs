using CohereLoanAndMortgage.Foundry.Governance;
using CohereLoanAndMortgage.Foundry.Governance.Audit;
using CohereLoanAndMortgage.Foundry.Governance.Mcp;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Governance;

public static class McpGovernanceRegistration
{
    public static IServiceCollection AddMcpGovernance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GovernanceSettings>(configuration.GetSection(GovernanceSettings.SectionName));
        services.AddHttpContextAccessor();

        services.AddSingleton<IAgentGovernanceAuditStore>(sp =>
        {
            GovernanceSettings options = sp.GetRequiredService<IOptions<GovernanceSettings>>().Value;
            return new FileAgentGovernanceAuditStore(options.AgentAuditStoreDirectory);
        });

        services.AddSingleton<FoundryAgentGovernanceBootstrap>(sp =>
        {
            GovernanceSettings options = sp.GetRequiredService<IOptions<GovernanceSettings>>().Value;
            return new FoundryAgentGovernanceBootstrap(
                loggerFactory: sp.GetRequiredService<ILoggerFactory>());
        });

        services.AddSingleton<McpToolGovernanceCoordinator>();

        return services;
    }
}
