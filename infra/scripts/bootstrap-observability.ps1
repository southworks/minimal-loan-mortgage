param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [string]$ApplicationInsightsName,
    [string]$ApiContainerAppName,
    [string]$McpContainerAppName
)

$ErrorActionPreference = "Stop"

function Get-RequiredTool {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' was not found in PATH."
    }
}

function Resolve-AppInsightsName {
    param(
        [string]$ResourceGroup,
        [string]$ExplicitName
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitName)) {
        return $ExplicitName
    }

    $allNames = az resource list --resource-group $ResourceGroup --resource-type "microsoft.insights/components" --query "[].name" -o tsv
    if (-not $allNames) {
        throw "No Application Insights component found in resource group '$ResourceGroup'."
    }

    $resolved = ($allNames -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    if (-not $resolved) {
        throw "No Application Insights component found in resource group '$ResourceGroup'."
    }

    return $resolved.Trim()
}

function Resolve-ContainerAppName {
    param(
        [string]$ResourceGroup,
        [string]$ExplicitName,
        [string]$SuffixHint
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitName)) {
        return $ExplicitName
    }

    $allNames = az containerapp list --resource-group $ResourceGroup --query "[].name" -o tsv
    if (-not $allNames) {
        throw "No Container Apps found in resource group '$ResourceGroup'."
    }

    $resolved = ($allNames -split "`n" |
        Where-Object { $_ -match $SuffixHint } |
        Select-Object -First 1)

    if (-not $resolved) {
        throw "Could not resolve Container App matching '$SuffixHint' in resource group '$ResourceGroup'."
    }

    return $resolved.Trim()
}

function Set-ContainerAppTelemetrySecret {
    param(
        [string]$ResourceGroup,
        [string]$ContainerAppName,
        [string]$ConnectionString
    )

    # Store connection string as a Container App secret and bind env var through secretref
    # to avoid persisting plain text values in container environment settings.
    az containerapp update `
        --name $ContainerAppName `
        --resource-group $ResourceGroup `
        --set-secrets "appinsights-connection-string=$ConnectionString" `
        --set-env-vars "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-connection-string" `
        --output none
}

Get-RequiredTool -Name "az"

Write-Host "Resolving Application Insights component..."
$appInsights = Resolve-AppInsightsName -ResourceGroup $ResourceGroupName -ExplicitName $ApplicationInsightsName

Write-Host "Reading APPLICATIONINSIGHTS_CONNECTION_STRING from '$appInsights'..."
$connectionString = az monitor app-insights component show `
    --resource-group $ResourceGroupName `
    --app $appInsights `
    --query "connectionString" `
    -o tsv

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "Application Insights connection string could not be resolved from '$appInsights'."
}

$apiApp = Resolve-ContainerAppName -ResourceGroup $ResourceGroupName -ExplicitName $ApiContainerAppName -SuffixHint "-api-"
$mcpApp = Resolve-ContainerAppName -ResourceGroup $ResourceGroupName -ExplicitName $McpContainerAppName -SuffixHint "-mcp-"

Write-Host "Configuring telemetry secret for Container App '$apiApp'..."
Set-ContainerAppTelemetrySecret `
    -ResourceGroup $ResourceGroupName `
    -ContainerAppName $apiApp `
    -ConnectionString $connectionString

Write-Host "Configuring telemetry secret for Container App '$mcpApp'..."
Set-ContainerAppTelemetrySecret `
    -ResourceGroup $ResourceGroupName `
    -ContainerAppName $mcpApp `
    -ConnectionString $connectionString

Write-Host "Observability bootstrap completed successfully."
Write-Host "  Resource Group: $ResourceGroupName"
Write-Host "  Application Insights: $appInsights"
Write-Host "  API Container App: $apiApp"
Write-Host "  MCP Container App: $mcpApp"
