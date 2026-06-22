using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.Shared.Contracts.Api.Governance;
using Cohere.LoanProcessing.WebApp.Models;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class StageTechnicalEvidenceUiTests
{
    [Fact]
    public void Build_MapsUnderwritingGovernanceDecisionsByAgentRole()
    {
        var summary = CreateGovernanceSummary(
            new GovernanceDecisionRecordDto(
                "decision-001",
                "case-001",
                "agent-underwriting",
                "Underwriting",
                "calculate_risk_signals",
                "Allow",
                "underwriting-tools",
                "v1",
                "Tool is allowed for underwriting.",
                DateTimeOffset.UtcNow),
            new GovernanceDecisionRecordDto(
                "decision-002",
                "case-001",
                "agent-document",
                "DocumentProcessing",
                "extract_identity_data",
                "Allow",
                "document-tools",
                "v1",
                null,
                DateTimeOffset.UtcNow));

        var evidence = StageTechnicalEvidenceUi.Build("Underwriting", summary, null);

        Assert.Single(evidence.GovernanceDecisions);
        Assert.Equal("calculate_risk_signals", evidence.GovernanceDecisions[0].ToolName);
        Assert.Equal(1, evidence.AllowedCount);
        Assert.Equal(0, evidence.DeniedCount);
    }

    [Fact]
    public void Build_MapsDocumentToolsFromTraceNames()
    {
        var trace = CreateExecutionTrace(
            tools:
            [
                "extract_identity_data",
                "build_normalized_evidence_bundle",
                "calculate_underwriting_metrics"
            ]);

        var evidence = StageTechnicalEvidenceUi.Build("DocumentProcessing", null, trace);

        Assert.Equal(2, evidence.ToolsAssociated.Count);
        Assert.Contains("extract_identity_data", evidence.ToolsAssociated);
        Assert.Contains("build_normalized_evidence_bundle", evidence.ToolsAssociated);
        Assert.DoesNotContain("calculate_underwriting_metrics", evidence.ToolsAssociated);
    }

    [Fact]
    public void Build_ReturnsEmptyModelWhenNoStageEvidenceMatches()
    {
        var trace = CreateExecutionTrace(
            tools: ["calculate_underwriting_metrics"],
            executedStages: ["Underwriting"],
            executedAgentRoles: ["Underwriting"]);

        var evidence = StageTechnicalEvidenceUi.Build("ResponsibleAiReview", null, trace);

        Assert.False(evidence.HasEvidence);
        Assert.Empty(evidence.ToolsAssociated);
        Assert.Empty(evidence.ExecutedStages);
        Assert.Empty(evidence.ExecutedAgentRoles);
    }

    [Fact]
    public void Build_CalculatesDecisionCounts()
    {
        var summary = CreateGovernanceSummary(
            new GovernanceDecisionRecordDto(
                "decision-allow",
                "case-001",
                "agent-underwriting",
                "Underwriting",
                "get_lending_rules",
                "Allow",
                null,
                "v1",
                null,
                DateTimeOffset.UtcNow),
            new GovernanceDecisionRecordDto(
                "decision-deny",
                "case-001",
                "agent-underwriting",
                "Underwriting",
                "restricted_tool",
                "Deny",
                null,
                "v1",
                null,
                DateTimeOffset.UtcNow),
            new GovernanceDecisionRecordDto(
                "decision-review",
                "case-001",
                "agent-underwriting",
                "Underwriting",
                "sensitive_tool",
                "RequireApproval",
                null,
                "v1",
                null,
                DateTimeOffset.UtcNow));

        var evidence = StageTechnicalEvidenceUi.Build("Underwriting", summary, null);

        Assert.Equal(1, evidence.AllowedCount);
        Assert.Equal(1, evidence.DeniedCount);
        Assert.Equal(1, evidence.RequireApprovalCount);
    }

    private static GovernanceSummaryResponse CreateGovernanceSummary(params GovernanceDecisionRecordDto[] decisions) =>
        new(
            true,
            [],
            new GovernanceEvidenceSummaryDto(0, 0, null, decisions.Length > 0, [], decisions.Length),
            "agt-test",
            new AgtIntegrationSummaryDto(
                "Agent Governance Toolkit",
                "1.0",
                "policy-v1",
                decisions.Count(decision => decision.Decision == "Allow"),
                decisions.Count(decision => decision.Decision == "Deny"),
                decisions.Count(decision => decision.Decision.Contains("Require", StringComparison.OrdinalIgnoreCase)),
                decisions));

    private static ExecutionTraceResponse CreateExecutionTrace(
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? executedStages = null,
        IReadOnlyList<string>? executedAgentRoles = null) =>
        new(
            "case-001",
            true,
            tools?.Count > 0 || executedStages?.Count > 0 || executedAgentRoles?.Count > 0,
            executedStages ?? [],
            executedAgentRoles ?? [],
            tools ?? [],
            tools?.Count ?? 0,
            null,
            [],
            []);
}
