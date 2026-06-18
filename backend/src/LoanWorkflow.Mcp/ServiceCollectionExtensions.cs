using System.Collections.Concurrent;
using System.Reflection;
using Azure.Search.Documents;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Builders;
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
        services.Configure<CohereOptions>(configuration.GetSection(CohereOptions.SectionName));

        var searchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();

        if (string.IsNullOrWhiteSpace(searchOptions.Endpoint))
        {
            throw new InvalidOperationException("AzureSearch:Endpoint is required.");
        }

        services.AddSingleton(SearchClientFactory.CreateIndexClient(searchOptions));

        services.AddHttpClient<CohereEmbeddingService>();
        services.AddHttpClient<CohereRerankService>();

        services.AddSingleton<LocalCaseDataAdapter>();
        services.AddSingleton<PolicyParser>();
        services.AddSingleton<SearchIndexInitializer>();
        services.AddSingleton<EvidenceIndexAdapter>();
        services.AddSingleton<PolicyIndexAdapter>();
        services.AddSingleton<HumanDecisionValidator>();
        services.AddSingleton<AccountSetupBuilder>();
        services.AddSingleton<PolicyIndexSeeder>();

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
        toolDictionary["document-retrieval"] = CreateTools(serviceProvider.GetRequiredService<DocumentRetrievalTools>());
        toolDictionary["underwriting-rules"] = CreateTools(serviceProvider.GetRequiredService<UnderwritingRulesTools>());
        toolDictionary["policy-knowledge"] = CreateTools(serviceProvider.GetRequiredService<PolicyKnowledgeTools>());
        toolDictionary["loan-setup"] = CreateTools(serviceProvider.GetRequiredService<LoanSetupTools>());
    }

    private static McpServerTool[] CreateTools<T>(T target)
    {
        var tools = new List<McpServerTool>();
        var methods = typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        foreach (var method in methods)
        {
            tools.Add(McpServerTool.Create(method, target));
        }

        return tools.ToArray();
    }
}
