# Loan Workflow MCP Server

Demo-grade MCP tool provider for the four-agent loan and mortgage workflow.

In the standard Azure deployment flow, this host runs as an Azure Container App. Foundry agents call its public HTTPS endpoints through Foundry project MCP connections.

## MCP Endpoints

| MCP endpoint | Tools |
| --- | --- |
| `/document-retrieval/mcp` | `get_case_documents`, `index_case_documents` |
| `/underwriting-rules/mcp` | `search_case_evidence`, `get_underwriting_context`, `get_relevant_policies` |
| `/policy-knowledge/mcp` | `get_relevant_policies`, `validate_human_decision` |
| `/loan-setup/mcp` | `build_account_setup_draft` |

## Responsibilities

- Read structured demo case data from `dataset-seed/02_identity` through `dataset-seed/07_collateral`
- Index case evidence into Azure AI Search using Azure AI Foundry `embed-v-4-0`
- Retrieve evidence and policies from Azure AI Search using Azure AI Foundry `Cohere-rerank-v4.0-pro`
- Seed the policy index from `dataset-seed/08_policy_rag/general_policy.txt` on startup
- Reindex policies only when the policy source hash changes

## Azure Deployment

[infra/main.bicep](../../../infra/main.bicep) deploys the MCP host as a Container App and sets runtime configuration through environment variables:

- Azure AI Search endpoint and index names
- Foundry embed and rerank deployment endpoints
- container-safe dataset paths (`/app/dataset-seed`)

Foundry MCP connection targets are wired automatically to:

- `{mcpUrl}/document-retrieval/mcp`
- `{mcpUrl}/underwriting-rules/mcp`
- `{mcpUrl}/policy-knowledge/mcp`
- `{mcpUrl}/loan-setup/mcp`

Health check: `GET /health`

## Container Image

The MCP image is built from [Dockerfile](Dockerfile). It includes:

- the MCP host application
- the seeded `dataset-seed` content required by `LocalCaseDataAdapter`

## Configuration

```json
{
  "Dataset": {
    "RootPath": "/app/dataset-seed",
    "PolicyFilePath": "/app/dataset-seed/08_policy_rag/general_policy.txt"
  },
  "AzureSearch": {
    "Endpoint": "https://{search-service}.search.windows.net",
    "EvidenceIndexName": "loan-case-evidence",
    "PolicyIndexName": "loan-policy-knowledge",
    "VectorDimensions": 1024
  },
  "AzureFoundryModels": {
    "EmbedDeploymentName": "cohere-embed-v4",
    "RerankDeploymentName": "cohere-rerank-v4-pro",
    "EmbedModelName": "embed-v-4-0",
    "RerankModelName": "Cohere-rerank-v4.0-pro",
    "EmbedEndpoint": "https://{account}.services.ai.azure.com/openai/deployments/cohere-embed-v4",
    "RerankEndpoint": "https://{account}.services.ai.azure.com/openai/deployments/cohere-rerank-v4-pro",
    "ApiKey": "",
    "EmbeddingDimensions": 1024
  }
}
```

Leave `ApiKey` empty in Azure to use managed identity with scope `https://ai.azure.com/.default`.

## Local Development

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run
```

Default URL: `http://localhost:5040`

For local runs, the default dataset paths in [appsettings.json](appsettings.json) point to the repo `dataset-seed` folder.

## Demo Validation Cases

- `APP-001` — clearly approvable
- `APP-017` — borderline/manual review style case
- `APP-015` — clearly rejectable

Run document processing first so evidence is indexed for the target `caseId` and `executionId` before calling underwriting tools.

## Future Extension

Case retrieval currently uses `LocalCaseDataAdapter` over local dataset assets. A future `AzureFabricCaseDataAdapter` can replace the local source without changing MCP tool contracts.
