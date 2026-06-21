using System.ComponentModel;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Models;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp.Tools;

public sealed class UnderwritingRulesTools
{
    private static readonly string[] UnderwritingCategories =
    [
        "identity",
        "income",
        "employment",
        "banking",
        "credit",
        "collateral"
    ];

    private readonly EvidenceIndexAdapter _evidenceIndexAdapter;
    private readonly PolicyIndexAdapter _policyIndexAdapter;

    public UnderwritingRulesTools(
        EvidenceIndexAdapter evidenceIndexAdapter,
        PolicyIndexAdapter policyIndexAdapter)
    {
        _evidenceIndexAdapter = evidenceIndexAdapter;
        _policyIndexAdapter = policyIndexAdapter;
    }

    [McpServerTool]
    [Description("Searches indexed case evidence using Azure AI Search and Azure Foundry rerank.")]
    public Task<SearchCaseEvidenceResponse> SearchCaseEvidence(
        string caseId,
        string executionId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
        => SearchEvidenceAsync(caseId, executionId, query, topK, cancellationToken);

    [McpServerTool]
    [Description("Returns compact grouped evidence for underwriting categories.")]
    public async Task<GetUnderwritingContextResponse> GetUnderwritingContext(
        string caseId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var categories = new List<UnderwritingCategoryContext>();

        foreach (var category in UnderwritingCategories)
        {
            var matches = await _evidenceIndexAdapter.SearchCategoryAsync(
                caseId,
                executionId,
                category,
                $"Summarize {category} evidence for underwriting.",
                topK: 2,
                cancellationToken: cancellationToken);

            categories.Add(new UnderwritingCategoryContext
            {
                Category = category,
                Matches = matches
            });
        }

        return new GetUnderwritingContextResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            Categories = categories
        };
    }

    [McpServerTool]
    [Description("Retrieves the most relevant underwriting policies using Azure AI Search and Azure Foundry rerank.")]
    public Task<GetRelevantPoliciesResponse> GetRelevantPolicies(
        string query,
        string? caseContext = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
        => _policyIndexAdapter.GetRelevantPoliciesAsync(query, caseContext, topK, cancellationToken);

    private async Task<SearchCaseEvidenceResponse> SearchEvidenceAsync(
        string caseId,
        string executionId,
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        var matches = await _evidenceIndexAdapter.SearchAsync(
            caseId,
            executionId,
            query,
            topK,
            cancellationToken: cancellationToken);

        return new SearchCaseEvidenceResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            Query = query,
            Matches = matches
        };
    }
}
