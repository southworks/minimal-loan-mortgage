# Hosted Agents

Small Microsoft Agent Framework host for the four Foundry hosted agents used by the loan and mortgage workflow.

The same container image serves all agents. At runtime, `AGENT_NAME` selects the instruction set and Foundry Toolbox from `HostedAgentCatalog`.

## Agents

- `document-processing-agent`
- `underwriting-agent`
- `responsible-ai-agent`
- `loan-setup-agent`

## Runtime Configuration

Required environment variables:

- `AGENT_NAME`
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
$env:AGENT_NAME = "document-processing-agent"
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "cohere-command-a"
dotnet run
```

The host listens on `http://localhost:8088` and exposes the Foundry Responses protocol at `/responses`.
