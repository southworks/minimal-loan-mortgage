targetScope = 'resourceGroup'

@description('Foundry account name.')
param foundryAccountName string

@description('Foundry project name.')
param foundryProjectName string

@description('Connection name created in the Foundry project for Application Insights.')
param connectionName string

@description('Application Insights resource ID used as target and metadata.')
param applicationInsightsResourceId string

@secure()
@description('Application Insights connection string. Passed as secure credential to avoid plain-text in deployment logs.')
param applicationInsightsConnectionString string

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName

  resource foundryProject 'projects' existing = {
    name: foundryProjectName
  }
}

resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryAccount::foundryProject
  name: connectionName
  properties: {
    category: 'AppInsights'
    target: applicationInsightsResourceId
    authType: 'ApiKey'
    isSharedToAll: true
    credentials: {
      key: applicationInsightsConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: applicationInsightsResourceId
    }
  }
}

output connectionName string = appInsightsConnection.name
output connectionId string = appInsightsConnection.id
