using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

public static class BasicLoanWorkflowConstants
{
    public const string SharedStateScope = "BasicLoanWorkflowState";

    public const string PendingMessagesKey = "PendingMessages";

    public const string PendingUnderwritingResultKey = "PendingUnderwritingResult";

    public const string ApprovalPortId = "BasicHumanApproval";
}

public sealed class LoanMortgageBasicWorkflowFactory
{
    public AgentWorkflow CreateWorkflow(FoundryAgents agents, string caseId, string executionId)
    {
        RequestPort approvalPort = RequestPort.Create<BasicWorkflowApprovalPrompt, BasicWorkflowApprovalDecision>(
            BasicLoanWorkflowConstants.ApprovalPortId);

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
        FunctionExecutor<ChatMessage> requestHumanApproval = CreateHumanApprovalRequestExecutor(
            id: "BasicWorkflowApprovalRequest",
            caseId: caseId,
            executionId: executionId);
        FunctionExecutor<BasicWorkflowApprovalDecision> applyHumanApprovalDecision = CreateHumanApprovalDecisionExecutor(
            id: "BasicWorkflowApprovalDecision",
            caseId: caseId,
            executionId: executionId);
        FunctionExecutor<IList<ChatMessage>> bridge03 = CreatePayloadBridgeExecutor(
            id: "BasicWorkflowBridge03",
            caseId: caseId,
            executionId: executionId,
            sourceAgentName: "responsible-ai-agent");

        return new WorkflowBuilder(documentProcessing)
            .AddEdge(documentProcessing, bridge01)
            .AddEdge(bridge01, underwriting)
            .AddEdge(underwriting, bridge02)
            .AddEdge(bridge02, requestHumanApproval)
            .AddEdge(requestHumanApproval, approvalPort)
            .AddEdge(approvalPort, applyHumanApprovalDecision)
            .AddEdge(applyHumanApprovalDecision, responsibleAi)
            .AddEdge(responsibleAi, bridge03)
            .AddEdge(bridge03, loanSetup)
            .WithOutputFrom(loanSetup)
            .WithName($"loan-mortgage-basic-{executionId}")
            .WithDescription("Basic loan workflow with human approval between underwriting and responsible AI.")
            .Build();
    }

    private static FunctionExecutor<ChatMessage> CreateHumanApprovalRequestExecutor(
        string id,
        string caseId,
        string executionId)
    {
        return new FunctionExecutor<ChatMessage>(
            id: id,
            handlerAsync: async (message, context, cancellationToken) =>
            {
                string rawOutput = WorkflowTextExtractor.FromMessage(message);
                AgentStepResult result = ParseBridgeOutput("underwriting-agent", rawOutput);
                ChatMessage payload = CaseWorkflowPayloadBuilder.CreateAgentTransitionMessage(
                    caseId,
                    executionId,
                    result);

                await context.QueueStateUpdateAsync(
                    BasicLoanWorkflowConstants.PendingUnderwritingResultKey,
                    result,
                    scopeName: BasicLoanWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await context.QueueStateUpdateAsync(
                    BasicLoanWorkflowConstants.PendingMessagesKey,
                    new List<ChatMessage> { payload },
                    scopeName: BasicLoanWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var prompt = new BasicWorkflowApprovalPrompt
                {
                    CaseId = caseId,
                    ExecutionId = executionId,
                    Summary = "Underwriting completed. Approve to continue with responsible AI and loan setup.",
                    UnderwritingOutput = rawOutput
                };

                await context.SendMessageAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(BasicWorkflowApprovalPrompt)]);
    }

    private static FunctionExecutor<BasicWorkflowApprovalDecision> CreateHumanApprovalDecisionExecutor(
        string id,
        string caseId,
        string executionId)
    {
        return new FunctionExecutor<BasicWorkflowApprovalDecision>(
            id: id,
            handlerAsync: async (decision, context, cancellationToken) =>
            {
                if (!decision.Approved)
                {
                    throw new InvalidOperationException("Basic workflow was rejected during human approval.");
                }

                AgentStepResult? underwritingResult = await context
                    .ReadStateAsync<AgentStepResult>(
                        BasicLoanWorkflowConstants.PendingUnderwritingResultKey,
                        scopeName: BasicLoanWorkflowConstants.SharedStateScope,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (underwritingResult is null)
                {
                    throw new InvalidOperationException("Pending underwriting result was not available when resuming the basic workflow.");
                }

                ChatMessage responsibleAiPayload = CaseWorkflowPayloadBuilder.CreateResponsibleAiReviewMessage(
                    caseId,
                    executionId,
                    underwritingResult,
                    decision.Approved,
                    decision.ReviewerComment);

                await context.SendMessageAsync(
                    new List<ChatMessage> { responsibleAiPayload },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(IList<ChatMessage>), typeof(List<ChatMessage>), typeof(TurnToken)]);
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

    private static AgentStepResult ParseBridgeOutput(string sourceAgentName, string rawOutput) =>
        AgentStructuredOutputParser.Parse(sourceAgentName, rawOutput);
}
