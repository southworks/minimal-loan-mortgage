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
        FunctionExecutor<IList<ChatMessage>> prepareUnderwritingExecutor = new(
            id: "PrepareUnderwritingInput",
            handlerAsync: async (messages, context, cancellationToken) =>
            {
                string documentOutput = WorkflowTextExtractor.ExtractAgentPayload(
                    WorkflowTextExtractor.FromChatMessages(messages));
                var forward = new List<ChatMessage>
                {
                    new(
                        ChatRole.User,
                        "Use the document-processing output below. Return JSON with summary, decision, evidence, and optional memoryUpdates.\n\n" +
                        documentOutput)
                };

                await context.SendMessageAsync(forward, cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(IList<ChatMessage>), typeof(List<ChatMessage>)]);

        RequestPort approvalPort = RequestPort.Create<UnderwritingApprovalPrompt, UnderwritingApprovalDecision>(
            LoanWorkflowConstants.ApprovalPortId);

        FunctionExecutor<IList<ChatMessage>> requestApprovalExecutor = new(
            id: "UnderwritingApprovalRequest",
            handlerAsync: async (messages, context, cancellationToken) =>
            {
                await context.QueueStateUpdateAsync(
                    LoanWorkflowConstants.PendingMessagesKey,
                    messages.ToList(),
                    scopeName: LoanWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                string underwritingOutput = WorkflowTextExtractor.FromChatMessages(messages);
                var prompt = new UnderwritingApprovalPrompt
                {
                    CaseId = caseId,
                    ExecutionId = executionId,
                    Summary = "Underwriting completed. Review the recommendation before continuing to responsible AI and loan setup.",
                    UnderwritingOutput = underwritingOutput
                };

                await context.SendMessageAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(UnderwritingApprovalPrompt)]);

        FunctionExecutor<UnderwritingApprovalDecision> decisionExecutor = new(
            id: "UnderwritingApprovalDecision",
            handlerAsync: async (decision, context, cancellationToken) =>
            {
                IList<ChatMessage>? pendingMessages = await context
                    .ReadStateAsync<IList<ChatMessage>>(
                        LoanWorkflowConstants.PendingMessagesKey,
                        scopeName: LoanWorkflowConstants.SharedStateScope,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (pendingMessages is null || pendingMessages.Count == 0)
                {
                    throw new InvalidOperationException("Underwriting messages were not available when processing the approval decision.");
                }

                if (!decision.Approved)
                {
                    await context.YieldOutputAsync(decision, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await context.SendMessageAsync(pendingMessages, cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(IList<ChatMessage>), typeof(List<ChatMessage>)],
            outputTypes: [typeof(UnderwritingApprovalDecision)]);

        var agentHostOptions = new AIAgentHostOptions
        {
            EmitAgentResponseEvents = true
        };

        var documentProcessing = agents.DocumentProcessing.BindAsExecutor(agentHostOptions);
        var underwriting = agents.Underwriting.BindAsExecutor(agentHostOptions);
        var responsibleAi = agents.ResponsibleAi.BindAsExecutor(agentHostOptions);
        var loanSetup = agents.LoanSetup.BindAsExecutor(agentHostOptions);

        return new WorkflowBuilder(documentProcessing)
            .AddEdge(documentProcessing, prepareUnderwritingExecutor)
            .AddEdge(prepareUnderwritingExecutor, underwriting)
            .AddEdge(underwriting, requestApprovalExecutor)
            .AddEdge(requestApprovalExecutor, approvalPort)
            .AddEdge(approvalPort, decisionExecutor)
            .AddEdge(decisionExecutor, responsibleAi)
            .AddEdge(responsibleAi, loanSetup)
            .WithOutputFrom(loanSetup)
            .WithName($"loan-mortgage-basic-{executionId}")
            .WithDescription("Basic loan workflow with underwriting human approval.")
            .Build();
    }
}
