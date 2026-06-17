using Azure.AI.Projects;
using Azure.Identity;
using CohereLoanAndMortgage.Api.Host.Options;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AzureAI;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class FoundryAgents
{
    public required AIAgent DocumentProcessing { get; init; }

    public required AIAgent Underwriting { get; init; }

    public required AIAgent ResponsibleAi { get; init; }

    public required AIAgent LoanSetup { get; init; }
}

public sealed class FoundryAgentProvider
{
    private readonly AzureFoundryOptions _options;
    private readonly ILogger<FoundryAgentProvider> _logger;
    private FoundryAgents? _agents;

    public FoundryAgentProvider(IOptions<AzureFoundryOptions> options, ILogger<FoundryAgentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FoundryAgents> GetAgentsAsync(CancellationToken cancellationToken)
    {
        if (_agents is not null)
        {
            return _agents;
        }

        if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
        {
            throw new InvalidOperationException(
                "Azure Foundry configuration is missing. Set AzureFoundry:ProjectEndpoint in configuration or the AZURE_FOUNDRY_PROJECT_ENDPOINT environment variable.");
        }

        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint), new DefaultAzureCredential());

        _logger.LogInformation("Resolving Azure AI Foundry agents from project endpoint {Endpoint}", _options.ProjectEndpoint);

        AIAgent documentProcessing = await LoadAgentAsync(client, _options.DocumentProcessingAgentName, cancellationToken);
        AIAgent underwriting = await LoadAgentAsync(client, _options.UnderwritingAgentName, cancellationToken);
        AIAgent responsibleAi = await LoadAgentAsync(client, _options.ResponsibleAiAgentName, cancellationToken);
        AIAgent loanSetup = await LoadAgentAsync(client, _options.LoanSetupAgentName, cancellationToken);

        _agents = new FoundryAgents
        {
            DocumentProcessing = documentProcessing,
            Underwriting = underwriting,
            ResponsibleAi = responsibleAi,
            LoanSetup = loanSetup
        };

        return _agents;
    }

    private static async Task<AIAgent> LoadAgentAsync(
        AIProjectClient client,
        string agentName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetAIAgentAsync(agentName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required Azure AI Foundry agent '{agentName}' could not be resolved. Verify the agent exists in the project and that the caller is authenticated.",
                ex);
        }
    }
}
