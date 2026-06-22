namespace LoanWorkflow.Mcp.Adapters;

public static class EvidenceCategoryFolders
{
    public static string For(EvidenceCategory category) => category switch
    {
        EvidenceCategory.Identity => "02_identity",
        EvidenceCategory.Income => "03_income",
        EvidenceCategory.Employment => "04_employment",
        EvidenceCategory.Banking => "05_banking",
        EvidenceCategory.Credit => "06_credit",
        EvidenceCategory.Collateral => "07_collateral",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };
}
