param(
    [string]$ResourceGroupName = 'cohere-loan-demo-rg',
    [string]$WorkspaceId = '',
    [string]$LakehouseId = '',
    [string]$LakehouseName = '',
    [string]$DatasetSeedPath = '',
    [string]$OneLakeEndpoint = 'https://onelake.dfs.fabric.microsoft.com',
    [switch]$SkipRaw,
    [switch]$SkipStructured,
    [switch]$SkipPolicy,
    [switch]$RequireFabricIds
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($WorkspaceId)) {
    $WorkspaceId = $env:FABRIC_WORKSPACE_ID
}
if ([string]::IsNullOrWhiteSpace($LakehouseId)) {
    $LakehouseId = $env:FABRIC_LAKEHOUSE_ID
}

Write-Host '=== Fabric dataset seed orchestrator ==='
Write-Host "Resource group: $ResourceGroupName"
Write-Host "WorkspaceId: $WorkspaceId"
Write-Host "LakehouseId: $LakehouseId"
Write-Host "LakehouseName: $LakehouseName"
Write-Host "OneLake endpoint: $OneLakeEndpoint"

if ([string]::IsNullOrWhiteSpace($WorkspaceId) -or [string]::IsNullOrWhiteSpace($LakehouseId)) {
    $message = 'WorkspaceId/LakehouseId are required for Fabric seeding. Provide -WorkspaceId and -LakehouseId (or FABRIC_WORKSPACE_ID/FABRIC_LAKEHOUSE_ID).'

    if ($RequireFabricIds) {
        throw $message
    }

    Write-Warning $message
    Write-Host 'Skipping seed as best-effort because Fabric IDs were not provided.'
    exit 0
}

if (-not $SkipRaw) {
    & (Join-Path $PSScriptRoot 'seed-fabric-raw.ps1') `
        -WorkspaceId $WorkspaceId `
        -LakehouseId $LakehouseId `
        -DatasetSeedPath $DatasetSeedPath `
        -OneLakeEndpoint $OneLakeEndpoint

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipStructured) {
    & (Join-Path $PSScriptRoot 'seed-fabric-structured-files.ps1') `
        -WorkspaceId $WorkspaceId `
        -LakehouseId $LakehouseId `
        -DatasetSeedPath $DatasetSeedPath `
        -OneLakeEndpoint $OneLakeEndpoint

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipPolicy) {
    $policyParams = @{
        WorkspaceId = $WorkspaceId
        LakehouseId = $LakehouseId
        DatasetSeedPath = $DatasetSeedPath
        OneLakeEndpoint = $OneLakeEndpoint
    }

    & (Join-Path $PSScriptRoot 'seed-fabric-policy.ps1') @policyParams
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host 'Fabric dataset seed completed.'
exit 0
