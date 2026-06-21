# Hosted Agents (Legacy Reference)

This folder contains a legacy Microsoft Agent Framework hosted-agent sample. The active deployment path uses Foundry prompt agents with direct public MCP tools instead of hosted agents and Foundry toolboxes.

See [agent-provisioning/README.md](../agent-provisioning/README.md) for the current agent provisioning path used by [infra/main.bicep](../infra/main.bicep).

## Original sample

The same container image serves all agents. In Foundry, the platform-provided `AGENT_NAME` selects the instruction set and Foundry Toolbox from `HostedAgentCatalog`. For local development, `HOSTED_AGENT_CATALOG_NAME` is supported as a fallback.

## Agents

- `document-processing-agent`
- `underwriting-agent`
- `responsible-ai-agent`
- `loan-setup-agent`

## Runtime Configuration

Required environment variables:

- In Foundry: the platform-provided `AGENT_NAME`
- For local runs only: `HOSTED_AGENT_CATALOG_NAME`
- `FOUNDRY_PROJECT_ENDPOINT` or `AZURE_AI_PROJECT_ENDPOINT`
- `AZURE_AI_MODEL_DEPLOYMENT_NAME`

Each agent attaches only its own Foundry Toolbox:

- `document-processing-agent` uses `document-retrieval-toolbox`
- `underwriting-agent` uses `underwriting-rules-toolbox`
- `responsible-ai-agent` uses `policy-knowledge-toolbox`
- `loan-setup-agent` uses `loan-setup-toolbox`

The Agent Framework workflow only orchestrates the agent order and human approval.

## Local Run

```powershell
cd hosted-agents/src/CohereLoanAndMortgage.HostedAgents
$env:HOSTED_AGENT_CATALOG_NAME = "document-processing-agent"
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "cohere-command-a"
dotnet run
```

The host listens on `http://localhost:8088` and exposes the Foundry Responses protocol at `/responses`.
