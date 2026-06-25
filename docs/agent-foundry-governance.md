# Agent Foundry Governance

This document describes how the loan and mortgage demo applies the [Agent Governance Toolkit (AGT)](https://microsoft.github.io/agent-governance-toolkit/) to the four Azure AI Foundry prompt agents.

## Three governance acts

1. **Tool sandboxing** — per-agent `governance.yaml` policies (`apiVersion: governance.toolkit/v1`) evaluated before tool execution.
2. **Rogue detection** — per-agent `rogue.yaml` sliding-window detection blocks repeated calls to a configured risky tool.
3. **Audit** — governance events append to a hash-chain JSONL store under `data/agent-governance-audit`.

## Enforcement

Remote Foundry agents call MCP tools over HTTP. Governance is enforced on the **MCP server** before each tool handler runs via `GovernedMcpServerTool` and `McpToolGovernanceCoordinator` in `LoanWorkflow.Mcp`.

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

At runtime, `CohereLoanAndMortgage.Foundry.Governance` copies these files to `policies/{agent-name}/` in the MCP server output directory.

## MCP-layer governance

1. **`McpAgentRoleMiddleware`** — requires `X-Agent-Role` header (e.g. `document-processing-agent`) on MCP routes when `Governance:RequireMcpAgentRoleHeader` is `true`.
2. **`GovernedMcpServerTool`** — wraps each MCP tool; delegates to `McpToolGovernanceCoordinator`.
3. **`McpToolGovernanceCoordinator`** — evaluates `governance.yaml`, applies rogue detection (keyed by role + `caseId` + `executionId` from tool args), writes audit records, and logs blocked or rogue-detected calls at warning level.

Blocked calls return an MCP error result; denied tools never reach the handler.

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

Per-agent deny rules live in `agent-provisioning/agents/*/governance.yaml`.

## Rogue risky tools

| Agent | `riskyTool` | Rationale |
|-------|-------------|-----------|
| document-processing | `build_account_setup_draft` | Cross-stage escalation to loan setup |
| underwriting | `build_account_setup_draft` | Same |
| responsible-ai | `build_account_setup_draft` | Same |
| loan-setup | `get_underwriting_context` | Re-underwriting bypass |

Defaults: `windowSize: 10`, `triggerCount: 5`.

## Configuration

`LoanWorkflow.Mcp/appsettings.json`:

```json
"Governance": {
  "EnableMcpToolGovernance": true,
  "RequireMcpAgentRoleHeader": true,
  "AgentAuditStoreDirectory": "data/agent-governance-audit"
}
```

Set `EnableMcpToolGovernance: false` or `RequireMcpAgentRoleHeader: false` for local MCP testing without agent headers.

Mount a persistent volume for `AgentAuditStoreDirectory` in production.

## Audit verification

1. Run a workflow execution that triggers MCP tool calls.
2. Inspect `data/agent-governance-audit/agent-governance-audit.jsonl` on the MCP host.

## Provisioning traceability

Agent provisioning fingerprints include governance YAML content. The provisioner CLI emits:

- `governanceToolkitVersion: 4.0.0`
- `policyBundleVersion: v1`
- `governedAgents: [...]`

Policy changes therefore bump the Foundry agent version fingerprint and trigger reprovisioning on the next deploy.
