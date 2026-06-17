# CohereLoanAndMortgage API

Educational ASP.NET Core Web API that demonstrates a thin orchestration layer over Microsoft Agent Framework workflows and pre-provisioned Azure AI Foundry agents, with multipart document upload to Azure Blob Storage, structured agent outputs, and a single human-in-the-loop approval after underwriting.

The API does **not** provision agents, configure prompts/models, implement MCP services, or duplicate Foundry capabilities. It assumes those components already exist.

Agent provisioning lives in [agent-provisioning/README.md](agent-provisioning/README.md).

## Prerequisites

- .NET 9 SDK
- Azure CLI login or another credential available to `DefaultAzureCredential`
- An Azure AI Foundry project containing these existing agents:
  - `document-processing-agent`
  - `underwriting-agent`
  - `responsible-ai-agent`
  - `loan-setup-agent`
- An Azure Storage account with a blob container for document uploads
- Each Foundry agent must return structured JSON at step completion:

```json
{
  "summary": "Concise explanation of the step outcome.",
  "decision": "The agent recommendation or outcome.",
  "evidence": "Key facts or rationale supporting the decision.",
  "memoryUpdates": ["Optional memory-oriented updates."]
}
```

If an agent returns invalid or missing structured output, the case fails fast with an explicit error.

## Configuration

### Azure AI Foundry

```json
"AzureFoundry": {
  "ProjectEndpoint": "https://{account}.services.ai.azure.com/api/projects/{project}"
}
```

Or:

```powershell
$env:AZURE_FOUNDRY_PROJECT_ENDPOINT = "https://{account}.services.ai.azure.com/api/projects/{project}"
```

### Azure Blob Storage

Use either a connection string or a blob service URI with `DefaultAzureCredential`:

```json
"AzureStorage": {
  "ConnectionString": "",
  "BlobServiceUri": "https://{account}.blob.core.windows.net",
  "ContainerName": "loan-documents"
}
```

Or:

```powershell
$env:AZURE_STORAGE_CONNECTION_STRING = "..."
# or
$env:AZURE_STORAGE_BLOB_SERVICE_URI = "https://{account}.blob.core.windows.net"
```

The API validates required Foundry and storage settings at startup and fails fast if they are missing.

## Run

```powershell
cd backend/src/Api.Host
dotnet run
```

Open the API at `http://localhost:5038` after `dotnet run`.

A sample create-case body is available at [backend/src/Api.Host/sample-request.json](backend/src/Api.Host/sample-request.json).

## API Endpoints

- `POST /api/loan-mortgage/applications` — create a loan case with applicant metadata
- `POST /api/loan-mortgage/applications/{caseId}/documents` — upload documents (multipart/form-data)
- `POST /api/loan-mortgage/applications/{caseId}/workflow/start` — start the Agent Framework workflow
- `GET /api/loan-mortgage/applications/{caseId}` — full case state with persisted structured agent outputs
- `GET /api/loan-mortgage/applications/{caseId}/progress` — lightweight progress for polling
- `POST /api/loan-mortgage/applications/{caseId}/decisions` — submit a human decision (resumes workflow internally)

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

## Workflow

`document-processing-agent` -> `underwriting-agent` -> human approval -> `responsible-ai-agent` -> `loan-setup-agent`

Human approval uses Microsoft Agent Framework request/response handling and workflow checkpoints. Submitting a decision resumes the workflow internally; there is no separate public resume endpoint for the current single pause point.

The API persists only minimal UI-facing state: case metadata, document references, checkpoint handles, timeline, pending approval info, and structured agent step results. Foundry Agent Memory evolves independently for collaborative agent context.

Paused cases are kept in memory only and are lost if the API process restarts.

## Packages

```powershell
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Agents.AI.AzureAI --prerelease
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```
