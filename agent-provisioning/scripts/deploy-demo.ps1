param(
    [string]$ResourceGroupName,
    [string]$Location = "eastus2",
    [string]$ParametersFile = "infra/main.parameters.json"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ResourceGroupName)) {
    throw "ResourceGroupName is required."
}

Write-Host "Deploying infrastructure to resource group '$ResourceGroupName'..."
az group create --name $ResourceGroupName --location $Location | Out-Null

$deployment = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file infra/main.bicep `
    --parameters "@$ParametersFile" `
    --query "properties.outputs" `
    -o json | ConvertFrom-Json

$provisioningConfig = @{
    ProjectEndpoint = $deployment.foundryProjectEndpoint.value
    FoundryProjectResourceId = $deployment.foundryProjectResourceId.value
    ModelDeploymentName = $deployment.modelDeploymentName.value
    MemoryStoreName = $deployment.memoryStoreName.value
} | ConvertTo-Json -Depth 5

$provisioningConfigPath = "agent-provisioning/config/provisioning.local.json"
$provisioningConfig | Set-Content -Path $provisioningConfigPath -Encoding utf8

Write-Host "Wrote provisioning config to $provisioningConfigPath"
Write-Host "Infrastructure deployment completed."

Write-Host "Provisioning Foundry agents..."
dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
    --config $provisioningConfigPath `
    --agents agent-provisioning/agents

if ($LASTEXITCODE -ne 0) {
    throw "Agent provisioning failed."
}

Write-Host "Deployment completed successfully."
Write-Host "Foundry project endpoint: $($deployment.foundryProjectEndpoint.value)"
Write-Host "Blob service URI: $($deployment.blobServiceUri.value)"
