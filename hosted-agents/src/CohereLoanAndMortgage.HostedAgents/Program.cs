using Azure.AI.Projects;
using Azure.Identity;
using CohereLoanAndMortgage.HostedAgents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;

HostedAgentDefinition agentDefinition = HostedAgentCatalog.GetRequired(
    HostedAgentEnvironment.GetAgentCatalogName());

Uri projectEndpoint = HostedAgentEnvironment.GetProjectEndpoint();
string modelDeploymentName = HostedAgentEnvironment.GetModelDeploymentName();

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: modelDeploymentName,
        instructions: agentDefinition.Instructions,
        name: agentDefinition.Name);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, LocalSessionIsolationKeyProvider>();
builder.Services.AddFoundryResponses(agent);
builder.Services.AddFoundryToolboxes(agentDefinition.ToolboxName);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
