# Agent Provisioning

This project provisions the four Azure AI Foundry hosted agents required by the loan and mortgage demo API.

In the standard Azure deployment flow, provisioning runs automatically as a Container Apps Job after infrastructure and MCP wiring complete. You do not need to run this CLI manually after clicking **Deploy to Azure**.

## Agents

| Agent | Responsibility | Required MCP |
| --- | --- | --- |
| `document-processing-agent` | Extract and validate document evidence | `document-retrieval-mcp` |
| `underwriting-agent` | Evaluate risk and produce underwriting recommendation | optional `underwriting-rules-mcp` |
| `responsible-ai-agent` | Review fairness, policy, and governance concerns | optional `policy-knowledge-mcp` |
| `loan-setup-agent` | Consolidate prior outputs and produce setup readiness | optional `loan-setup-mcp` |

All agents use the same Foundry model deployment: **Cohere Command A**.

## Azure Deployment Lifecycle

During `Deploy to Azure`, [infra/main.bicep](../infra/main.bicep):

1. Provisions Foundry, Storage, Search, and model deployments.
2. Deploys the MCP Container App and wires Foundry MCP connection targets to its public URL.
3. Starts the agent provisioning Container Apps Job.
4. Waits for the job to finish before completing the deployment.

The provisioning container image is built from [Dockerfile](Dockerfile) and runs:

- MCP connection target updates when `MCP_BASE_URL` is set
- hosted agent create/update
- prompt and structured output configuration
- MCP bindings and memory settings

## Agent-as-Code Layout

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

## Configuration

[config/provisioning.json](config/provisioning.json) is the base configuration file:

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
- `MCP_BASE_URL` — when set, updates Foundry MCP connection targets before provisioning agents

## Optional Local Maintenance

Use this only when updating agent definitions outside the Azure deployment flow:

```powershell
./agent-provisioning/scripts/provision-agents.ps1 -ConfigPath agent-provisioning/config/provisioning.local.json
```

Or:

```powershell
dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
  --config agent-provisioning/config/provisioning.local.json `
  --agents agent-provisioning/agents
```

## Idempotency and Fail-Fast Behavior

For each agent the CLI:

1. Loads manifest assets.
2. Validates required model deployment configuration.
3. Validates required MCP project connections.
4. Reads the latest Foundry agent version when present.
5. Compares a deterministic definition fingerprint.
6. Creates a new version only when the definition changed.

Results are reported as `Created`, `Updated`, `Unchanged`, or `Failed`.

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

- MCP services are deployed separately as the MCP Container App.
- `mcp.json` declares required or optional project connections per agent.
- IaC creates Foundry project connections using the deployed MCP host URL.
- Provisioning validates connection existence and binds agent definitions to those connections.
