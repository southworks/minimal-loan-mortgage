using System.ComponentModel;
using System.Text.Json;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Models;
using ModelContextProtocol.Server;

namespace LoanWorkflow.Mcp.Tools;

public sealed class LoanSetupTools
{
    private readonly AccountSetupBuilder _accountSetupBuilder;

    public LoanSetupTools(AccountSetupBuilder accountSetupBuilder)
    {
        _accountSetupBuilder = accountSetupBuilder;
    }

    [McpServerTool]
    [Description("Builds a deterministic account setup draft from prior workflow outputs.")]
    public BuildAccountSetupDraftResponse BuildAccountSetupDraft(
        string caseId,
        string executionId,
        JsonElement applicationData,
        JsonElement documentProcessingResult,
        JsonElement underwritingResult,
        JsonElement humanDecision,
        JsonElement responsibleAiResult)
        => _accountSetupBuilder.Build(
            caseId,
            executionId,
            applicationData,
            documentProcessingResult,
            underwritingResult,
            humanDecision,
            responsibleAiResult);
}
