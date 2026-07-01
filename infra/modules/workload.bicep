targetScope = 'resourceGroup'

@description('Application Insights connection string for workload containers.')
param applicationInsightsConnectionString string

@description('Search service name.')
param searchServiceName string

@description('MCP managed identity client ID.')
param mcpIdentityClientId string

@description('API managed identity client ID.')
param apiIdentityClientId string

@description('Storage blob service URI.')
param storageBlobServiceUri string

@description('Blob container for uploaded loan documents.')
param documentsContainerName string

@description('Foundry project endpoint.')
param foundryProjectEndpoint string

@description('Foundry embed deployment name.')
param embedDeploymentName string

@description('Foundry rerank deployment name.')
param rerankDeploymentName string

@description('Foundry embed model name.')
param embedModelName string

@description('Foundry rerank model name.')
param rerankModelName string

@description('Foundry embed endpoint.')
param embedEndpoint string

@description('Foundry rerank endpoint.')
param rerankEndpoint string

@description('Document Intelligence endpoint.')
param documentIntelligenceEndpoint string

var mcpFoundryModelEnv = [
  { name: 'AzureFoundryModels__EmbedDeploymentName', value: embedDeploymentName }
  { name: 'AzureFoundryModels__RerankDeploymentName', value: rerankDeploymentName }
  { name: 'AzureFoundryModels__EmbedModelName', value: embedModelName }
  { name: 'AzureFoundryModels__RerankModelName', value: rerankModelName }
  { name: 'AzureFoundryModels__EmbedEndpoint', value: embedEndpoint }
  { name: 'AzureFoundryModels__RerankEndpoint', value: rerankEndpoint }
  { name: 'AzureFoundryModels__EmbeddingDimensions', value: '1024' }
  { name: 'AzureFoundryModels__EmbeddingBatchSize', value: '16' }
  { name: 'AzureFoundryModels__MaxConcurrentEmbeddingRequests', value: '1' }
  { name: 'AzureFoundryModels__MaxConcurrentRerankRequests', value: '2' }
]

var mcpContainerEnv = concat([
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsightsConnectionString }
  { name: 'AzureSearch__Endpoint', value: 'https://${searchServiceName}.search.windows.net' }
  { name: 'AzureSearch__EvidenceIndexName', value: 'loan-case-evidence' }
  { name: 'AzureSearch__PolicyIndexName', value: 'loan-policy-knowledge' }
  { name: 'AzureSearch__VectorDimensions', value: '1024' }
  { name: 'Dataset__RootPath', value: '/app/dataset-seed' }
  { name: 'Dataset__PolicyFilePath', value: '/app/dataset-seed/08_policy_rag/general_policy.txt' }
  { name: 'AZURE_CLIENT_ID', value: mcpIdentityClientId }
], mcpFoundryModelEnv)

output mcpContainerEnv array = mcpContainerEnv

output policySeedContainerEnv array = concat(mcpContainerEnv, [
  { name: 'AzureFoundryModels__MaxRetryAttempts', value: '10' }
  { name: 'AzureFoundryModels__MaxDelaySeconds', value: '60' }
])

output apiContainerEnv array = [
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsightsConnectionString }
  { name: 'AZURE_FOUNDRY_PROJECT_ENDPOINT', value: foundryProjectEndpoint }
  { name: 'AZURE_STORAGE_BLOB_SERVICE_URI', value: storageBlobServiceUri }
  { name: 'AzureSearch__Endpoint', value: 'https://${searchServiceName}.search.windows.net' }
  { name: 'AzureSearch__EvidenceIndexName', value: 'loan-case-evidence' }
  { name: 'AzureSearch__PolicyIndexName', value: 'loan-policy-knowledge' }
  { name: 'AzureSearch__VectorDimensions', value: '1024' }
  { name: 'AzureStorage__ContainerName', value: documentsContainerName }
  { name: 'AzureFoundryModels__EmbedDeploymentName', value: embedDeploymentName }
  { name: 'AzureFoundryModels__RerankDeploymentName', value: rerankDeploymentName }
  { name: 'AzureFoundryModels__EmbedModelName', value: embedModelName }
  { name: 'AzureFoundryModels__RerankModelName', value: rerankModelName }
  { name: 'AzureFoundryModels__EmbedEndpoint', value: embedEndpoint }
  { name: 'AzureFoundryModels__RerankEndpoint', value: rerankEndpoint }
  { name: 'AzureFoundryModels__EmbeddingDimensions', value: '1024' }
  { name: 'AzureFoundryModels__EmbeddingBatchSize', value: '16' }
  { name: 'AzureFoundryModels__MaxConcurrentEmbeddingRequests', value: '1' }
  { name: 'AzureFoundryModels__MaxConcurrentRerankRequests', value: '2' }
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'AZURE_CLIENT_ID', value: apiIdentityClientId }
  { name: 'DocumentExtraction__Endpoint', value: documentIntelligenceEndpoint }
]
