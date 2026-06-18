using LoanWorkflow.Mcp.Adapters;

namespace LoanWorkflow.Mcp.Startup;

public sealed class McpStartupInitializer : IHostedService
{
    private readonly SearchIndexInitializer _searchIndexInitializer;
    private readonly PolicyIndexSeeder _policyIndexSeeder;
    private readonly ILogger<McpStartupInitializer> _logger;

    public McpStartupInitializer(
        SearchIndexInitializer searchIndexInitializer,
        PolicyIndexSeeder policyIndexSeeder,
        ILogger<McpStartupInitializer> logger)
    {
        _searchIndexInitializer = searchIndexInitializer;
        _policyIndexSeeder = policyIndexSeeder;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring Azure AI Search indexes exist.");
        await _searchIndexInitializer.EnsureIndexesAsync(cancellationToken);

        _logger.LogInformation("Seeding policy index if needed.");
        await _policyIndexSeeder.SeedIfNeededAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
