# Observability Deployment Contract

## Required variables
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Auto-configured in infrastructure
From `infra/main.bicep`:
- App Insights resource is provisioned and linked to Log Analytics.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` is injected into:
  - API Container App
  - MCP Container App

## Post-deploy bootstrap
Script: `infra/scripts/bootstrap-observability.ps1`

Purpose:
- Resolve App Insights connection string from resource group
- Inject `APPLICATIONINSIGHTS_CONNECTION_STRING` into API and MCP Container Apps

Usage:

```powershell
pwsh ./infra/scripts/bootstrap-observability.ps1 \
  -ResourceGroupName <rg-name>
```

Optional explicit names:

```powershell
pwsh ./infra/scripts/bootstrap-observability.ps1 \
  -ResourceGroupName <rg-name> \
  -ApplicationInsightsName <appi-name> \
  -ApiContainerAppName <api-ca-name> \
  -McpContainerAppName <mcp-ca-name>
```

## Local development contract
- If `APPLICATIONINSIGHTS_CONNECTION_STRING` is absent and `ASPNETCORE_ENVIRONMENT=Development`, telemetry exports to console.
- In non-Development without connection string, telemetry exporters remain disabled (no console fallback).

## Security contract
- No secrets are hardcoded in source.
- Connection string is provided via environment and/or Azure resource resolution.
