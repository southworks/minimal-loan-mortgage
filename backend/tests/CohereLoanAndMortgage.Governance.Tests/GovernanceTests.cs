using AgentGovernance;
using AgentGovernance.Integration;
using AgentGovernance.Policy;
using CohereLoanAndMortgage.Api.Host.Governance;
using CohereLoanAndMortgage.Foundry.Governance;
using Microsoft.Extensions.AI;

namespace CohereLoanAndMortgage.Governance.Tests;

public sealed class AgentToolBoundariesTests
{
    [Theory]
    [InlineData(AgentRole.DocumentProcessing, "get_case_documents")]
    [InlineData(AgentRole.Underwriting, "enrich_customer_context")]
    [InlineData(AgentRole.ResponsibleAi, "validate_human_decision")]
    [InlineData(AgentRole.LoanSetup, "get_underwriting_context")]
    public void DeniedTools_MatchInstructionAlignedMatrix(AgentRole role, string deniedTool)
    {
        Assert.Contains(deniedTool, AgentToolBoundaries.GetDeniedTools(role));
    }
}

public sealed class PolicyPathTests
{
    [Fact]
    public void ResolveGovernancePolicyPath_UsesAgentFolderName()
    {
        string path = FoundryAgentGovernancePolicyPaths.ResolveGovernancePolicyPath(
            AgentRole.DocumentProcessing,
            AppContext.BaseDirectory);

        Assert.EndsWith(
            Path.Combine("policies", "document-processing-agent", "governance.yaml"),
            path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void EnsurePolicyFilesExist_SucceedsForAllRoles()
    {
        foreach (AgentRole role in AgentCatalog.AllRoles)
        {
            FoundryAgentGovernancePolicyPaths.EnsurePolicyFilesExist(role, AppContext.BaseDirectory);
        }
    }
}

public sealed class GovernanceKernelPolicyTests
{
    [Theory]
    [InlineData(AgentRole.DocumentProcessing, "get_case_documents", false)]
    [InlineData(AgentRole.DocumentProcessing, "enrich_customer_context", true)]
    [InlineData(AgentRole.Underwriting, "get_application_profile", true)]
    [InlineData(AgentRole.Underwriting, "get_case_documents", false)]
    [InlineData(AgentRole.ResponsibleAi, "get_policies_by_refs", true)]
    [InlineData(AgentRole.ResponsibleAi, "search_case_evidence", false)]
    [InlineData(AgentRole.LoanSetup, "build_account_setup_draft", true)]
    [InlineData(AgentRole.LoanSetup, "get_relevant_policies", false)]
    public void EvaluateToolCall_MatchesYamlPolicy(AgentRole role, string toolName, bool expectedAllowed)
    {
        var bootstrap = new FoundryAgentGovernanceBootstrap(policiesBaseDirectory: AppContext.BaseDirectory);
        GovernanceKernel kernel = bootstrap.GetKernel(role);

        ToolCallResult result = kernel.EvaluateToolCall(
            AgentIdentityCatalog.ToMeshAgentId(role),
            toolName,
            new Dictionary<string, object>());

        Assert.Equal(expectedAllowed, result.Allowed);
    }
}

public sealed class RogueToolCallMiddlewareTests
{
    [Fact]
    public async Task BlocksRiskyTool_WhenTriggerCountReachedInWindow()
    {
        var config = new RoguePolicyConfig
        {
            RiskyTool = "build_account_setup_draft",
            WindowSize = 5,
            TriggerCount = 3
        };

        var middleware = new RogueToolCallMiddleware(config);
        var contexts = new List<FunctionInvocationContext>();

        for (int index = 0; index < 2; index++)
        {
            FunctionInvocationContext context = CreateContext("build_account_setup_draft");
            contexts.Add(context);
            await middleware.InvokeAsync(
                null!,
                context,
                static (_, _) => ValueTask.FromResult<object?>("ok"),
                CancellationToken.None);
            Assert.False(context.Terminate);
        }

        FunctionInvocationContext blockedContext = CreateContext("build_account_setup_draft");
        object? blockedResult = await middleware.InvokeAsync(
            null!,
            blockedContext,
            static (_, _) => ValueTask.FromResult<object?>("ok"),
            CancellationToken.None);

        Assert.True(blockedContext.Terminate);
        Assert.Contains("Governance blocked risky tool", blockedResult?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RogueYaml_LoadsPerAgentRiskyTool()
    {
        RoguePolicyConfig loanSetup = RoguePolicyConfig.Load(AgentRole.LoanSetup, AppContext.BaseDirectory);
        Assert.Equal("get_underwriting_context", loanSetup.RiskyTool);
        Assert.Equal(10, loanSetup.WindowSize);
        Assert.Equal(5, loanSetup.TriggerCount);
    }

    private static FunctionInvocationContext CreateContext(string toolName) =>
        new()
        {
            Function = AIFunctionFactory.Create(() => "ok", toolName)
        };
}

public sealed class FileAgentGovernanceAuditStoreTests
{
    [Fact]
    public void AppendAndVerifyIntegrity_DetectsTampering()
    {
        string directory = Path.Combine(Path.GetTempPath(), "agent-governance-audit-" + Guid.NewGuid().ToString("N"));
        var store = new FileAgentGovernanceAuditStore(directory);

        store.Append(new AgentGovernanceAuditRecord(
            0,
            DateTimeOffset.UtcNow,
            "did:mesh:loan-underwriting",
            "ToolCallAllowed",
            "allow",
            string.Empty,
            string.Empty,
            "APP-001",
            "exec-001",
            "ToolCallAllowed"));

        store.Append(new AgentGovernanceAuditRecord(
            0,
            DateTimeOffset.UtcNow,
            "did:mesh:loan-underwriting",
            "ToolCallBlocked",
            "deny",
            string.Empty,
            string.Empty,
            "APP-001",
            "exec-001",
            "ToolCallBlocked"));

        Assert.True(store.VerifyIntegrity());
        Assert.Equal(2, store.ReadAll().Count);

        string auditFile = Path.Combine(directory, "agent-governance-audit.jsonl");
        string[] lines = File.ReadAllLines(auditFile);
        AgentGovernanceAuditRecord? second =
            System.Text.Json.JsonSerializer.Deserialize<AgentGovernanceAuditRecord>(lines[1]);
        Assert.NotNull(second);
        AgentGovernanceAuditRecord tampered = second with { PreviousHash = "tampered" };
        lines[1] = System.Text.Json.JsonSerializer.Serialize(tampered);
        File.WriteAllLines(auditFile, lines);

        var tamperedStore = new FileAgentGovernanceAuditStore(directory);
        Assert.False(tamperedStore.VerifyIntegrity());

        Directory.Delete(directory, recursive: true);
    }
}
