using Cohere.LoanProcessing.WebApp.Services;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class AgentOutputParserTests
{
    private const string DocumentProcessingFenced = """
        ```json
        {
          "summary": "The provided documents are sufficient and consistent with supporting evidence.",
          "decision": "Complete",
          "evidence": "The employment verification document confirms employment since 2018."
        }
        ```
        """;

    private const string UnderwritingFenced = """
        ```json
        {
          "summary": "The application is borderline due to the elevated loan-to-value ratio and caution-band credit score.",
          "decision": "Reject",
          "evidence": "The applicant has a credit score of 684 and an LTV of 83.6%.",
          "riskLevel": "Medium",
          "policyRefs": ["UW-100", "CL-125", "CR-210"],
          "anomalies": [],
          "keyFacts": ["credit score: 684", "LTV: 83.6%", "annual income: $142,000"]
        }
        ```
        """;

    private const string ResponsibleAiJson = """
        {
          "summary": "The human decision overrides the underwriting rejection, but no rationale is provided for the override.",
          "decision": "Approval Not Supported",
          "evidence": "The applicant has a credit score of 684 and an LTV of 83.6%.",
          "approvalAssessment": "Not Supported",
          "biasRisk": "Potential",
          "policyRefs": ["UW-100", "CL-125", "CR-210"],
          "supportingFacts": [
            "Credit score of 684 is in the caution band.",
            "Loan-to-value ratio of 83.6% exceeds the standard maximum of 80%."
          ],
          "concerns": [
            "The human decision overrides the underwriting rejection without providing a rationale."
          ],
          "recommendations": [
            "Request a detailed explanation for the override decision."
          ]
        }
        """;

    private const string LoanSetupFenced = """
        ```json
        {
          "summary": "The loan setup package is prepared, but additional information is required to proceed.",
          "decision": "Additional Information Required",
          "evidence": "The override rationale is missing, which is necessary for final approval."
        }
        ```
        """;

    [Fact]
    public void ParseDocumentProcessing_PreservesDecisionAndEvidence()
    {
        var result = AgentOutputParser.ParseDocumentProcessing(DocumentProcessingFenced);

        Assert.NotNull(result);
        Assert.True(result!.IsComplete);
        Assert.Equal("Complete", result.Decision);
        Assert.Contains("employment verification", result.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseUnderwriting_MapsRejectDecisionAndStructuredFields()
    {
        var result = AgentOutputParser.ParseUnderwriting(UnderwritingFenced);

        Assert.NotNull(result);
        Assert.Equal("Reject", result!.Decision);
        Assert.Equal("Deny", result.Recommendation);
        Assert.Equal("Medium", result.RiskLevel);
        Assert.Equal(3, result.KeyFacts!.Count);
        Assert.Equal(3, result.PolicyRefs!.Count);
        Assert.Contains("UW-100", result.PolicyRefs);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void ParseResponsibleAi_MarksApprovalNotSupportedAsFailed()
    {
        var result = AgentOutputParser.ParseResponsibleAi(ResponsibleAiJson);

        Assert.NotNull(result);
        Assert.False(result!.Passed);
        Assert.Equal("Approval Not Supported", result.Decision);
        Assert.Equal("Not Supported", result.ApprovalAssessment);
        Assert.Equal("Potential", result.BiasRisk);
        Assert.Equal(2, result.SupportingFacts!.Count);
        Assert.Single(result.Concerns!);
        Assert.Single(result.Recommendations!);
    }

    [Fact]
    public void ParseLoanSetup_MarksAdditionalInformationRequiredWithoutAccountId()
    {
        var result = AgentOutputParser.ParseLoanSetup(LoanSetupFenced);

        Assert.NotNull(result);
        Assert.True(result!.RequiresAdditionalInformation);
        Assert.Equal("ActionRequired", result.Status);
        Assert.Null(result.DemoAccountId);
        Assert.Equal("Additional Information Required", result.Decision);
    }
}
