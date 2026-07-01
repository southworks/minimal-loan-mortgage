# Cohere Loan & Mortgage workflow RIs

The main purpose of this repository is to show a real world use cases of Loan & Mortgage by integrating Cohere AI models, Microsoft Foundry and Microsoft Agent Framework.
You can find in the directory the dataset-seed, infrastructure, code and deployment for a real world use cases of Loan & Mortgage.

- **Demo inputs:** [`dataset-seed/README.md`](dataset-seed/README.md) — runtime case data for API, MCP, and Fabric
- **Reference / rebuild:** [`data-generation/README.md`](data-generation/README.md) — corpus, scripts, ground truth; [how runtime discovers scenarios](data-generation/README.md#how-runtime-discovers-scenarios)

Click on "Deploy to azure" button and see how it works into your Azure Subcription.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2Fazuredeploy.json/createUiDefinition.uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsouthworks%2Fminimal-loan-mortgage%2Fmain%2Finfra%2FcreateUiDefinition.json)


Below you can see the workflow diagram of the entire solution
<img width="1359" height="604" alt="image" src="https://github.com/user-attachments/assets/12b8e93c-734f-4a30-a323-2ec7813f4ef5" />


## Deploy to Azure

The primary deployment path is a single end-to-end Azure deployment from the README button.

### Fabric prerequisites (required)

The MCP container app reads case data from a Microsoft Fabric Lakehouse, so a Fabric workspace is mandatory. Before clicking Deploy, prepare the UAMI that the Bicep will reuse as the MCP identity:

1. A Fabric workspace (capacity-backed). Note its name.
2. From the repo root, in a PowerShell 7 terminal, run:

   ```powershell
   ./infra/scripts/setup-fabric-provision-identity.ps1 `
     -ResourceGroupName <rg> `
     -WorkspaceName <fabric-ws> `
     -Location eastus `
     -FabricRole Contributor
   ```

   The script creates the user-assigned managed identity, assigns the workspace role, and prints the `managedIdentityResourceId`. The client ID is auto-derived by the deployment.

3. In the Deploy-to-Azure form, on the **Fabric prerequisites** step, paste that value along with the workspace and lakehouse names. The lakehouse is created at deploy time if it does not exist.

Without those values the deployment will fail at the Fabric seed step (the last step) and the MCP will not be able to read case data.

When you deploy:

1. Azure provisions Foundry, model deployments, Search, and Container Apps.
2. The API and MCP hosts start as Azure Container Apps. The MCP runs as the UAMI created by the prerequisite step.
3. A Container Apps Job seeds the policy index into AI Search.
4. A deployment script starts the agent provisioning Container Apps Job.
5. A deployment script provisions the Fabric Lakehouse in the supplied workspace (always runs). The deployment waits for this step before starting the MCP container.
6. A deployment script seeds the lakehouse with case data from `dataset-seed/` (runs only when `enableFabricSeed=true`). Raw documents go to `Files/raw/`, structured JSONs to `Files/bronze/`, and policies to `Files/policies/`. This step runs as a postscript — it does not block the MCP or other infrastructure.
7. The deployment outputs the live API and frontend URLs, Fabric workspace and lakehouse names, and SQL endpoint.

You do **not** need to run a separate agent CLI after deployment.

Container images are published automatically to GitHub Container Registry by [.github/workflows/publish-container-images.yml](.github/workflows/publish-container-images.yml) on pushes to `main`. The deployment template uses these default image URIs:

- `ghcr.io/southworks/cohereloan-api:demo`
- `ghcr.io/southworks/cohereloan-mcp:demo`
- `ghcr.io/southworks/cohereloan-provisioning:demo`

Make the GHCR packages public after the first workflow run so Azure Container Apps can pull them without registry credentials.

### After deployment

Open the `apiUrl` output from the deployment and use the API endpoints below. Seeded demo cases such as `case-01`, `case-17`, and `case-15` work when their documents are present in the bundled `dataset-seed/cases/{caseId}/ingest/` assets inside the API container.

The MCP reads supporting case data from the Fabric Lakehouse created during deployment. The deployment outputs `fabricWorkspaceName` and `fabricLakehouseName`. The MCP container app reads through `DataSource:Mode=Fabric` against `Files/raw/`, `Files/bronze/`, and `Files/policies/` in that lakehouse. Use the Fabric portal to inspect or upload additional cases.

To skip the data upload (e.g., while you repair the workspace or the UAMI role assignment), redeploy `infra/main.bicep` with `enableFabricSeed=false`. The lakehouse is still provisioned (empty but functional). The MCP adapter handles an empty lakehouse at runtime.

## Architecture

`document-processing-agent` -> `underwriting-agent` -> human approval -> `responsible-ai-agent` -> `loan-setup-agent`

The API orchestrates the workflow. Foundry prompt agents execute each step and call the public MCP endpoints exposed by [backend/src/LoanWorkflow.Mcp](backend/src/LoanWorkflow.Mcp/README.md).

Evidence indexing is split by source. Case documents from the bundled dataset assets are indexed by the API before the agent workflow starts. During agent execution, each prompt agent connects directly to its dedicated MCP endpoint. Policy knowledge is still indexed by the deploy-time seed job.

## Demo limitations

This is intentionally a simple demo:

- Workflow executions are kept in memory only and are lost if the API restarts.
- The API runs as a single Container App replica.
- MCP auth is open for the demo host. The API uses the same MCP services internally to prepare case evidence.
- Case documents for the API workflow are read from the bundled `dataset-seed/cases/{caseId}/ingest/` assets inside the API container. Supporting case context for MCP tools lives in a Microsoft Fabric Lakehouse populated at deploy time through `DataSource:Mode=Fabric` against `Files/raw/`, `Files/bronze/`, and `Files/policies/` in the lakehouse named by the `fabricLakehouseName` deployment output. The API does not expose create-case or document-upload endpoints.

## API Endpoints

- `GET /health` — health probe
- `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/start` — start the basic Agent Framework workflow for a seeded demo case
- `GET /api/loan-mortgage/executions/{executionId}/basic/status` — poll workflow status and agent outputs
- `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/executions/{executionId}/resume` — submit a human approval decision and resume the workflow

The start endpoint returns an `executionId`. Use that value for status polling and resume calls.

### Status response shape

```json
{
  "executionId": "abc123...",
  "caseId": "case-01",
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

1. Pick a seeded demo case such as `case-01`, `case-17`, or `case-15`.
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
- Local `dataset-seed` assets for case documents (included in the repo and API container image)

### Run locally

#### VS Code / Cursor (recommended)

1. Install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension (recommended when opening the repo).
2. Copy `backend/src/Api.Host/.env.local.example` to `backend/src/Api.Host/.env.local` and fill in your Azure values.
3. Sign in with Azure CLI (`az login`) or another credential available to `DefaultAzureCredential`.
4. Open **Run and Debug** and start **API + MCP** (or **Full stack (API + MCP + Web)** to include the Blazor UI).

Default URLs when debugging:

- API: `http://localhost:5038`
- MCP: `http://localhost:5040`
- Web UI: `http://localhost:5147`

#### Command line

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
dotnet add package Microsoft.Agents.AI.AzureAI --prerelease
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```
