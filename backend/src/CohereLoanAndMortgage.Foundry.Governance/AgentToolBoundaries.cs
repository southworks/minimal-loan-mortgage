namespace CohereLoanAndMortgage.Foundry.Governance;

public static class AgentToolBoundaries
{
    public const string GetCaseDocuments = "get_case_documents";
    public const string EnrichCustomerContext = "enrich_customer_context";
    public const string IndexCaseDocuments = "index_case_documents";
    public const string SearchCaseEvidence = "search_case_evidence";
    public const string GetApplicationProfile = "get_application_profile";
    public const string GetUnderwritingContext = "get_underwriting_context";
    public const string GetRelevantPolicies = "get_relevant_policies";
    public const string GetPoliciesByRefs = "get_policies_by_refs";
    public const string ValidateHumanDecision = "validate_human_decision";
    public const string BuildAccountSetupDraft = "build_account_setup_draft";

    public static IReadOnlyList<string> AllTools { get; } =
    [
        GetCaseDocuments,
        EnrichCustomerContext,
        IndexCaseDocuments,
        SearchCaseEvidence,
        GetApplicationProfile,
        GetUnderwritingContext,
        GetRelevantPolicies,
        GetPoliciesByRefs,
        ValidateHumanDecision,
        BuildAccountSetupDraft
    ];

    public static IReadOnlySet<string> GetDeniedTools(AgentRole role) =>
        role switch
        {
            AgentRole.DocumentProcessing => new HashSet<string>(StringComparer.Ordinal)
            {
                GetCaseDocuments,
                GetApplicationProfile,
                GetUnderwritingContext,
                GetRelevantPolicies,
                GetPoliciesByRefs,
                ValidateHumanDecision,
                BuildAccountSetupDraft
            },
            AgentRole.Underwriting => new HashSet<string>(StringComparer.Ordinal)
            {
                GetCaseDocuments,
                EnrichCustomerContext,
                IndexCaseDocuments,
                GetPoliciesByRefs,
                ValidateHumanDecision,
                BuildAccountSetupDraft
            },
            AgentRole.ResponsibleAi => new HashSet<string>(StringComparer.Ordinal)
            {
                GetCaseDocuments,
                EnrichCustomerContext,
                IndexCaseDocuments,
                SearchCaseEvidence,
                GetApplicationProfile,
                GetUnderwritingContext,
                ValidateHumanDecision,
                BuildAccountSetupDraft
            },
            AgentRole.LoanSetup => new HashSet<string>(StringComparer.Ordinal)
            {
                GetCaseDocuments,
                EnrichCustomerContext,
                IndexCaseDocuments,
                SearchCaseEvidence,
                GetApplicationProfile,
                GetUnderwritingContext,
                GetRelevantPolicies,
                GetPoliciesByRefs,
                ValidateHumanDecision
            },
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
}
