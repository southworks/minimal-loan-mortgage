param location string
param resourceTags object
param foundryAccountName string

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryAccountName
  location: location
  tags: resourceTags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: foundryAccountName
    publicNetworkAccess: 'Enabled'
  }
}

output foundryAccountName string = foundryAccount.name
output foundryAccountId string = foundryAccount.id
output foundryEndpointBase string = 'https://${foundryAccountName}.services.ai.azure.com'
