using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

public sealed class LoanMortgageBasicWorkflowFactory
{
    public AgentWorkflow CreateWorkflow(FoundryAgents agents, string executionId)
    {
        var agentHostOptions = new AIAgentHostOptions
        {
            EmitAgentResponseEvents = true
        };

        var documentProcessing = agents.DocumentProcessing.BindAsExecutor(agentHostOptions);
        var underwriting = agents.Underwriting.BindAsExecutor(agentHostOptions);
        var responsibleAi = agents.ResponsibleAi.BindAsExecutor(agentHostOptions);
        var loanSetup = agents.LoanSetup.BindAsExecutor(agentHostOptions);

        return new WorkflowBuilder(documentProcessing)
            .AddEdge(documentProcessing, underwriting)
            .AddEdge(underwriting, responsibleAi)
            .AddEdge(responsibleAi, loanSetup)
            .WithOutputFrom(loanSetup)
            .WithName($"loan-mortgage-basic-{executionId}")
            .WithDescription("Basic loan workflow with 4 agents and no human-in-the-loop.")
            .Build();
    }
}
