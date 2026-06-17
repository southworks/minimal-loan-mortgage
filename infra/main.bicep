@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for deployed resources.')
param baseName string = 'cohereloan'

@description('Foundry model deployment name used by all agents.')
param modelDeploymentName string = 'cohere-command-a'

@description('Cohere Command A model name in Foundry catalog.')
param cohereModelName string = 'Cohere-command-a'

@description('Cohere Command A model version.')
param cohereModelVersion string = '1'

@description('Blob container for uploaded loan documents.')
param documentsContainerName string = 'loan-documents'

@description('Agent memory store name.')
param memoryStoreName string = 'loan-mortgage-agent-memory'

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = toLower(take(replace('${baseName}st${uniqueSuffix}', '-', ''), 24))
var foundryAccountName = toLower(take(replace('${baseName}foundry${uniqueSuffix}', '-', ''), 24))
var projectName = '${baseName}-project'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: documentsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryAccountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: foundryAccountName
    publicNetworkAccess: 'Enabled'
  }
}

resource foundryProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundryAccount
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundryAccount
  name: modelDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'Cohere'
      name: cohereModelName
      version: cohereModelVersion
    }
  }
}

resource documentRetrievalConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'document-retrieval-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: 'https://placeholder-document-retrieval-mcp.azurewebsites.net/mcp'
    metadata: {
      purpose: 'Document retrieval MCP placeholder for demo wiring.'
    }
  }
}

resource underwritingRulesConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'underwriting-rules-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: 'https://placeholder-underwriting-rules-mcp.azurewebsites.net/mcp'
    metadata: {
      purpose: 'Underwriting rules MCP placeholder for demo wiring.'
    }
  }
}

resource policyKnowledgeConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'policy-knowledge-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: 'https://placeholder-policy-knowledge-mcp.azurewebsites.net/mcp'
    metadata: {
      purpose: 'Policy knowledge MCP placeholder for demo wiring.'
    }
  }
}

resource loanSetupConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'loan-setup-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: 'https://placeholder-loan-setup-mcp.azurewebsites.net/mcp'
    metadata: {
      purpose: 'Loan setup MCP placeholder for demo wiring.'
    }
  }
}

output storageAccountName string = storageAccount.name
output blobServiceUri string = storageAccount.properties.primaryEndpoints.blob
output documentsContainerName string = documentsContainerName
output foundryAccountName string = foundryAccount.name
output foundryProjectName string = foundryProject.name
output foundryProjectEndpoint string = 'https://${foundryAccount.properties.customSubDomainName}.services.ai.azure.com/api/projects/${foundryProject.name}'
output foundryProjectResourceId string = foundryProject.id
output modelDeploymentName string = modelDeploymentName
output memoryStoreName string = memoryStoreName
