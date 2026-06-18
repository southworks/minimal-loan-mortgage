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

@description('Foundry deployment name for Cohere embed-v-4-0.')
param embedDeploymentName string = 'cohere-embed-v4'

@description('Cohere embed model name in Foundry catalog.')
param embedModelName string = 'embed-v-4-0'

@description('Cohere embed model version.')
param embedModelVersion string = '1'

@description('Foundry deployment name for Cohere-rerank-v4.0-pro.')
param rerankDeploymentName string = 'cohere-rerank-v4-pro'

@description('Cohere rerank model name in Foundry catalog.')
param rerankModelName string = 'Cohere-rerank-v4.0-pro'

@description('Cohere rerank model version.')
param rerankModelVersion string = '1'

@description('Blob container for uploaded loan documents.')
param documentsContainerName string = 'loan-documents'

@description('Agent memory store name.')
param memoryStoreName string = 'loan-mortgage-agent-memory'

@description('Azure AI Search SKU for demo retrieval indexes.')
param searchSku string = 'basic'

@description('Full container image URI for the API host.')
param apiContainerImage string = 'ghcr.io/southworks/cohereloan-api:demo'

@description('Full container image URI for the MCP host.')
param mcpContainerImage string = 'ghcr.io/southworks/cohereloan-mcp:demo'

@description('Full container image URI for the agent provisioning job.')
param provisioningContainerImage string = 'ghcr.io/southworks/cohereloan-provisioning:demo'

var resourceTags = {
  project: 'inesite'
}

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = toLower(take(replace('${baseName}st${uniqueSuffix}', '-', ''), 24))
var foundryAccountName = toLower(take(replace('${baseName}foundry${uniqueSuffix}', '-', ''), 24))
var searchServiceName = toLower(take(replace('${baseName}search${uniqueSuffix}', '-', ''), 60))
var projectName = '${baseName}-project'
var logAnalyticsName = take('${baseName}-logs-${uniqueSuffix}', 63)
var containerAppsEnvironmentName = take('${baseName}-cae-${uniqueSuffix}', 63)
var apiAppName = take('${baseName}-api-${uniqueSuffix}', 32)
var mcpAppName = take('${baseName}-mcp-${uniqueSuffix}', 32)
var provisioningJobName = take('${baseName}-provision-${uniqueSuffix}', 32)
var foundryEndpointBase = 'https://${foundryAccount.properties.customSubDomainName}.services.ai.azure.com'
var embedEndpoint = '${foundryEndpointBase}/openai/deployments/${embedDeploymentName}'
var rerankEndpoint = '${foundryEndpointBase}/openai/deployments/${rerankDeploymentName}'
var foundryProjectEndpoint = '${foundryEndpointBase}/api/projects/${foundryProject.name}'

resource resourceGroupTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: resourceTags
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: resourceTags
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
  tags: resourceTags
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

resource embedModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundryAccount
  name: embedDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'Cohere'
      name: embedModelName
      version: embedModelVersion
    }
  }
}

resource rerankModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundryAccount
  name: rerankDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'Cohere'
      name: rerankModelName
      version: rerankModelVersion
    }
  }
}

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  tags: resourceTags
  sku: {
    name: searchSku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: resourceTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
  location: location
  tags: resourceTags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-api-identity-${uniqueSuffix}'
  location: location
  tags: resourceTags
}

resource mcpIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-mcp-identity-${uniqueSuffix}'
  location: location
  tags: resourceTags
}

resource provisioningIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-provision-identity-${uniqueSuffix}'
  location: location
  tags: resourceTags
}

resource apiStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiIdentity.id, 'StorageBlobDataContributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, apiIdentity.id, 'CognitiveServicesUser')
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97aa65c-624c-424c-a56f-59ff10e1e8ce')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpSearchContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, mcpIdentity.id, 'SearchServiceContributor')
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-b645b4fd2350')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpSearchDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, mcpIdentity.id, 'SearchIndexDataContributor')
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8eb59f1d-7bff-47a0-b4d9-b645b4fd2350')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, mcpIdentity.id, 'CognitiveServicesUser')
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97aa65c-624c-424c-a56f-59ff10e1e8ce')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource provisioningFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, provisioningIdentity.id, 'CognitiveServicesContributor')
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '25c0f9b0-23c6-4788-b26e-7b7928663388')
    principalId: provisioningIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: mcpAppName
  location: location
  tags: resourceTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${mcpIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'mcp'
          image: mcpContainerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AzureSearch__Endpoint', value: 'https://${searchService.name}.search.windows.net' }
            { name: 'AzureSearch__EvidenceIndexName', value: 'loan-case-evidence' }
            { name: 'AzureSearch__PolicyIndexName', value: 'loan-policy-knowledge' }
            { name: 'AzureSearch__VectorDimensions', value: '1024' }
            { name: 'AzureFoundryModels__EmbedDeploymentName', value: embedDeploymentName }
            { name: 'AzureFoundryModels__RerankDeploymentName', value: rerankDeploymentName }
            { name: 'AzureFoundryModels__EmbedModelName', value: embedModelName }
            { name: 'AzureFoundryModels__RerankModelName', value: rerankModelName }
            { name: 'AzureFoundryModels__EmbedEndpoint', value: embedEndpoint }
            { name: 'AzureFoundryModels__RerankEndpoint', value: rerankEndpoint }
            { name: 'AzureFoundryModels__EmbeddingDimensions', value: '1024' }
            { name: 'Dataset__RootPath', value: '/app/dataset-seed' }
            { name: 'Dataset__PolicyFilePath', value: '/app/dataset-seed/08_policy_rag/general_policy.txt' }
            { name: 'AZURE_CLIENT_ID', value: mcpIdentity.properties.clientId }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  tags: resourceTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiContainerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AZURE_FOUNDRY_PROJECT_ENDPOINT', value: foundryProjectEndpoint }
            { name: 'AZURE_STORAGE_BLOB_SERVICE_URI', value: storageAccount.properties.primaryEndpoints.blob }
            { name: 'AzureStorage__ContainerName', value: documentsContainerName }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'AZURE_CLIENT_ID', value: apiIdentity.properties.clientId }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

var mcpBaseUrl = 'https://${mcpApp.properties.configuration.ingress.fqdn}'

resource documentRetrievalConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'document-retrieval-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: '${mcpBaseUrl}/document-retrieval/mcp'
  }
  dependsOn: [
    mcpApp
  ]
}

resource underwritingRulesConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'underwriting-rules-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: '${mcpBaseUrl}/underwriting-rules/mcp'
  }
  dependsOn: [
    mcpApp
  ]
}

resource policyKnowledgeConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'policy-knowledge-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: '${mcpBaseUrl}/policy-knowledge/mcp'
  }
  dependsOn: [
    mcpApp
  ]
}

resource loanSetupConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: foundryProject
  name: 'loan-setup-mcp'
  properties: {
    category: 'GenericHttp'
    authType: 'None'
    target: '${mcpBaseUrl}/loan-setup/mcp'
  }
  dependsOn: [
    mcpApp
  ]
}

resource provisioningJob 'Microsoft.App/jobs@2024-03-01' = {
  name: provisioningJobName
  location: location
  tags: resourceTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${provisioningIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        replicaCompletionCount: 1
        parallelism: 1
      }
    }
    template: {
      containers: [
        {
          name: 'agent-provisioning'
          image: provisioningContainerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AZURE_FOUNDRY_PROJECT_ENDPOINT', value: foundryProjectEndpoint }
            { name: 'AZURE_FOUNDRY_PROJECT_RESOURCE_ID', value: foundryProject.id }
            { name: 'ProjectEndpoint', value: foundryProjectEndpoint }
            { name: 'FoundryProjectResourceId', value: foundryProject.id }
            { name: 'ModelDeploymentName', value: modelDeploymentName }
            { name: 'MemoryStoreName', value: memoryStoreName }
            { name: 'MCP_BASE_URL', value: mcpBaseUrl }
            { name: 'AZURE_CLIENT_ID', value: provisioningIdentity.properties.clientId }
          ]
        }
      ]
    }
  }
  dependsOn: [
    documentRetrievalConnection
    underwritingRulesConnection
    policyKnowledgeConnection
    loanSetupConnection
    provisioningFoundryRole
  ]
}

resource deploymentScriptIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-deployscript-${uniqueSuffix}'
  location: location
  tags: resourceTags
}

resource deploymentScriptContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deploymentScriptIdentity.id, 'Contributor')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: deploymentScriptIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource runProvisioningScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'run-agent-provisioning-${uniqueSuffix}'
  location: location
  tags: resourceTags
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${deploymentScriptIdentity.id}': {}
    }
  }
  properties: {
    azCliVersion: '2.62.0'
    timeout: 'PT45M'
    retentionInterval: 'PT1H'
    cleanupPreference: 'OnSuccess'
    forceUpdateTag: uniqueSuffix
    scriptContent: '''
      set -euo pipefail
      echo "Waiting briefly for role assignment propagation..."
      sleep 60
      echo "Starting agent provisioning job..."
      EXECUTION=$(az containerapp job start --name "${PROVISIONING_JOB_NAME}" --resource-group "${RESOURCE_GROUP}" --query name -o tsv)
      echo "Job execution: ${EXECUTION}"

      for i in $(seq 1 120); do
        STATUS=$(az containerapp job execution show \
          --name "${PROVISIONING_JOB_NAME}" \
          --resource-group "${RESOURCE_GROUP}" \
          --job-execution-name "${EXECUTION}" \
          --query properties.status -o tsv)

        echo "Provisioning job status: ${STATUS}"

        if [ "${STATUS}" = "Succeeded" ]; then
          echo "Agent provisioning completed successfully."
          exit 0
        fi

        if [ "${STATUS}" = "Failed" ]; then
          echo "Agent provisioning job failed."
          exit 1
        fi

        sleep 15
      done

      echo "Timed out waiting for agent provisioning job."
      exit 1
    '''
    environmentVariables: [
      {
        name: 'RESOURCE_GROUP'
        value: resourceGroup().name
      }
      {
        name: 'PROVISIONING_JOB_NAME'
        value: provisioningJobName
      }
    ]
  }
  dependsOn: [
    provisioningJob
    deploymentScriptContributorRole
    apiApp
    mcpApp
  ]
}

output storageAccountName string = storageAccount.name
output blobServiceUri string = storageAccount.properties.primaryEndpoints.blob
output documentsContainerName string = documentsContainerName
output foundryAccountName string = foundryAccount.name
output foundryProjectName string = foundryProject.name
output foundryProjectEndpoint string = foundryProjectEndpoint
output foundryProjectResourceId string = foundryProject.id
output modelDeploymentName string = modelDeploymentName
output embedDeploymentName string = embedDeploymentName
output embedModelName string = embedModelName
output rerankDeploymentName string = rerankDeploymentName
output rerankModelName string = rerankModelName
output memoryStoreName string = memoryStoreName
output searchServiceName string = searchService.name
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'
output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output mcpUrl string = mcpBaseUrl
output provisioningJobName string = provisioningJob.name
