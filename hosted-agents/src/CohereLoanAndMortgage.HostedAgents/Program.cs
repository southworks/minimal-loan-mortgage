using Azure.AI.Projects;
using Azure.Identity;
using CohereLoanAndMortgage.HostedAgents;
using CohereLoanAndMortgage.HostedAgents.Observability;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using System.Diagnostics;

HostedAgentDefinition agentDefinition = HostedAgentCatalog.GetRequired(
    HostedAgentEnvironment.GetAgentCatalogName());

Uri modelEndpoint = HostedAgentEnvironment.GetModelInferenceEndpoint();
string modelDeploymentName = HostedAgentEnvironment.GetModelDeploymentName();

Console.WriteLine(
    "Starting hosted agent '{0}' with model '{1}' at '{2}'.",
    agentDefinition.Name,
    modelDeploymentName,
    modelEndpoint);

AIAgent agent = new AIProjectClient(modelEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: modelDeploymentName,
        instructions: agentDefinition.Instructions,
        name: agentDefinition.Name);

var builder = AgentHost.CreateBuilder(args);

builder.Services.AddTransient<OutgoingCorrelationHeaderHandler>();
builder.Services.AddSingleton<Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter, CorrelationHeaderHandlerFilter>();
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, LocalSessionIsolationKeyProvider>();

string agentRole = agentDefinition.Name.EndsWith("-agent", StringComparison.OrdinalIgnoreCase)
    ? agentDefinition.Name[..^"-agent".Length]
    : agentDefinition.Name;

builder.Services.Configure<HostedAgentCorrelationOptions>(options =>
{
    options.AgentRole = agentRole;
    options.AgentName = agentDefinition.Name;
});

string environmentName =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

builder.Services.AddHostedAgentOpenTelemetry(
    serviceName: $"cohereloan-hosted-agent-{agentDefinition.Name}",
    environmentName: environmentName);
builder.Services.AddFoundryResponses(agent);
builder.Services.AddFoundryToolboxes(agentDefinition.ToolboxName);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();

app.Run();
