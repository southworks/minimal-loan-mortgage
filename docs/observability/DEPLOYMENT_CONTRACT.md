# Observability Deployment Contract

## Required variables
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Auto-configured in infrastructure
From `infra/main.bicep` orchestration and `infra/modules/*` modules:
- App Insights resource is provisioned and linked to Log Analytics.
- Foundry project is connected to App Insights through `infra/modules/foundry-appinsights-connection.bicep`.
- The App Insights connection string is passed to Foundry connection/workload modules via secure module parameters.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` is injected into:
  - API Container App
  - MCP Container App

## Post-deploy recovery bootstrap (optional)
Script: `infra/scripts/bootstrap-observability.ps1`

Purpose:
- Recover telemetry wiring if configuration drift occurs after deployment.
- Resolve App Insights connection string from resource group.
- Store the value as a Container App secret and set `APPLICATIONINSIGHTS_CONNECTION_STRING` through `secretref:`.

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
- Standard deployment passes the connection string via secure module parameters.
- Recovery bootstrap stores the connection string as Container App secret and binds env vars via `secretref:`.
