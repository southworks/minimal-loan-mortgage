# Loan Workflow MCP Server

Demo-grade MCP tool provider for the four-agent loan and mortgage workflow.

In the standard Azure deployment flow, this host runs as an Azure Container App. Foundry prompt agents call its public HTTPS MCP endpoints directly.

## MCP Endpoints

| MCP endpoint | Tools |
| --- | --- |
| `/document-retrieval/mcp` | `get_case_documents`, `enrich_customer_context`, `index_case_documents` |
| `/underwriting-rules/mcp` | `search_case_evidence`, `get_underwriting_context`, `get_relevant_policies` |
| `/policy-knowledge/mcp` | `get_relevant_policies`, `validate_human_decision` |
| `/loan-setup/mcp` | `build_account_setup_draft` |

## Responsibilities

- Read structured demo case data from `dataset-seed/02_identity` through `dataset-seed/07_collateral`
- Enrich customer context from local assets today, with the same adapter boundary intended for Fabric later
- Ensure customer context evidence is indexed idempotently into Azure AI Search using Azure AI Foundry `embed-v-4-0`
- Retrieve evidence and policies from Azure AI Search using Azure AI Foundry `Cohere-rerank-v4.0-pro`
- Seed the policy index from `dataset-seed/08_policy_rag/general_policy.txt` during deploy-time seeding
- Reindex policies only when the policy source hash changes
- Batch embedding inputs, limit Foundry concurrency, and retry transient throttling/server errors

## Azure Deployment

[infra/main.bicep](../../../infra/main.bicep) deploys the MCP host as a Container App and runs a separate Container Apps Job to seed the policy index. Runtime configuration is set through environment variables:

- Azure AI Search endpoint and index names
- Foundry embed and rerank deployment endpoints
- container-safe dataset paths (`/app/dataset-seed`)

The deployed MCP endpoints are:

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
  "McpStartup": {
    "EnsureSearchIndexesOnStartup": true,
    "SeedPoliciesOnStartup": false
  },
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
    "RerankEndpoint": "https://{account}.services.ai.azure.com",
    "ApiKey": "",
    "EmbeddingDimensions": 1024,
    "EmbeddingBatchSize": 16,
    "MaxConcurrentEmbeddingRequests": 1,
    "MaxConcurrentRerankRequests": 2,
    "RetryEnabled": true,
    "MaxRetryAttempts": 4,
    "BaseDelaySeconds": 1,
    "MaxDelaySeconds": 30
  }
}
```

Leave `ApiKey` empty in Azure to use managed identity with scope `https://ai.azure.com/.default`.

Endpoint notes:

- `EmbedEndpoint` must point to the hub deployment path (`/openai/deployments/{name}`). The app calls `/embeddings?api-version=2024-05-01-preview` on that URL. Do not append `/v1/embed`; that path is for serverless model endpoints only.
- `RerankEndpoint` must be the Foundry account base URL. The app calls `/providers/cohere/v2/rerank` on that base. A deployment URL such as `/openai/deployments/cohere-rerank-v4-pro` is normalized automatically, but the account base is preferred.

## Local Development

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run
```

Default URL: `http://localhost:5040`

To run deploy-style policy seeding locally without starting the web host:

```powershell
cd backend/src/LoanWorkflow.Mcp
dotnet run -- --seed-policies
```

For local runs, the default dataset paths in [appsettings.json](appsettings.json) point to the repo `dataset-seed` folder.

## Demo Validation Cases

- `APP-001` — clearly approvable
- `APP-017` — borderline/manual review style case
- `APP-015` — clearly rejectable

When the API workflow starts, Blob-uploaded documents are indexed before the agent workflow begins. During document processing, the agent extracts the case id and calls `enrich_customer_context`; that tool loads the current assets-backed customer context, indexes it if the content hash changed, and returns compact facts for comparison. Policies are still indexed by deploy-time seed.

## Future Extension

Customer context retrieval currently uses `LocalCaseDataAdapter` over local dataset assets. A future `AzureFabricCaseDataAdapter` can replace the local source without changing the `enrich_customer_context` tool contract.
