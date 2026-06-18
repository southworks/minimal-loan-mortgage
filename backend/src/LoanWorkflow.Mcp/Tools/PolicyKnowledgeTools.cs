using System.ComponentModel;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Models;
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
    [Description("Retrieves relevant policy entries for responsible AI and governance review.")]
    public Task<GetRelevantPoliciesResponse> GetRelevantPolicies(
        string query,
        string? caseContext = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
        => _policyIndexAdapter.GetRelevantPoliciesAsync(query, caseContext, topK, cancellationToken);

    [McpServerTool]
    [Description("Performs structural and consistency checks on the human approval decision.")]
    public Task<ValidateHumanDecisionResponse> ValidateHumanDecision(
        string caseId,
        string executionId,
        string humanDecision,
        string underwritingDecision,
        string? reviewerComment = null,
        CancellationToken cancellationToken = default)
        => _humanDecisionValidator.ValidateAsync(
            caseId,
            executionId,
            humanDecision,
            underwritingDecision,
            reviewerComment,
            _policyIndexAdapter,
            cancellationToken);
}
