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
    public void UnderwritingInputFindings_SeparatesMetricsFromDecisionDrivers()
    {
        var result = new UnderwritingResultDto(
            Recommendation: "Review",
            Reasons:
            [
                "DTI: 28.30% (Policy threshold: 36%)",
                "Case requires manual review because one or more metrics are borderline."
            ],
            Evidence: [],
            RiskSignals: [],
            Anomalies: []);

        var inputs = StageExplainabilityUi.UnderwritingInputFindings(result);
        var decisions = StageExplainabilityUi.UnderwritingDecisionFindings(result);

        Assert.Single(inputs);
        Assert.Single(decisions);
        Assert.Contains("DTI", inputs[0], StringComparison.Ordinal);
        Assert.Contains("manual review", decisions[0], StringComparison.OrdinalIgnoreCase);
    }
}
