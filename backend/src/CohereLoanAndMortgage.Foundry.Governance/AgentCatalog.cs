namespace CohereLoanAndMortgage.Foundry.Governance;

public static class AgentCatalog
{
    public const string DocumentProcessingAgentName = "document-processing-agent";
    public const string UnderwritingAgentName = "underwriting-agent";
    public const string ResponsibleAiAgentName = "responsible-ai-agent";
    public const string LoanSetupAgentName = "loan-setup-agent";

    public static string ToFolderName(AgentRole role) =>
        role switch
        {
            AgentRole.DocumentProcessing => DocumentProcessingAgentName,
            AgentRole.Underwriting => UnderwritingAgentName,
            AgentRole.ResponsibleAi => ResponsibleAiAgentName,
            AgentRole.LoanSetup => LoanSetupAgentName,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

    public static AgentRole FromFolderName(string folderName)
    {
        if (string.Equals(folderName, DocumentProcessingAgentName, StringComparison.OrdinalIgnoreCase))
        {
            return AgentRole.DocumentProcessing;
        }

        if (string.Equals(folderName, UnderwritingAgentName, StringComparison.OrdinalIgnoreCase))
        {
            return AgentRole.Underwriting;
        }

        if (string.Equals(folderName, ResponsibleAiAgentName, StringComparison.OrdinalIgnoreCase))
        {
            return AgentRole.ResponsibleAi;
        }

        if (string.Equals(folderName, LoanSetupAgentName, StringComparison.OrdinalIgnoreCase))
        {
            return AgentRole.LoanSetup;
        }

        throw new ArgumentException($"Unknown agent folder name '{folderName}'.", nameof(folderName));
    }

    public static IReadOnlyList<AgentRole> AllRoles { get; } =
    [
        AgentRole.DocumentProcessing,
        AgentRole.Underwriting,
        AgentRole.ResponsibleAi,
        AgentRole.LoanSetup
    ];

    public static bool TryResolveRole(string? agentName, out AgentRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return false;
        }

        foreach (AgentRole candidate in AllRoles)
        {
            if (string.Equals(agentName, ToFolderName(candidate), StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }

        return false;
    }
}
