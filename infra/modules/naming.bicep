param baseName string

var deploymentSuffix = uniqueString(resourceGroup().id)

output deploymentSuffix string = deploymentSuffix
output foundryAccountName string = toLower(take(replace('${baseName}foundry${deploymentSuffix}', '-', ''), 24))
output searchServiceName string = toLower(take(replace('${baseName}search${deploymentSuffix}', '-', ''), 60))
output documentIntelligenceAccountName string = toLower(take(replace('${baseName}docintel${deploymentSuffix}', '-', ''), 24))
output logAnalyticsName string = take('${baseName}-logs-${deploymentSuffix}', 63)
output containerAppsEnvironmentName string = take('${baseName}-cae-${deploymentSuffix}', 63)
output apiAppName string = endsWith(apiAppNameRaw, '-') ? substring(apiAppNameRaw, 0, length(apiAppNameRaw) - 1) : apiAppNameRaw
output mcpAppName string = endsWith(mcpAppNameRaw, '-') ? substring(mcpAppNameRaw, 0, length(mcpAppNameRaw) - 1) : mcpAppNameRaw
output frontendAppName string = endsWith(frontendAppNameRaw, '-') ? substring(frontendAppNameRaw, 0, length(frontendAppNameRaw) - 1) : frontendAppNameRaw
output policySeedJobName string = endsWith(policySeedJobNameRaw, '-') ? substring(policySeedJobNameRaw, 0, length(policySeedJobNameRaw) - 1) : policySeedJobNameRaw
output provisioningJobName string = endsWith(provisioningJobNameRaw, '-') ? substring(provisioningJobNameRaw, 0, length(provisioningJobNameRaw) - 1) : provisioningJobNameRaw
output apiIdentityName string = '${baseName}-api-identity-${deploymentSuffix}'
output mcpIdentityName string = '${baseName}-mcp-identity-${deploymentSuffix}'
output provisioningIdentityName string = '${baseName}-provision-identity-${deploymentSuffix}'
output deploymentScriptIdentityName string = '${baseName}-deployscript-${deploymentSuffix}'
