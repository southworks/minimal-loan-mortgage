@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for deployed resources.')
param baseName string = 'cohereloan'

@description('Foundry model deployment name used by all agents.')
param modelDeploymentName string = 'cohere-command-a'

@description('SKU used by the Foundry model deployment for the agents. Use GlobalStandard for serverless deployments; use a provisioned SKU only if it is available for the model and region.')
param modelDeploymentSkuName string = 'GlobalStandard'

@minValue(1)
@description('Capacity units for the Foundry model deployment used by the agents. Increase this when agents fail with no_capacity during peak load.')
param modelDeploymentCapacity int = 10

@description('Cohere Command A model name in Foundry catalog.')
param cohereModelName string = 'cohere-command-a'

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
param embedDeploymentCapacity int = 10

@description('Foundry deployment name for Cohere-rerank-v4.0-pro.')
param rerankDeploymentName string = 'cohere-rerank-v4-pro'

@description('Cohere rerank model name in Foundry catalog.')
param rerankModelName string = 'Cohere-rerank-v4.0-pro'

@description('Cohere rerank model version.')
param rerankModelVersion string = '1'

@minValue(1)
@description('Capacity units for the Cohere rerank deployment. Increase this for faster retrieval reranking and fewer throttling failures.')
param rerankDeploymentCapacity int = 5

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

@description('Optional suffix for retry deployments. Set when redeploying after a partial failure left names reserved.')
param nameSuffix string = ''

var resourceTags = {
  project: 'inesite'
}

var deploymentSuffix = empty(nameSuffix) ? uniqueString(resourceGroup().id) : uniqueString(resourceGroup().id, nameSuffix)
var storageAccountName = toLower(take(replace('${baseName}st${deploymentSuffix}', '-', ''), 24))
var foundryAccountName = toLower(take(replace('${baseName}foundry${deploymentSuffix}', '-', ''), 24))
var searchServiceName = toLower(take(replace('${baseName}search${deploymentSuffix}', '-', ''), 60))
var projectName = '${baseName}-project'
var logAnalyticsName = take('${baseName}-logs-${deploymentSuffix}', 63)
var containerAppsEnvironmentName = take('${baseName}-cae-${deploymentSuffix}', 63)
var apiAppName = take('${baseName}-api-${deploymentSuffix}', 32)
var mcpAppName = take('${baseName}-mcp-${deploymentSuffix}', 32)
var provisioningJobName = take('${baseName}-provision-${deploymentSuffix}', 32)
var policySeedJobName = take('${baseName}-policyseed-${deploymentSuffix}', 32)
var foundryEndpointBase = 'https://${foundryAccount.properties.customSubDomainName}.services.ai.azure.com'
var embedEndpoint = '${foundryEndpointBase}/openai/deployments/${embedDeploymentName}'
var rerankEndpoint = foundryEndpointBase
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
    allowProjectManagement: true
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
    name: modelDeploymentSkuName
    capacity: modelDeploymentCapacity
  }
  properties: {
    model: {
      format: 'Cohere'
      name: cohereModelName
      version: cohereModelVersion
    }
  }
  dependsOn: [
    foundryProject
  ]
}

resource embedModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundryAccount
  name: embedDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: embedDeploymentCapacity
  }
  properties: {
    model: {
      format: 'Cohere'
      name: embedModelName
      version: embedModelVersion
    }
  }
  dependsOn: [
    foundryProject
    modelDeployment
  ]
}

resource rerankModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundryAccount
  name: rerankDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: rerankDeploymentCapacity
  }
  properties: {
    model: {
      format: 'Cohere'
      name: rerankModelName
      version: rerankModelVersion
    }
  }
  dependsOn: [
    foundryProject
    embedModelDeployment
  ]
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
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http403'
      }
    }
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
  name: '${baseName}-api-identity-${deploymentSuffix}'
  location: location
  tags: resourceTags
}

resource mcpIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-mcp-identity-${deploymentSuffix}'
  location: location
  tags: resourceTags
}

resource provisioningIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-provision-identity-${deploymentSuffix}'
  location: location
  tags: resourceTags
}

resource apiStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiIdentity.id, 'StorageBlobDataContributor', nameSuffix)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, apiIdentity.id, 'CognitiveServicesUser', nameSuffix)
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    foundryProject
  ]
}

resource apiSearchContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, apiIdentity.id, 'SearchServiceContributor', nameSuffix)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiSearchDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, apiIdentity.id, 'SearchIndexDataContributor', nameSuffix)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpSearchContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, mcpIdentity.id, 'SearchServiceContributor', nameSuffix)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpSearchDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, mcpIdentity.id, 'SearchIndexDataContributor', nameSuffix)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, mcpIdentity.id, 'CognitiveServicesUser', nameSuffix)
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: mcpIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    foundryProject
  ]
}

resource provisioningFoundryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, provisioningIdentity.id, 'CognitiveServicesContributor', nameSuffix)
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68')
    principalId: provisioningIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    foundryProject
    rerankModelDeployment
  ]
}

resource provisioningFoundryDeveloperRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, provisioningIdentity.id, 'FoundryUser', nameSuffix)
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '53ca6127-db72-4b80-b1b0-d745d6d5456d')
    principalId: provisioningIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    foundryProject
    rerankModelDeployment
  ]
}

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
  { name: 'AzureSearch__Endpoint', value: 'https://${searchService.name}.search.windows.net' }
  { name: 'AzureSearch__EvidenceIndexName', value: 'loan-case-evidence' }
  { name: 'AzureSearch__PolicyIndexName', value: 'loan-policy-knowledge' }
  { name: 'AzureSearch__VectorDimensions', value: '1024' }
  { name: 'Dataset__RootPath', value: '/app/dataset-seed' }
  { name: 'Dataset__PolicyFilePath', value: '/app/dataset-seed/08_policy_rag/general_policy.txt' }
  { name: 'AZURE_CLIENT_ID', value: mcpIdentity.properties.clientId }
], mcpFoundryModelEnv)

var policySeedContainerEnv = concat(mcpContainerEnv, [
  { name: 'AzureFoundryModels__MaxRetryAttempts', value: '10' }
  { name: 'AzureFoundryModels__MaxDelaySeconds', value: '60' }
])

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
          env: concat(mcpContainerEnv, [
            { name: 'McpStartup__EnsureSearchIndexesOnStartup', value: 'true' }
            { name: 'McpStartup__SeedPoliciesOnStartup', value: 'false' }
          ])
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
            { name: 'AzureSearch__Endpoint', value: 'https://${searchService.name}.search.windows.net' }
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

resource policySeedJob 'Microsoft.App/jobs@2024-03-01' = {
  name: policySeedJobName
  location: location
  tags: resourceTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${mcpIdentity.id}': {}
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
          name: 'policy-seed'
          image: mcpContainerImage
          command: [
            'dotnet'
            'LoanWorkflow.Mcp.dll'
            '--seed-policies'
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: policySeedContainerEnv
        }
      ]
    }
  }
  dependsOn: [
    mcpSearchContributorRole
    mcpSearchDataRole
    mcpFoundryRole
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
  name: '${baseName}-deployscript-${deploymentSuffix}'
  location: location
  tags: resourceTags
}

resource deploymentScriptContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deploymentScriptIdentity.id, 'Contributor', nameSuffix)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: deploymentScriptIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource runPolicySeedScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'run-policy-seed-${deploymentSuffix}'
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
    forceUpdateTag: deploymentSuffix
    scriptContent: '''
      set -euo pipefail
      az extension add --name containerapp --upgrade 2>/dev/null || true
      echo "Waiting for role assignments and Foundry deployments to settle..."
      sleep 180
      echo "Starting policy seed job..."
      EXECUTION=$(az containerapp job start --name "${POLICY_SEED_JOB_NAME}" --resource-group "${RESOURCE_GROUP}" --query name -o tsv)
      echo "Policy seed job execution: ${EXECUTION}"

      for i in $(seq 1 120); do
        STATUS=$(az containerapp job execution show \
          --name "${POLICY_SEED_JOB_NAME}" \
          --resource-group "${RESOURCE_GROUP}" \
          --job-execution-name "${EXECUTION}" \
          --query properties.status -o tsv)

        echo "Policy seed job status: ${STATUS}"

        if [ "${STATUS}" = "Succeeded" ]; then
          echo "Policy seeding completed successfully."
          exit 0
        fi

        if [ "${STATUS}" = "Failed" ]; then
          echo "Policy seed job failed. Fetching recent job logs..."
          az containerapp job logs show \
            --name "${POLICY_SEED_JOB_NAME}" \
            --resource-group "${RESOURCE_GROUP}" \
            --execution "${EXECUTION}" \
            --container policy-seed \
            --tail 50 || true
          exit 1
        fi

        sleep 15
      done

      echo "Timed out waiting for policy seed job."
      exit 1
    '''
    environmentVariables: [
      {
        name: 'RESOURCE_GROUP'
        value: resourceGroup().name
      }
      {
        name: 'POLICY_SEED_JOB_NAME'
        value: policySeedJobName
      }
    ]
  }
  dependsOn: [
    policySeedJob
    deploymentScriptContributorRole
  ]
}

resource runProvisioningScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'run-agent-provisioning-${deploymentSuffix}'
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
    forceUpdateTag: deploymentSuffix
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
    runPolicySeedScript
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
output policySeedJobName string = policySeedJob.name
output provisioningJobName string = provisioningJob.name
