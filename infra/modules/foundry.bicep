param location string
param resourceTags object
param foundryAccountName string
param resolvedFoundryProjectName string
param modelDeploymentName string
param modelDeploymentSkuName string
param modelDeploymentCapacity int
param cohereModelName string
param cohereModelVersion string
param embedDeploymentName string
param embedModelName string
param embedModelVersion string
param embedDeploymentCapacity int
param rerankDeploymentName string
param rerankModelName string
param rerankModelVersion string
param rerankDeploymentCapacity int

module foundryAccountModule 'foundry-account.bicep' = {
  name: 'foundry-account'
  params: {
    location: location
    resourceTags: resourceTags
    foundryAccountName: foundryAccountName
  }
}

module foundryWorkloadModule 'foundry-workload.bicep' = {
  name: 'foundry-workload'
  params: {
    location: location
    foundryAccountName: foundryAccountModule.outputs.foundryAccountName
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

output foundryAccountName string = foundryAccountModule.outputs.foundryAccountName
output foundryAccountId string = foundryAccountModule.outputs.foundryAccountId
output foundryProjectName string = foundryWorkloadModule.outputs.foundryProjectName
output foundryProjectResourceId string = foundryWorkloadModule.outputs.foundryProjectResourceId
output foundryProjectPrincipalId string = foundryWorkloadModule.outputs.foundryProjectPrincipalId
output foundryProjectEndpoint string = foundryWorkloadModule.outputs.foundryProjectEndpoint
output embedEndpoint string = foundryWorkloadModule.outputs.embedEndpoint
output rerankEndpoint string = foundryWorkloadModule.outputs.rerankEndpoint
output modelDeploymentName string = foundryWorkloadModule.outputs.modelDeploymentName
output embedDeploymentName string = foundryWorkloadModule.outputs.embedDeploymentName
output embedModelName string = foundryWorkloadModule.outputs.embedModelName
output rerankDeploymentName string = foundryWorkloadModule.outputs.rerankDeploymentName
output rerankModelName string = foundryWorkloadModule.outputs.rerankModelName
