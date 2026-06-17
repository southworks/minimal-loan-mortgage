# Agent Provisioning

This project provisions the four Azure AI Foundry hosted agents required by the loan and mortgage demo API.

The runtime API does not create or update agents. This CLI owns agent definitions, structured output settings, MCP bindings, memory participation, and idempotent create/update behavior.

## Agents

| Agent | Responsibility | Required MCP |
| --- | --- | --- |
| `document-processing-agent` | Extract and validate document evidence | `document-retrieval-mcp` |
| `underwriting-agent` | Evaluate risk and produce underwriting recommendation | optional `underwriting-rules-mcp` |
| `responsible-ai-agent` | Review fairness, policy, and governance concerns | optional `policy-knowledge-mcp` |
| `loan-setup-agent` | Consolidate prior outputs and produce setup readiness | optional `loan-setup-mcp` |

All agents use the same Foundry model deployment: **Cohere Command A**.

## Agent-as-Code Layout

Each agent folder contains only the minimum demo configuration:

```text
agents/
  document-processing-agent/
    agent.json
    instructions.md
    memory-policy.json
    mcp.json
shared/
  agent-structured-output.schema.json
config/
  provisioning.json
```

### `agent.json`

Declares agent identity, instructions file, shared output schema, and allowed decision values.

### `instructions.md`

Business prompt and workflow boundaries for the agent.

### `memory-policy.json`

Defines whether the agent participates in Agent Memory and what it may read or write.

Agent Memory is used only for durable reusable observations. It is not authoritative case state.

### `mcp.json`

Declares external MCP dependencies. The provisioning CLI validates required project connections and binds them to the agent definition. MCP services themselves are implemented separately.

## Structured Output

All agents must return JSON compatible with the API contract:

```json
{
  "summary": "Concise explanation of the step outcome.",
  "decision": "Agent-specific business decision.",
  "evidence": "Key facts or rationale supporting the decision.",
  "memoryUpdates": ["Optional memory-oriented updates."]
}
```

The shared schema lives in [shared/agent-structured-output.schema.json](shared/agent-structured-output.schema.json). Provisioning configures native JSON schema output rather than relying on prompt-only compliance.

## Configuration

[config/provisioning.json](config/provisioning.json) is the single deployment configuration file:

```json
{
  "ProjectEndpoint": "",
  "FoundryProjectResourceId": "",
  "ModelDeploymentName": "cohere-command-a",
  "MemoryStoreName": "loan-mortgage-agent-memory"
}
```

Environment variable overrides:

- `AZURE_FOUNDRY_PROJECT_ENDPOINT`
- `AZURE_FOUNDRY_PROJECT_RESOURCE_ID`

## Run Locally

```powershell
dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
  --config agent-provisioning/config/provisioning.local.json `
  --agents agent-provisioning/agents
```

Or use:

```powershell
./agent-provisioning/scripts/provision-agents.ps1 -ConfigPath agent-provisioning/config/provisioning.local.json
```

## Deployment Lifecycle

1. Deploy [infra/main.bicep](../infra/main.bicep).
2. Write Foundry outputs into `config/provisioning.local.json`.
3. Run the provisioning CLI.
4. Deploy the API with the same Foundry endpoint and stable agent names.

Full demo deployment:

```powershell
./agent-provisioning/scripts/deploy-demo.ps1 -ResourceGroupName cohereloan-demo-rg
```

GitHub Actions workflow: [.github/workflows/deploy-demo.yml](../.github/workflows/deploy-demo.yml)

## Idempotency and Fail-Fast Behavior

For each agent the CLI:

1. Loads manifest assets.
2. Validates required model deployment configuration.
3. Validates required MCP project connections.
4. Reads the latest Foundry agent version when present.
5. Compares a deterministic definition fingerprint.
6. Creates a new version only when the definition changed.

Results are reported as `Created`, `Updated`, `Unchanged`, or `Failed`.

The provisioning project does not invoke agents, execute MCP business logic, or simulate workflow behavior after deployment.

## Per-Agent Decision Semantics

### Document Processing

`Complete`, `Incomplete`, `Missing Information`, `Validation Failed`, `Inconsistent Documents`, `Suspicious Content`, `Manual Review Required`

### Underwriting

`Approve`, `Conditionally Approve`, `Refer for Manual Review`, `Reject`, `High Risk`, `Insufficient Information`

### Responsible AI

`Passed`, `Passed with Recommendations`, `Policy Warning`, `Fairness Concern`, `Compliance Concern`, `Escalation Required`

### Loan Setup

`Ready for Setup`, `Ready with Conditions`, `Additional Information Required`, `Setup Blocked`, `Operational Review Required`, `Setup Completed`

## Memory Strategy

- Write only durable reusable observations through `memoryUpdates`.
- Do not store case status, workflow checkpoints, approval state, or authoritative business records in Agent Memory.
- Consume prior workflow structured outputs directly; memory is additive context only.

## MCP Strategy

- MCP services are external dependencies provisioned separately.
- `mcp.json` declares required or optional project connections per agent.
- IaC creates placeholder Foundry project connections with target URLs that can be replaced when MCP services are implemented.
- Provisioning validates connection existence and binds agent definitions to those connections.
