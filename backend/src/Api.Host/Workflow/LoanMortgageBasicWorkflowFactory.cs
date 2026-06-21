using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

public sealed class LoanMortgageBasicWorkflowFactory
{
    public AgentWorkflow CreateWorkflow(FoundryAgents agents, string caseId, string executionId)
    {
        var agentHostOptions = new AIAgentHostOptions
        {
            EmitAgentResponseEvents = true,
            ForwardIncomingMessages = false
        };

        var documentProcessing = agents.DocumentProcessing.BindAsExecutor(agentHostOptions);
        var underwriting = agents.Underwriting.BindAsExecutor(agentHostOptions);
        var responsibleAi = agents.ResponsibleAi.BindAsExecutor(agentHostOptions);
        var loanSetup = agents.LoanSetup.BindAsExecutor(agentHostOptions);
        FunctionExecutor<IList<ChatMessage>> bridge01 = CreatePayloadBridgeExecutor(
            id: "BasicWorkflowBridge01",
            caseId: caseId,
            executionId: executionId,
            sourceAgentName: "document-processing-agent");
        FunctionExecutor<IList<ChatMessage>> bridge02 = CreatePayloadBridgeExecutor(
            id: "BasicWorkflowBridge02",
            caseId: caseId,
            executionId: executionId,
            sourceAgentName: "underwriting-agent");
        FunctionExecutor<IList<ChatMessage>> bridge03 = CreatePayloadBridgeExecutor(
            id: "BasicWorkflowBridge03",
            caseId: caseId,
            executionId: executionId,
            sourceAgentName: "responsible-ai-agent");

        return new WorkflowBuilder(documentProcessing)
            .AddEdge(documentProcessing, bridge01)
            .AddEdge(bridge01, underwriting)
            .AddEdge(underwriting, bridge02)
            .AddEdge(bridge02, responsibleAi)
            .AddEdge(responsibleAi, bridge03)
            .AddEdge(bridge03, loanSetup)
            .WithOutputFrom(loanSetup)
            .WithName($"loan-mortgage-basic-{executionId}")
            .WithDescription("Basic loan workflow with 4 agents and no human-in-the-loop.")
            .Build();
    }

    private static FunctionExecutor<IList<ChatMessage>> CreatePayloadBridgeExecutor(
        string id,
        string caseId,
        string executionId,
        string sourceAgentName)
    {
        return new FunctionExecutor<IList<ChatMessage>>(
            id: id,
            handlerAsync: async (messages, context, cancellationToken) =>
            {
                string rawOutput = WorkflowTextExtractor.FromLastAssistantMessage(messages);
                AgentStepResult result = ParseBridgeOutput(sourceAgentName, rawOutput);
                ChatMessage payload = CaseWorkflowPayloadBuilder.CreateAgentTransitionMessage(
                    caseId,
                    executionId,
                    result);

                await context.SendMessageAsync(payload, cancellationToken: cancellationToken).ConfigureAwait(false);

                await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(ChatMessage), typeof(TurnToken)]);
    }

    private static AgentStepResult ParseBridgeOutput(string sourceAgentName, string rawOutput)
    {
        AgentStepResult? parsed = AgentStructuredOutputParser.TryParse(sourceAgentName, rawOutput);
        if (parsed is not null)
        {
            return parsed;
        }

        string? duplicatedFencedPayload = TryExtractDuplicatedFencedJsonPayload(rawOutput);
        if (!string.IsNullOrWhiteSpace(duplicatedFencedPayload))
        {
            parsed = AgentStructuredOutputParser.TryParse(sourceAgentName, duplicatedFencedPayload);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return AgentStructuredOutputParser.Parse(sourceAgentName, rawOutput);
    }

    private static string? TryExtractDuplicatedFencedJsonPayload(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return null;
        }

        string normalized = rawOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        const string jsonFence = "```json\n";
        if (!normalized.StartsWith(jsonFence, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] segments = normalized.Split(jsonFence, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string segment in segments)
        {
            int closingFenceIndex = segment.IndexOf("\n```", StringComparison.Ordinal);
            if (closingFenceIndex <= 0)
            {
                continue;
            }

            string candidate = segment[..closingFenceIndex].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
