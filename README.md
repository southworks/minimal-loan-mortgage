# CohereLoanAndMortgage API

Educational ASP.NET Core Web API that demonstrates a thin orchestration layer over Microsoft Agent Framework workflows and Azure AI Foundry prompt agents, with case documents loaded from Azure Blob Storage, structured agent outputs, and a single human-in-the-loop approval after underwriting.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2Fazuredeploy.json/createUiDefinition.uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2FcreateUiDefinition.json)

## Deploy to Azure

The primary deployment path is a single end-to-end Azure deployment from the README button.

When you deploy:

1. Azure provisions Foundry, model deployments, Storage, Search, and Container Apps.
2. The API and MCP hosts start as Azure Container Apps.
3. A Container Apps Job seeds the policy index.
4. A deployment script starts the agent provisioning Container Apps Job.
5. The deployment outputs the live API URL.

You do **not** need to run a separate agent CLI after deployment.

Container images are published automatically to GitHub Container Registry by [.github/workflows/publish-container-images.yml](.github/workflows/publish-container-images.yml) on pushes to `main`. The deployment template uses these default image URIs:

- `ghcr.io/southworks/cohereloan-api:demo`
- `ghcr.io/southworks/cohereloan-mcp:demo`
- `ghcr.io/southworks/cohereloan-provisioning:demo`

Make the GHCR packages public after the first workflow run so Azure Container Apps can pull them without registry credentials.

### After deployment

Open the `apiUrl` output from the deployment and use the API endpoints below. Seeded demo cases such as `APP-001`, `APP-017`, and `APP-015` work when their documents are present in Blob Storage under `cases/{caseId}/`.

## Architecture

`document-processing-agent` -> `underwriting-agent` -> human approval -> `responsible-ai-agent` -> `loan-setup-agent`

The API orchestrates the workflow. Foundry prompt agents execute each step and call the public MCP endpoints exposed by [backend/src/LoanWorkflow.Mcp](backend/src/LoanWorkflow.Mcp/README.md).

Evidence indexing is split by source. Uploaded Blob documents are indexed by the API before the agent workflow starts. During agent execution, each prompt agent connects directly to its dedicated MCP endpoint. Policy knowledge is still indexed by the deploy-time seed job.

## Demo limitations

This is intentionally a simple demo:

- Workflow executions are kept in memory only and are lost if the API restarts.
- The API runs as a single Container App replica.
- MCP auth is open for the demo host. The API uses the same MCP services internally to prepare case evidence.
- Case documents must already exist in Azure Blob Storage under `cases/{caseId}/` in the configured container. The API does not expose create-case or document-upload endpoints.

## API Endpoints

- `GET /health` — health probe
- `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/start` — start the basic Agent Framework workflow for a case whose documents are already in Blob Storage
- `GET /api/loan-mortgage/executions/{executionId}/basic/status` — poll workflow status and agent outputs
- `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/executions/{executionId}/resume` — submit a human approval decision and resume the workflow

The start endpoint returns an `executionId`. Use that value for status polling and resume calls.

### Status response shape

```json
{
  "executionId": "abc123...",
  "caseId": "APP-001",
  "status": "Running",
  "agentOutputs": {
    "documentProcessing": null,
    "underwriting": null,
    "responsibleAi": null,
    "loanSetup": null
  },
  "failureReason": null,
  "lastUpdatedUtc": "2026-06-22T12:00:00Z"
}
```

Possible `status` values: `Pending`, `Running`, `AwaitingHumanApproval`, `Completed`, `Failed`.

## UI Integration Pattern

1. Ensure the case documents are available in Blob Storage at `cases/{caseId}/`.
2. Start the workflow with `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/start`.
3. Save the returned `executionId`.
4. Poll `GET /api/loan-mortgage/executions/{executionId}/basic/status`.
5. When `status` becomes `AwaitingHumanApproval`, show the `agentOutputs.underwriting` content to the reviewer.
6. Submit a decision with `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/executions/{executionId}/resume`.
7. Continue polling until `status` becomes `Completed` or `Failed`.

Example approval body:

```json
{
  "approved": true,
  "reviewerComment": "Underwriting looks acceptable."
}
```

## Structured Agent Output

Each Foundry agent must return JSON compatible with:

```json
{
  "summary": "Concise explanation of the step outcome.",
  "decision": "The agent recommendation or outcome.",
  "evidence": "Key facts or rationale supporting the decision."
}
```

If an agent returns invalid or missing structured output, the case fails fast with an explicit error.

## Local Development

Local development is optional and separate from the Azure deployment path.

### Prerequisites

- .NET 9 SDK
- Azure CLI login or another credential available to `DefaultAzureCredential`
- An Azure AI Foundry project with the four demo prompt agents already deployed
- Azure Storage account with a blob container for document uploads

### Run locally

```powershell
cd backend/src/Api.Host
dotnet run
```

Open the API at `http://localhost:5038`.

Run the MCP host separately:

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run
```

Default MCP URL: `http://localhost:5040`

To seed the policy index locally without starting the MCP web host:

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run -- --seed-policies
```

Prompt agent definitions are provisioned by [agent-provisioning/](agent-provisioning/README.md) during Azure deployment. For local experiments, run the provisioning CLI against your project endpoint and MCP base URL.

The legacy hosted-agent sample under [hosted-agents](hosted-agents) is kept for reference only and is not part of the active deployment path.

## Packages

```powershell
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Agents.AI.AzureAI --prerelease
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```
