# Loan Workflow MCP Server

Demo-grade MCP tool provider for the four-agent loan and mortgage workflow.

This host exposes four logical MCP HTTP endpoints that align with the existing Foundry agent provisioning connections:

| MCP endpoint | Tools |
| --- | --- |
| `/document-retrieval/mcp` | `get_case_documents`, `index_case_documents` |
| `/underwriting-rules/mcp` | `search_case_evidence`, `get_underwriting_context`, `get_relevant_policies` |
| `/policy-knowledge/mcp` | `get_relevant_policies`, `validate_human_decision` |
| `/loan-setup/mcp` | `build_account_setup_draft` |

## Responsibilities

- Read structured demo case data from `dataset-seed/02_identity` through `dataset-seed/07_collateral`
- Index case evidence into Azure AI Search using Cohere Embed
- Retrieve evidence and policies from Azure AI Search using Cohere Rerank
- Seed the policy index from `dataset-seed/08_policy_rag/general_policy.txt` on startup
- Reindex policies only when the policy source hash changes

## Prerequisites

- .NET 9 SDK
- Azure AI Search service
- Cohere API key for embedding and rerank
- Azure CLI login or another credential available to `DefaultAzureCredential` for Azure AI Search

## Configuration

```json
{
  "Dataset": {
    "RootPath": "../../../dataset-seed",
    "PolicyFilePath": "../../../dataset-seed/08_policy_rag/general_policy.txt"
  },
  "AzureSearch": {
    "Endpoint": "https://{search-service}.search.windows.net",
    "EvidenceIndexName": "loan-case-evidence",
    "PolicyIndexName": "loan-policy-knowledge",
    "VectorDimensions": 1024
  },
  "Cohere": {
    "ApiKey": "",
    "BaseUrl": "https://api.cohere.ai",
    "EmbedModel": "embed-english-v3.0",
    "RerankModel": "rerank-english-v3.0"
  }
}
```

Environment variable overrides:

```powershell
$env:AzureSearch__Endpoint = "https://{search-service}.search.windows.net"
$env:Cohere__ApiKey = "..."
```

## Run

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run
```

Default URL: `http://localhost:5040`

Health check: `GET /health`

## Infrastructure

Infrastructure deployment provisions Azure AI Search and the empty index containers. Application startup creates the indexes if needed and seeds policy content.

See [infra/main.bicep](../../infra/main.bicep) for the Search service outputs:

- `searchServiceName`
- `searchServiceEndpoint`

## Future Extension

Case retrieval currently uses `LocalCaseDataAdapter` over local dataset assets. A future `AzureFabricCaseDataAdapter` can replace the local source without changing MCP tool contracts.

## Demo Validation Cases

Use these seeded cases for manual end-to-end validation:

- `APP-001` — clearly approvable
- `APP-017` — borderline/manual review style case
- `APP-015` — clearly rejectable

Run document processing first so evidence is indexed for the target `caseId` and `executionId` before calling underwriting tools.
