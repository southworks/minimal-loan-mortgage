using System.ComponentModel;
using System.Text.Json;
using LoanWorkflow.Mcp.Builders;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Observability;
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
        [Description("JSON object with loan application data.")]
        string applicationData,
        [Description("JSON object with document-processing-agent output.")]
        string documentProcessingResult,
        [Description("JSON object with underwriting-agent output.")]
        string underwritingResult,
        [Description("JSON object with the human approval decision.")]
        string humanDecision,
        [Description("JSON object with responsible-ai-agent output.")]
        string responsibleAiResult)
        => McpToolInstrumentation.Execute(
            operationName: "mcp.loan_setup.build_account_setup_draft",
            caseId: caseId,
            executionId: executionId,
            agentRole: "loan-setup",
            agentName: "loan-setup-agent",
            action: () => _accountSetupBuilder.Build(
                caseId,
                executionId,
                ParseRequiredJson(applicationData, nameof(applicationData)),
                ParseRequiredJson(documentProcessingResult, nameof(documentProcessingResult)),
                ParseRequiredJson(underwritingResult, nameof(underwritingResult)),
                ParseRequiredJson(humanDecision, nameof(humanDecision)),
                ParseRequiredJson(responsibleAiResult, nameof(responsibleAiResult))));

    private static JsonElement ParseRequiredJson(string json, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
