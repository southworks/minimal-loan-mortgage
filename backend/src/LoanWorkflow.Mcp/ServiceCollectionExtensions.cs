using System.Collections.Concurrent;
using System.Reflection;
using Azure.Search.Documents;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Options;
using LoanWorkflow.Mcp.Startup;
using LoanWorkflow.Mcp.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoanWorkflowMcpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatasetOptions>(configuration.GetSection(DatasetOptions.SectionName));
        services.AddSingleton<CaseCatalog>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatasetOptions>>().Value;
            var environment = sp.GetRequiredService<IHostEnvironment>();
            string datasetRoot = CasePathResolver.ResolveDatasetRoot(environment.ContentRootPath, options.RootPath);
            return CaseCatalog.Load(datasetRoot, options);
        });
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.Configure<AzureFoundryModelsOptions>(configuration.GetSection(AzureFoundryModelsOptions.SectionName));
        services.Configure<McpStartupOptions>(configuration.GetSection(McpStartupOptions.SectionName));
        services.Configure<DataSourceOptions>(configuration.GetSection(DataSourceOptions.SectionName));

        RegisterCaseDataStore(services, configuration);

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

        services.AddSingleton<CaseDataAdapter>();
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

    private static void RegisterCaseDataStore(IServiceCollection services, IConfiguration configuration)
    {
        var dsOptions = configuration.GetSection(DataSourceOptions.SectionName).Get<DataSourceOptions>()
            ?? new DataSourceOptions();

        if (dsOptions.Mode == DataSourceMode.Fabric
            && !string.IsNullOrWhiteSpace(dsOptions.FabricLakehouse?.WorkspaceName)
            && !string.IsNullOrWhiteSpace(dsOptions.FabricLakehouse?.LakehouseName))
        {
            services.AddSingleton<IFabricLakehouseClient>(sp => FabricLakehouseClient.Create(
                dsOptions,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<FabricLakehouseClient>()));
            services.AddSingleton<ICaseDataStore, FabricCaseDataStore>();
        }
        else
        {
            services.AddSingleton<ICaseDataStore, LocalCaseDataStore>();
        }
    }
}
