using System.ComponentModel;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Observability;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp.Tools;

public sealed class PolicyKnowledgeTools
{
    private readonly PolicyIndexAdapter _policyIndexAdapter;
    private readonly HumanDecisionValidator _humanDecisionValidator;

    public PolicyKnowledgeTools(
        PolicyIndexAdapter policyIndexAdapter,
        HumanDecisionValidator humanDecisionValidator)
    {
        _policyIndexAdapter = policyIndexAdapter;
        _humanDecisionValidator = humanDecisionValidator;
    }

    [McpServerTool]
    [Description("Retrieves relevant policy entries for responsible AI and governance review. The query parameter is required.")]
    public Task<GetRelevantPoliciesResponse> GetRelevantPolicies(
        [Description("Required natural-language search query describing the governance or fairness topic to retrieve.")]
        string query,
        string? caseContext = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));
        return McpToolInstrumentation.ExecuteAsync(
            operationName: "mcp.policy_knowledge.get_relevant_policies",
            caseId: caseContext ?? "n/a",
            executionId: "n/a",
            agentRole: "responsible-ai",
            agentName: "responsible-ai-agent",
            action: () => _policyIndexAdapter.GetRelevantPoliciesAsync(query, caseContext, topK, cancellationToken));
    }

    [McpServerTool]
    [Description("Retrieves policy entries by exact policy reference codes, such as those listed in underwriting policyRefs.")]
    public Task<GetRelevantPoliciesResponse> GetPoliciesByRefs(
        [Description("Policy reference codes to retrieve, for example UW-100 or MR-001.")]
        string[] policyRefs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyRefs);
        return McpToolInstrumentation.ExecuteAsync(
            operationName: "mcp.policy_knowledge.get_policies_by_refs",
            caseId: "n/a",
            executionId: "n/a",
            agentRole: "responsible-ai",
            agentName: "responsible-ai-agent",
            action: () => _policyIndexAdapter.GetPoliciesByRefsAsync(policyRefs, cancellationToken));
    }

    [McpServerTool]
    [Description("Performs structural and consistency checks on the human approval decision.")]
    public Task<ValidateHumanDecisionResponse> ValidateHumanDecision(
        string caseId,
        string executionId,
        string humanDecision,
        string underwritingDecision,
        string? reviewerComment = null,
        CancellationToken cancellationToken = default)
        => McpToolInstrumentation.ExecuteAsync(
            operationName: "mcp.policy_knowledge.validate_human_decision",
            caseId: caseId,
            executionId: executionId,
            agentRole: "responsible-ai",
            agentName: "responsible-ai-agent",
            action: () => _humanDecisionValidator.ValidateAsync(
                caseId,
                executionId,
                humanDecision,
                underwritingDecision,
                reviewerComment,
                _policyIndexAdapter,
                cancellationToken));
}
