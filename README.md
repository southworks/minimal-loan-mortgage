# CohereLoanAndMortgage API

Educational ASP.NET Core Web API that demonstrates a thin orchestration layer over Microsoft Agent Framework workflows and Azure AI Foundry hosted agents, with multipart document upload to Azure Blob Storage, structured agent outputs, and a single human-in-the-loop approval after underwriting.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2Fazuredeploy.json/createUiDefinition.uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2FcreateUiDefinition.json)

## Deploy to Azure

The primary deployment path is a single end-to-end Azure deployment from the README button.

When you deploy:

1. Azure provisions Foundry, model deployments, Storage, Search, and Container Apps.
2. The API and MCP hosts start as Azure Container Apps.
3. Foundry MCP connections are wired to the deployed MCP host.
4. A Container Apps Job runs agent provisioning automatically.
5. The deployment outputs the live API URL.

You do **not** need to run a separate provisioning CLI after deployment.

Container images are published automatically to GitHub Container Registry by [.github/workflows/publish-container-images.yml](.github/workflows/publish-container-images.yml) on pushes to `main`. The deployment template uses these default image URIs:

- `ghcr.io/southworks/cohereloan-api:demo`
- `ghcr.io/southworks/cohereloan-mcp:demo`
- `ghcr.io/southworks/cohereloan-provisioning:demo`

Make the GHCR packages public after the first workflow run so Azure Container Apps can pull them without registry credentials.

### After deployment

Open the `apiUrl` output from the deployment and use the API endpoints below. Seeded demo cases such as `APP-001`, `APP-017`, and `APP-015` work against the deployed MCP host.

## Architecture

`document-processing-agent` -> `underwriting-agent` -> human approval -> `responsible-ai-agent` -> `loan-setup-agent`

The API orchestrates the workflow. Foundry hosted agents execute the steps. MCP tools are provided by [backend/src/LoanWorkflow.Mcp](backend/src/LoanWorkflow.Mcp/README.md). Agent definitions and bindings are managed by [agent-provisioning/README.md](agent-provisioning/README.md) and run automatically during deployment.

## Demo limitations

This is intentionally a simple demo:

- Paused workflow cases are kept in memory only and are lost if the API restarts.
- The API runs as a single Container App replica.
- MCP auth is open (`authType: None`) so Foundry can call the demo MCP endpoints over HTTPS.

## API Endpoints

- `GET /health` — health probe
- `POST /api/loan-mortgage/applications` — create a loan case with applicant metadata
- `POST /api/loan-mortgage/applications/{caseId}/documents` — upload documents (multipart/form-data)
- `POST /api/loan-mortgage/applications/{caseId}/workflow/start` — start the Agent Framework workflow
- `GET /api/loan-mortgage/applications/{caseId}` — full case state with persisted structured agent outputs
- `GET /api/loan-mortgage/applications/{caseId}/progress` — lightweight progress for polling
- `POST /api/loan-mortgage/applications/{caseId}/decisions` — submit a human decision (resumes workflow internally)

A sample create-case body is available at [backend/src/Api.Host/sample-request.json](backend/src/Api.Host/sample-request.json).

## UI Integration Pattern

1. Create the loan application with `POST /applications`.
2. Upload documents with `POST /applications/{caseId}/documents`.
3. Start the workflow with `POST /applications/{caseId}/workflow/start`.
4. Poll `GET /applications/{caseId}/progress`.
5. When status becomes `WaitingForHuman`, fetch `GET /applications/{caseId}` to show structured underwriting output.
6. Submit a decision with `POST /applications/{caseId}/decisions`.
7. Continue polling until status becomes `Completed`, `Rejected`, or `Failed`.

Example decision body:

```json
{
  "decisionType": "Underwriting",
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
  "evidence": "Key facts or rationale supporting the decision.",
  "memoryUpdates": ["Optional memory-oriented updates."]
}
```

If an agent returns invalid or missing structured output, the case fails fast with an explicit error.

## Local Development

Local development is optional and separate from the Azure deployment path.

### Prerequisites

- .NET 9 SDK
- Azure CLI login or another credential available to `DefaultAzureCredential`
- An Azure AI Foundry project with the four demo agents already provisioned
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

For local agent maintenance only, see [agent-provisioning/README.md](agent-provisioning/README.md).

## Packages

```powershell
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Agents.AI.AzureAI --prerelease
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```
