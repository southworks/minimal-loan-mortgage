using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.Models;
using Xunit;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class StageExplainabilityUiTests
{
    [Fact]
    public void DocumentProcessingPrimaryReason_UsesSummaryWhenPresent()
    {
        var result = new DocumentProcessingResultDto(
            IsComplete: true,
            CompletenessScore: 1m,
            MissingDocuments: [],
            Inconsistencies: [],
            Summary: "Processed 11 raw documents and validated customer references.");

        var reason = StageExplainabilityUi.DocumentProcessingPrimaryReason(result);

        Assert.Equal("Processed 11 raw documents and validated customer references.", reason);
    }

    [Fact]
    public void UnderwritingInputFindings_UsesKeyFactsWhenPresent()
    {
        var result = new UnderwritingResultDto(
            Recommendation: "Deny",
            Reasons: ["credit score: 684", "LTV exceeds threshold."],
            Evidence: ["UW-100"],
            RiskSignals: [],
            Anomalies: [],
            KeyFacts: ["credit score: 684", "LTV: 83.6%"]);

        var inputs = StageExplainabilityUi.UnderwritingInputFindings(result);
        var policies = StageExplainabilityUi.UnderwritingPolicyReferences(result);

        Assert.Equal(2, inputs.Count);
        Assert.Single(policies);
        Assert.Equal("UW-100", policies[0]);
    }
}
