using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using CohereLoanAndMortgage.Api.Host.Options;
using Microsoft.Agents.AI;
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
    private readonly SemaphoreSlim _agentLoadLock = new(1, 1);
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

        await _agentLoadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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

            var credential = new DefaultAzureCredential();
            var projectEndpoint = new Uri(_options.ProjectEndpoint);
            var projectClient = new AIProjectClient(projectEndpoint, credential);
            var agentClient = new AgentAdministrationClient(projectEndpoint, credential);

            _logger.LogInformation("Resolving Azure AI Foundry agents from project endpoint {Endpoint}", _options.ProjectEndpoint);

            AIAgent documentProcessing = await LoadAgentAsync(
                    projectClient,
                    agentClient,
                    _options.DocumentProcessingAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent underwriting = await LoadAgentAsync(
                    projectClient,
                    agentClient,
                    _options.UnderwritingAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent responsibleAi = await LoadAgentAsync(
                    projectClient,
                    agentClient,
                    _options.ResponsibleAiAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent loanSetup = await LoadAgentAsync(
                    projectClient,
                    agentClient,
                    _options.LoanSetupAgentName,
                    cancellationToken)
                .ConfigureAwait(false);

            _agents = new FoundryAgents
            {
                DocumentProcessing = documentProcessing,
                Underwriting = underwriting,
                ResponsibleAi = responsibleAi,
                LoanSetup = loanSetup
            };

            return _agents;
        }
        finally
        {
            _agentLoadLock.Release();
        }
    }

    private async Task<AIAgent> LoadAgentAsync(
        AIProjectClient projectClient,
        AgentAdministrationClient agentClient,
        string agentName,
        CancellationToken cancellationToken)
    {
        try
        {
            ProjectsAgentRecord agentRecord = (await agentClient
                    .GetAgentAsync(agentName, cancellationToken)
                    .ConfigureAwait(false))
                .Value;

            AIAgent agent = projectClient.AsAIAgent(agentRecord);

            _logger.LogInformation(
                "Validated Azure AI Foundry agent {AgentName} and resolved as {AgentType}.",
                agentName,
                agent.GetType().Name);

            return agent;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required Azure AI Foundry agent '{agentName}' could not be resolved. Verify the agent exists in the project and that the caller is authenticated.",
                ex);
        }
    }
}
