namespace Cohere.LoanProcessing.WebApp.Models;

public static class WorkflowStageUi
{
    public static string ToBusinessStatusLabel(string status) => status switch
    {
        "AwaitingHumanApproval" => "Human review required",
        "Approved" => "Approved",
        "Completed" => "Completed",
        "Rejected" => "Rejected",
        "Failed" => "Failed",
        "InReview" => "Running",
        "Created" => "Pending",
        _ => status
    };

    public static string ToBusinessStageName(string stageKey, string? fallbackName = null) => stageKey switch
    {
        "None" => "Document Review",
        "Created" => "Document Review",
        "DocumentProcessing" => "Document Review",
        "Underwriting" => "Financial Assessment",
        "HumanDecision" => "Human Review",
        "Human Approval" => "Human Review",
        "ResponsibleAiReview" => "Fairness Review",
        "LoanSetup" => "Account Setup",
        "Completed" => "Completed",
        _ => fallbackName ?? stageKey
    };

    public static string ToCssClass(string executionStatus) => executionStatus switch
    {
        "Succeeded" => "stage-succeeded",
        "InProgress" => "stage-in-progress",
        "Failed" => "stage-failed",
        "Blocked" => "stage-blocked",
        "Skipped" => "stage-skipped",
        _ => "stage-not-started"
    };

    public static string ToLabel(string executionStatus, string? stageName = null, string? summary = null)
    {
        if (executionStatus == "InProgress"
            && string.Equals(ToBusinessStageName(stageName ?? string.Empty, stageName), "Human Review", StringComparison.Ordinal)
            && string.Equals(summary, "Pending", StringComparison.Ordinal))
        {
            return "Awaiting decision";
        }

        return executionStatus switch
        {
            "Succeeded" => "Completed",
            "InProgress" => "In progress",
            "Failed" => "Failed",
            "Blocked" => "Blocked",
            "Skipped" => "Skipped",
            _ => "Not started"
        };
    }

    public static string RecommendationCssClass(string recommendation) =>
        recommendation.ToLowerInvariant() switch
        {
            "approve" => "rec-approve",
            "deny" => "rec-deny",
            _ => "rec-review"
        };

    public static string? GetAgentAnchor(string stageKey) => stageKey switch
    {
        "DocumentProcessing" => "stage-document-review",
        "Underwriting" => "stage-financial-assessment",
        "ResponsibleAiReview" => "stage-fairness-review",
        "HumanDecision" => "stage-human-review",
        "LoanSetup" => "stage-account-setup",
        "Completed" => "stage-completed",
        _ => null
    };
}
