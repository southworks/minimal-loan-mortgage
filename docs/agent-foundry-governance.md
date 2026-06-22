# Agent Foundry Governance

This document describes how the loan and mortgage demo applies the [Agent Governance Toolkit (AGT)](https://microsoft.github.io/agent-governance-toolkit/) to the four Azure AI Foundry prompt agents.

## Three governance acts

1. **Tool sandboxing** — per-agent `governance.yaml` policies (`apiVersion: governance.toolkit/v1`) evaluated through MAF function middleware before remote MCP tool calls execute.
2. **Rogue detection** — per-agent `rogue.yaml` sliding-window detection blocks repeated calls to a configured risky tool.
3. **Audit** — AGT governance events append to a hash-chain JSONL store under `data/agent-governance-audit`.

## Co-located policy layout

Policies live beside each agent definition:

```text
agent-provisioning/agents/
  document-processing-agent/
    agent.json
    instructions.md
    mcp.json
    governance.yaml
    rogue.yaml
  underwriting-agent/
    ...
  responsible-ai-agent/
    ...
  loan-setup-agent/
    ...
```

At runtime, `CohereLoanAndMortgage.Foundry.Governance` copies these files to `policies/{agent-name}/` in the API host output directory.

## Per-run wrap vs cached raw agents

| Component | Responsibility |
|-----------|----------------|
| `FoundryAgentProvider` | Caches **ungoverned** Foundry agents (singleton lifecycle) |
| `FoundryGovernedAgentsFactory` | Wraps a fresh governed `FoundryAgents` snapshot per workflow run |
| `BasicLoanWorkflowService` | Sets `GovernanceRunContext` (`caseId`, `executionId`) before `CreateWorkflow` |

This avoids double-wrapping and keeps audit correlation scoped to each execution.

## Phase 0 spike — remote MCP interception

**Question:** Do remote Foundry→MCP tool calls flow through MAF `InvokeFunctionAsync` on a wrapped `AIAgent`?

When `Governance:LogFunctionInvocations` is `true` (default), the API logs:

```text
Phase0 governance function middleware invoked for {AgentRole}: tool_name={ToolName} caseId={CaseId} executionId={ExecutionId}
```

Run one workflow stage against dev Foundry and confirm whether `search_case_evidence`, `enrich_customer_context`, or other MCP tools appear in these logs.

| Spike result | Path forward |
|--------------|--------------|
| Middleware intercepts remote MCP tools | Tool-only YAML enforcement is effective |
| Middleware does **not** intercept | Document the gap; YAML governs only visible hooks; consider MCP-layer governance or local tool registration |

## Tool deny matrix

| Tool | doc | uw | rai | setup |
|------|:---:|:--:|:---:|:-----:|
| `get_case_documents` | D | D | D | D |
| `enrich_customer_context` | A | D | D | D |
| `index_case_documents` | A | D | D | D |
| `search_case_evidence` | A | A | D | D |
| `get_application_profile` | D | A | D | D |
| `get_underwriting_context` | D | A | D | D |
| `get_relevant_policies` | D | A | A | D |
| `get_policies_by_refs` | D | D | A | D |
| `validate_human_decision` | D | D | D | D |
| `build_account_setup_draft` | D | D | D | A |

**D** = explicit deny rule in `governance.yaml`; **A** = allowed via `default_action: allow`.

`AgentToolBoundaries` in `CohereLoanAndMortgage.Foundry.Governance` is the single source of truth for denied tools in code and tests.

## Rogue risky tools

| Agent | `riskyTool` | Rationale |
|-------|-------------|-----------|
| document-processing | `build_account_setup_draft` | Cross-stage escalation to loan setup |
| underwriting | `build_account_setup_draft` | Same |
| responsible-ai | `build_account_setup_draft` | Same |
| loan-setup | `get_underwriting_context` | Re-underwriting bypass |

Defaults: `windowSize: 10`, `triggerCount: 5`.

## Configuration

`appsettings.json`:

```json
"Governance": {
  "EnableFoundryAgentGovernance": true,
  "AgentAuditStoreDirectory": "data/agent-governance-audit",
  "RogueDetectionWindowSize": 10,
  "RogueDetectionTriggerCount": 5,
  "LogFunctionInvocations": true
}
```

Mount a persistent volume for `AgentAuditStoreDirectory` in production.

## Audit verification

1. Run a workflow execution.
2. Inspect `data/agent-governance-audit/agent-governance-audit.jsonl`.
3. From code or tests, call `IAgentGovernanceAuditStore.VerifyIntegrity()` — returns `false` if any entry was tampered with.

## Provisioning traceability

Agent provisioning fingerprints now include governance YAML content. The provisioner CLI emits:

- `governanceToolkitVersion: 4.0.0`
- `policyBundleVersion: v1`
- `governedAgents: [...]`

Policy changes therefore bump the Foundry agent version fingerprint and trigger reprovisioning on the next deploy.
