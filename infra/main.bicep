@description('Base name used for deployed resources.')
param baseName string = 'cohereloan'

@description('Foundry model deployment name used by all agents.')
param modelDeploymentName string = 'cohere-command-a-plus'

@description('SKU used by the Foundry model deployment for the agents. Use GlobalStandard for serverless deployments; use a provisioned SKU only if it is available for the model and region.')
param modelDeploymentSkuName string = 'GlobalStandard'

@minValue(1)
@description('Capacity units for the Foundry model deployment used by the agents. Increase this when agents fail with no_capacity during peak load.')
param modelDeploymentCapacity int = 100

@description('Cohere Command A model name in Foundry catalog.')
param cohereModelName string = 'Cohere-command-a-plus-05-2026'

@description('Cohere Command A model version.')
param cohereModelVersion string = '1'

@description('Foundry deployment name for Cohere embed-v-4-0.')
param embedDeploymentName string = 'cohere-embed-v4'

@description('Cohere embed model name in Foundry catalog.')
param embedModelName string = 'embed-v-4-0'

@description('Cohere embed model version.')
param embedModelVersion string = '1'

@minValue(1)
@description('Capacity units for the Cohere embed deployment. Increase this for faster case evidence indexing and fewer throttling failures.')
param embedDeploymentCapacity int = 1000

@description('Foundry deployment name for Cohere-rerank-v4.0-pro.')
param rerankDeploymentName string = 'cohere-rerank-v4-pro'

@description('Cohere rerank model name in Foundry catalog.')
param rerankModelName string = 'Cohere-rerank-v4.0-pro'

@description('Cohere rerank model version.')
param rerankModelVersion string = '1'

@minValue(1)
@description('Capacity units for the Cohere rerank deployment. Increase this for faster retrieval reranking and fewer throttling failures.')
param rerankDeploymentCapacity int = 5

@description('Agent memory store name.')
param memoryStoreName string = 'loan-mortgage-agent-memory'

@description('Azure AI Search SKU for demo retrieval indexes.')
param searchSku string = 'basic'

@description('Full container image URI for the API host.')
param apiContainerImage string = 'ghcr.io/southworks/cohereloan-api:demo'

@description('Full container image URI for the MCP host.')
param mcpContainerImage string = 'ghcr.io/southworks/cohereloan-mcp:demo'

@description('Full container image URI for the frontend host.')
param frontendContainerImage string = 'ghcr.io/southworks/cohereloan-web:demo'

@description('Full container image URI for the agent provisioning job.')
param provisioningContainerImage string = 'ghcr.io/southworks/cohereloan-provisioning:demo'

@description('When true, provisions Fabric lakehouse, seeds lakehouse data, and configures MCP to read case context from Fabric. Requires fabricWorkspaceName and fabricUamiResourceId.')
param enableFabric bool = false

@description('Fabric workspace name. Required when enableFabric is true. Must be capacity-backed and accessible to the operator.')
param fabricWorkspaceName string = ''

@description('Fabric lakehouse name. Used only when enableFabric is true. Created at deploy time if missing.')
param fabricLakehouseName string = 'LoanProcessingLakehouse'

@description('UAMI resource ID. Required when enableFabric is true. Must be created by setup-fabric-provision-identity.ps1 with workspace role. Used as the MCP container app identity and as the Fabric seed deployment script identity. Its client ID is auto-derived by the deployment.')
param fabricUamiResourceId string = ''

@description('When false, the lakehouse is still provisioned but no data is uploaded. The MCP will see an empty lakehouse and the adapter handles this at runtime. Ignored when enableFabric is false.')
param enableFabricSeed bool = true

@description('Repository archive URL the seed script downloads to fetch infra/scripts/ and dataset-seed/.')
param fabricRepositoryArchiveUrl string = 'https://github.com/southworks/minimal-loan-mortgage/archive/refs/heads/main.zip'

var location = resourceGroup().location
var resolvedFoundryProjectName = '${baseName}-project'

var resourceTags = {
  project: 'inesite'
}

resource resourceGroupTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: resourceTags
  }
}

module naming 'modules/naming.bicep' = {
  name: 'naming'
  params: {
    baseName: baseName
  }
}

module dataServices 'modules/data-services.bicep' = {
  name: 'data-services'
  params: {
    location: location
    resourceTags: resourceTags
    searchServiceName: naming.outputs.searchServiceName
    searchSku: searchSku
    documentIntelligenceAccountName: naming.outputs.documentIntelligenceAccountName
  }
}

module foundry 'modules/foundry.bicep' = {
  name: 'foundry'
  params: {
    location: location
    resourceTags: resourceTags
    foundryAccountName: naming.outputs.foundryAccountName
    resolvedFoundryProjectName: resolvedFoundryProjectName
    modelDeploymentName: modelDeploymentName
    modelDeploymentSkuName: modelDeploymentSkuName
    modelDeploymentCapacity: modelDeploymentCapacity
    cohereModelName: cohereModelName
    cohereModelVersion: cohereModelVersion
    embedDeploymentName: embedDeploymentName
    embedModelName: embedModelName
    embedModelVersion: embedModelVersion
    embedDeploymentCapacity: embedDeploymentCapacity
    rerankDeploymentName: rerankDeploymentName
    rerankModelName: rerankModelName
    rerankModelVersion: rerankModelVersion
    rerankDeploymentCapacity: rerankDeploymentCapacity
  }
}

module platform 'modules/platform.bicep' = {
  name: 'platform'
  params: {
    location: location
    resourceTags: resourceTags
    logAnalyticsName: naming.outputs.logAnalyticsName
    containerAppsEnvironmentName: naming.outputs.containerAppsEnvironmentName
  }
}

module security 'modules/security.bicep' = {
  name: 'security'
  params: {
    location: location
    resourceTags: resourceTags
    apiIdentityName: naming.outputs.apiIdentityName
    provisioningIdentityName: naming.outputs.provisioningIdentityName
    foundryAccountName: foundry.outputs.foundryAccountName
    foundryProjectName: foundry.outputs.foundryProjectName
    searchServiceName: dataServices.outputs.searchServiceName
    documentIntelligenceAccountName: dataServices.outputs.documentIntelligenceAccountName
    enableFabric: enableFabric
    fabricUamiResourceId: fabricUamiResourceId
    mcpIdentityName: naming.outputs.mcpIdentityName
  }
}

module fabricProvision 'modules/fabric-provision.bicep' = if (enableFabric) {
  name: 'fabric-provision'
  params: {
    location: location
    resourceTags: resourceTags
    deploymentSuffix: naming.outputs.deploymentSuffix
    fabricUamiResourceId: fabricUamiResourceId
    fabricWorkspaceName: fabricWorkspaceName
    fabricLakehouseName: fabricLakehouseName
  }
}

module containerApps 'modules/container-apps.bicep' = {
  name: 'container-apps'
  params: {
    location: location
    resourceTags: resourceTags
    containerAppsEnvironmentId: platform.outputs.containerAppsEnvironmentId
    apiAppName: naming.outputs.apiAppName
    mcpAppName: naming.outputs.mcpAppName
    frontendAppName: naming.outputs.frontendAppName
    apiContainerImage: apiContainerImage
    mcpContainerImage: mcpContainerImage
    frontendContainerImage: frontendContainerImage
    apiIdentityId: security.outputs.apiIdentityId
    apiIdentityClientId: security.outputs.apiIdentityClientId
    mcpIdentityId: security.outputs.mcpIdentityId
    mcpIdentityClientId: security.outputs.mcpIdentityClientId
    foundryProjectEndpoint: foundry.outputs.foundryProjectEndpoint
    searchServiceEndpoint: dataServices.outputs.searchServiceEndpoint
    documentIntelligenceEndpoint: dataServices.outputs.documentIntelligenceEndpoint
    embedDeploymentName: foundry.outputs.embedDeploymentName
    embedModelName: foundry.outputs.embedModelName
    rerankDeploymentName: foundry.outputs.rerankDeploymentName
    rerankModelName: foundry.outputs.rerankModelName
    embedEndpoint: foundry.outputs.embedEndpoint
    rerankEndpoint: foundry.outputs.rerankEndpoint
    enableFabric: enableFabric
    fabricWorkspaceName: fabricWorkspaceName
    fabricLakehouseName: fabricLakehouseName
  }
  dependsOn: enableFabric ? [
    fabricProvision
  ] : []
}

module containerJobs 'modules/container-jobs.bicep' = {
  name: 'container-jobs'
  params: {
    location: location
    resourceTags: resourceTags
    containerAppsEnvironmentId: platform.outputs.containerAppsEnvironmentId
    policySeedJobName: naming.outputs.policySeedJobName
    provisioningJobName: naming.outputs.provisioningJobName
    mcpContainerImage: mcpContainerImage
    provisioningContainerImage: provisioningContainerImage
    mcpIdentityId: security.outputs.mcpIdentityId
    provisioningIdentityId: security.outputs.provisioningIdentityId
    provisioningIdentityClientId: security.outputs.provisioningIdentityClientId
    mcpUrl: containerApps.outputs.mcpUrl
    mcpContainerEnv: containerApps.outputs.mcpContainerEnv
    foundryProjectEndpoint: foundry.outputs.foundryProjectEndpoint
    modelDeploymentName: foundry.outputs.modelDeploymentName
  }
}

module postDeployScripts 'modules/post-deploy-scripts.bicep' = {
  name: 'post-deploy-scripts'
  params: {
    location: location
    resourceTags: resourceTags
    deploymentSuffix: naming.outputs.deploymentSuffix
    deploymentScriptIdentityName: naming.outputs.deploymentScriptIdentityName
    foundryAccountName: foundry.outputs.foundryAccountName
    foundryProjectName: foundry.outputs.foundryProjectName
    policySeedJobName: naming.outputs.policySeedJobName
    provisioningJobName: naming.outputs.provisioningJobName
    enableFabric: enableFabric
    enableFabricSeed: enableFabric && enableFabricSeed
    fabricUamiResourceId: fabricUamiResourceId
    fabricWorkspaceId: enableFabric ? fabricProvision!.outputs.workspaceId : ''
    fabricWorkspaceName: enableFabric ? fabricProvision!.outputs.workspaceName : ''
    fabricLakehouseId: enableFabric ? fabricProvision!.outputs.lakehouseId : ''
    fabricLakehouseName: enableFabric ? fabricProvision!.outputs.lakehouseName : ''
    fabricRepositoryArchiveUrl: fabricRepositoryArchiveUrl
  }
  dependsOn: [
    containerJobs
  ]
}

output foundryAccountName string = foundry.outputs.foundryAccountName
output foundryProjectName string = foundry.outputs.foundryProjectName
output foundryProjectEndpoint string = foundry.outputs.foundryProjectEndpoint
output foundryProjectResourceId string = foundry.outputs.foundryProjectResourceId
output modelDeploymentName string = foundry.outputs.modelDeploymentName
output embedDeploymentName string = foundry.outputs.embedDeploymentName
output embedModelName string = foundry.outputs.embedModelName
output rerankDeploymentName string = foundry.outputs.rerankDeploymentName
output rerankModelName string = foundry.outputs.rerankModelName
output memoryStoreName string = memoryStoreName
output searchServiceName string = dataServices.outputs.searchServiceName
output searchServiceEndpoint string = dataServices.outputs.searchServiceEndpoint
output documentIntelligenceAccountName string = dataServices.outputs.documentIntelligenceAccountName
output documentIntelligenceEndpoint string = dataServices.outputs.documentIntelligenceEndpoint
output apiUrl string = containerApps.outputs.apiUrl
output frontendUrl string = containerApps.outputs.frontendUrl
output mcpUrl string = containerApps.outputs.mcpUrl
output policySeedJobName string = containerJobs.outputs.policySeedJobName
output provisioningJobName string = containerJobs.outputs.provisioningJobName
output fabricWorkspaceName string = enableFabric ? fabricProvision!.outputs.workspaceName : ''
output fabricLakehouseName string = enableFabric ? fabricProvision!.outputs.lakehouseName : ''
output fabricSqlServer string = enableFabric ? fabricProvision!.outputs.sqlServer : ''
output fabricSqlDatabase string = enableFabric ? fabricProvision!.outputs.sqlDatabase : ''
output fabricSeedDeploymentScriptName string = postDeployScripts.outputs.fabricSeedDeploymentScriptName
