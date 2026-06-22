namespace Cohere.LoanProcessing.WebApp.Models;

internal static class EvidenceUiLabels
{
    public static string SourceTypeLabel(string sourceType) =>
        sourceType switch
        {
            "Policy" => "Policy",
            "Rule" => "Lending rule",
            "Document" => "Document",
            "FairnessGuideline" => "Fairness guideline",
            "RiskSignal" => "Risk signal",
            "Anomaly" => "Anomaly",
            "SetupOperation" => "Setup operation",
            _ => sourceType
        };

    public static string MatchStrengthLabel(double relevance) =>
        relevance >= 0.85 ? "Strong match"
        : relevance >= 0.65 ? "Good match"
        : relevance >= 0.45 ? "Moderate match"
        : "Weak match";
}
