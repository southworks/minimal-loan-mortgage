using System.Text.Json;
using LoanWorkflow.Mcp.Adapters;
using LoanWorkflow.Mcp.Models;

namespace LoanWorkflow.Mcp.Builders;

public sealed class HumanDecisionValidator
{
    private static readonly HashSet<string> AllowedHumanDecisions = new(StringComparer.OrdinalIgnoreCase)
    {
        "approved",
        "denied"
    };

    public async Task<ValidateHumanDecisionResponse> ValidateAsync(
        string caseId,
        string executionId,
        string humanDecision,
        string underwritingDecision,
        string? reviewerComment,
        PolicyIndexAdapter policyIndexAdapter,
        CancellationToken cancellationToken = default)
    {
        var flags = new List<string>();
        var notes = new List<string>();

        if (!AllowedHumanDecisions.Contains(humanDecision))
        {
            flags.Add("invalid_human_decision_value");
            notes.Add("Human decision must be 'approved' or 'denied'.");
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            flags.Add("missing_case_id");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            flags.Add("missing_execution_id");
        }

        if (IsOverride(underwritingDecision, humanDecision) && string.IsNullOrWhiteSpace(reviewerComment))
        {
            flags.Add("missing_override_rationale");
            notes.Add("Reviewer comment is required when the human decision overrides underwriting.");
        }

        if (IsRejectOverride(underwritingDecision, humanDecision))
        {
            flags.Add("human_denial_overrides_underwriting_approval");
            notes.Add("Human denial conflicts with an underwriting approval recommendation.");
        }

        if (IsApproveOverride(underwritingDecision, humanDecision))
        {
            flags.Add("human_approval_overrides_underwriting_reject");
            notes.Add("Human approval conflicts with an underwriting rejection recommendation.");
        }

        var policyQuery = BuildPolicyQuery(humanDecision, underwritingDecision, reviewerComment);
        var policies = await policyIndexAdapter.GetRelevantPoliciesAsync(
            policyQuery,
            reviewerComment,
            topK: 3,
            cancellationToken);

        foreach (var policy in policies.Policies)
        {
            if (humanDecision.Equals("approved", StringComparison.OrdinalIgnoreCase)
                && policy.Action.Contains("deny", StringComparison.OrdinalIgnoreCase)
                && policy.Threshold.Contains("out of policy", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add($"approval_conflicts_with_policy_{policy.PolicyRef}");
                notes.Add($"Human approval may conflict with policy {policy.PolicyRef}.");
            }
        }

        var consistencyStatus = flags.Count switch
        {
            0 => "passed",
            <= 2 => "warning",
            _ => "concern"
        };

        return new ValidateHumanDecisionResponse
        {
            IsStructurallyValid = !flags.Contains("invalid_human_decision_value")
                && !flags.Contains("missing_case_id")
                && !flags.Contains("missing_execution_id"),
            ConsistencyStatus = consistencyStatus,
            Flags = flags,
            PolicyRefs = policies.Policies.Select(policy => policy.PolicyRef).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Notes = notes
        };
    }

    private static bool IsOverride(string underwritingDecision, string humanDecision)
    {
        if (underwritingDecision.Contains("reject", StringComparison.OrdinalIgnoreCase)
            && humanDecision.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (underwritingDecision.Contains("approve", StringComparison.OrdinalIgnoreCase)
            && humanDecision.Equals("denied", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsRejectOverride(string underwritingDecision, string humanDecision) =>
        underwritingDecision.Contains("approve", StringComparison.OrdinalIgnoreCase)
        && humanDecision.Equals("denied", StringComparison.OrdinalIgnoreCase);

    private static bool IsApproveOverride(string underwritingDecision, string humanDecision) =>
        underwritingDecision.Contains("reject", StringComparison.OrdinalIgnoreCase)
        && humanDecision.Equals("approved", StringComparison.OrdinalIgnoreCase);

    private static string BuildPolicyQuery(
        string humanDecision,
        string underwritingDecision,
        string? reviewerComment)
    {
        return $"Validate human decision '{humanDecision}' against underwriting '{underwritingDecision}'. Reviewer comment: {reviewerComment ?? "none"}.";
    }
}

public sealed class AccountSetupBuilder
{
    public BuildAccountSetupDraftResponse Build(
        string caseId,
        string executionId,
        JsonElement applicationData,
        JsonElement documentProcessingResult,
        JsonElement underwritingResult,
        JsonElement humanDecision,
        JsonElement responsibleAiResult)
    {
        var missingFields = new List<string>();
        var blockingIssues = new List<string>();

        if (caseId.Length == 0)
        {
            missingFields.Add("caseId");
        }

        if (executionId.Length == 0)
        {
            missingFields.Add("executionId");
        }

        if (applicationData.ValueKind == JsonValueKind.Undefined || applicationData.ValueKind == JsonValueKind.Null)
        {
            missingFields.Add("applicationData");
        }

        if (underwritingResult.ValueKind == JsonValueKind.Undefined || underwritingResult.ValueKind == JsonValueKind.Null)
        {
            missingFields.Add("underwritingResult");
        }

        if (humanDecision.ValueKind == JsonValueKind.Undefined || humanDecision.ValueKind == JsonValueKind.Null)
        {
            missingFields.Add("humanDecision");
        }

        var humanApproved = TryGetApproved(humanDecision);
        var responsibleDecision = TryGetDecision(responsibleAiResult);

        if (humanApproved == false)
        {
            blockingIssues.Add("Human decision denied the loan.");
        }

        if (!string.IsNullOrWhiteSpace(responsibleDecision)
            && responsibleDecision.Contains("concern", StringComparison.OrdinalIgnoreCase))
        {
            blockingIssues.Add($"Responsible AI raised a concern: {responsibleDecision}");
        }

        if (!string.IsNullOrWhiteSpace(responsibleDecision)
            && responsibleDecision.Contains("escalation", StringComparison.OrdinalIgnoreCase))
        {
            blockingIssues.Add("Responsible AI requires escalation before setup.");
        }

        var setupStatus = blockingIssues.Count switch
        {
            0 when missingFields.Count == 0 => humanApproved == true ? "Ready for Setup" : "Setup Blocked",
            0 => "Additional Information Required",
            _ => "Setup Blocked"
        };

        var draft = new AccountSetupDraft
        {
            CaseId = caseId,
            ExecutionId = executionId,
            SetupStatus = setupStatus,
            ApplicationSummary = applicationData.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? JsonDocument.Parse("{}").RootElement
                : applicationData,
            UnderwritingSummary = underwritingResult.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? JsonDocument.Parse("{}").RootElement
                : underwritingResult,
            HumanDecisionSummary = humanDecision.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? JsonDocument.Parse("{}").RootElement
                : humanDecision,
            ResponsibleAiSummary = responsibleAiResult.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? JsonDocument.Parse("{}").RootElement
                : responsibleAiResult,
            OperationalRequirements = BuildOperationalRequirements(humanApproved, responsibleDecision)
        };

        return new BuildAccountSetupDraftResponse
        {
            Draft = draft,
            MissingFields = missingFields,
            BlockingIssues = blockingIssues
        };
    }

    private static IReadOnlyList<string> BuildOperationalRequirements(bool? humanApproved, string? responsibleDecision)
    {
        var requirements = new List<string> { "Confirm final loan terms and account identifiers." };

        if (humanApproved == true)
        {
            requirements.Add("Prepare account opening package for approved loan.");
        }

        if (!string.IsNullOrWhiteSpace(responsibleDecision)
            && responsibleDecision.Contains("recommendation", StringComparison.OrdinalIgnoreCase))
        {
            requirements.Add("Apply responsible AI recommendations before operational handoff.");
        }

        return requirements;
    }

    private static bool? TryGetApproved(JsonElement humanDecision)
    {
        if (humanDecision.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (humanDecision.TryGetProperty("approved", out var approvedProperty)
            && approvedProperty.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (humanDecision.TryGetProperty("approved", out approvedProperty)
            && approvedProperty.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (humanDecision.TryGetProperty("decision", out var decisionProperty))
        {
            var decision = decisionProperty.GetString();
            if (decision?.Equals("approved", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (decision?.Equals("denied", StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }
        }

        return null;
    }

    private static string? TryGetDecision(JsonElement responsibleAiResult)
    {
        if (responsibleAiResult.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return responsibleAiResult.TryGetProperty("decision", out var decisionProperty)
            ? decisionProperty.GetString()
            : null;
    }
}
