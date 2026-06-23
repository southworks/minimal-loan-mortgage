using System.Collections.Concurrent;
using System.Reflection;
using Azure.Search.Documents;
using CohereLoanAndMortgage.Foundry.Governance;
using CohereLoanAndMortgage.Foundry.Governance.Mcp;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Governance;
using LoanWorkflow.Mcp.Options;
using LoanWorkflow.Mcp.Startup;
using LoanWorkflow.Mcp.Tools;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoanWorkflowMcpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatasetOptions>(configuration.GetSection(DatasetOptions.SectionName));
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.Configure<AzureFoundryModelsOptions>(configuration.GetSection(AzureFoundryModelsOptions.SectionName));
        services.Configure<McpStartupOptions>(configuration.GetSection(McpStartupOptions.SectionName));

        var searchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();

        if (string.IsNullOrWhiteSpace(searchOptions.Endpoint))
        {
            throw new InvalidOperationException("AzureSearch:Endpoint is required.");
        }

        var foundryOptions = configuration.GetSection(AzureFoundryModelsOptions.SectionName).Get<AzureFoundryModelsOptions>()
            ?? new AzureFoundryModelsOptions();

        services.AddSingleton(SearchClientFactory.CreateIndexClient(searchOptions));

        services.AddHttpClient<FoundryEmbeddingService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .AddFoundryResilience(foundryOptions);
        services.AddHttpClient<FoundryRerankService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .AddFoundryResilience(foundryOptions);

        services.AddSingleton<LocalCaseDataAdapter>();
        services.AddSingleton<PolicyParser>();
        services.AddSingleton<SearchIndexInitializer>();
        services.AddSingleton<EvidenceIndexAdapter>();
        services.AddSingleton<PolicyIndexAdapter>();
        services.AddSingleton<HumanDecisionValidator>();
        services.AddSingleton<AccountSetupBuilder>();
        services.AddSingleton<PolicyIndexSeeder>();
        services.AddSingleton<PolicySeedRunner>();

        services.AddSingleton<DocumentRetrievalTools>();
        services.AddSingleton<UnderwritingRulesTools>();
        services.AddSingleton<PolicyKnowledgeTools>();
        services.AddSingleton<LoanSetupTools>();

        return services;
    }

    public static void PopulateToolDictionary(
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, McpServerTool[]> toolDictionary)
    {
        GovernanceSettings settings = serviceProvider
            .GetRequiredService<IOptions<GovernanceSettings>>()
            .Value;
        McpToolGovernanceCoordinator? coordinator = settings.EnableMcpToolGovernance
            ? serviceProvider.GetRequiredService<McpToolGovernanceCoordinator>()
            : null;
        IHttpContextAccessor? httpContextAccessor = settings.EnableMcpToolGovernance
            ? serviceProvider.GetRequiredService<IHttpContextAccessor>()
            : null;

        toolDictionary["document-retrieval"] = CreateTools(
            serviceProvider.GetRequiredService<DocumentRetrievalTools>(),
            coordinator,
            httpContextAccessor);
        toolDictionary["underwriting-rules"] = CreateTools(
            serviceProvider.GetRequiredService<UnderwritingRulesTools>(),
            coordinator,
            httpContextAccessor);
        toolDictionary["policy-knowledge"] = CreateTools(
            serviceProvider.GetRequiredService<PolicyKnowledgeTools>(),
            coordinator,
            httpContextAccessor);
        toolDictionary["loan-setup"] = CreateTools(
            serviceProvider.GetRequiredService<LoanSetupTools>(),
            coordinator,
            httpContextAccessor);
    }

    private static McpServerTool[] CreateTools<T>(
        T target,
        McpToolGovernanceCoordinator? coordinator,
        IHttpContextAccessor? httpContextAccessor)
    {
        var tools = new List<McpServerTool>();
        var methods = typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        foreach (var method in methods)
        {
            McpServerTool innerTool = McpServerTool.Create(method, target);
            tools.Add(coordinator is not null && httpContextAccessor is not null
                ? new GovernedMcpServerTool(innerTool, coordinator, httpContextAccessor)
                : innerTool);
        }

        return tools.ToArray();
    }
}
