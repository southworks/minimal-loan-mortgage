using CohereLoanAndMortgage.Api.Host.Services;
using CohereLoanAndMortgage.Api.Host.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

public static class LoanWorkflowConstants
{
    public const string SharedStateScope = "LoanCaseState";

    public const string PendingMessagesKey = "PendingMessages";

    public const string PendingUnderwritingResultKey = "PendingUnderwritingResult";

    public const string ApprovalPortId = "UnderwritingApproval";
}

public sealed class LoanMortgageWorkflowFactory
{
    public AgentWorkflow CreateWorkflow(FoundryAgents agents, string caseId, string executionId)
    {
        RequestPort approvalPort = RequestPort.Create<UnderwritingApprovalPrompt, UnderwritingApprovalDecision>(LoanWorkflowConstants.ApprovalPortId);

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
                AgentStepResult underwritingResult = AgentStructuredOutputParser.Parse("underwriting-agent", underwritingOutput);

                await context.QueueStateUpdateAsync(
                    LoanWorkflowConstants.PendingUnderwritingResultKey,
                    underwritingResult,
                    scopeName: LoanWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

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

                AgentStepResult? underwritingResult = await context
                    .ReadStateAsync<AgentStepResult>(
                        LoanWorkflowConstants.PendingUnderwritingResultKey,
                        scopeName: LoanWorkflowConstants.SharedStateScope,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (pendingMessages is null || pendingMessages.Count == 0 || underwritingResult is null)
                {
                    throw new InvalidOperationException("Underwriting context was not available when processing the approval decision.");
                }

                if (!decision.Approved)
                {
                    await context.YieldOutputAsync(decision, cancellationToken).ConfigureAwait(false);
                    return;
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
            },
            sentMessageTypes: [typeof(IList<ChatMessage>), typeof(List<ChatMessage>)],
            outputTypes: [typeof(UnderwritingApprovalDecision)]);

        var agentHostOptions = new AIAgentHostOptions
        {
            EmitAgentResponseEvents = false
        };

        var documentProcessing = agents.DocumentProcessing.BindAsExecutor(agentHostOptions);
        var underwriting = agents.Underwriting.BindAsExecutor(agentHostOptions);
        var responsibleAi = agents.ResponsibleAi.BindAsExecutor(agentHostOptions);
        var loanSetup = agents.LoanSetup.BindAsExecutor(agentHostOptions);

        return new WorkflowBuilder(documentProcessing)
            .AddEdge(documentProcessing, underwriting)
            .AddEdge(underwriting, requestApprovalExecutor)
            .AddEdge(requestApprovalExecutor, approvalPort)
            .AddEdge(approvalPort, decisionExecutor)
            .AddEdge(decisionExecutor, responsibleAi)
            .AddEdge(responsibleAi, loanSetup)
            .WithOutputFrom(loanSetup)
            .WithName($"loan-mortgage-{executionId}")
            .WithDescription("Loan and mortgage processing workflow with underwriting human approval.")
            .Build();
    }
}
