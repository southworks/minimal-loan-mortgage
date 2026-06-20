# loan-setup-agent (azd deploy-only)

Deploy **loan-setup-agent** as a Microsoft Hosted Agent into your **existing** Foundry project.

No `azd provision`. After a one-time local config file, deploy is:

```powershell
cd hosted-agents
azd deploy
```

## One-time setup (2 minutes)

1. Sign in:

```powershell
azd auth login
```

2. Create your local deploy config:

```powershell
Copy-Item deploy.local.json.example deploy.local.json
# Edit deploy.local.json with your Foundry project endpoint, model, and MCP host URL
```

Required fields in `deploy.local.json`:

| Field | Example |
|-------|---------|
| `AZURE_AI_PROJECT_ENDPOINT` | `https://<account>.services.ai.azure.com/api/projects/<project>` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | `cohere-command-a` |
| `MCP_BASE_URL` | `https://<mcp-app>.azurecontainerapps.io` (no trailing slash) |

Optional: `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, `AZD_ENVIRONMENT_NAME`, `AZURE_AI_PROJECT_ID`.

3. Deploy:

```powershell
azd deploy
```

The **predeploy hook** will automatically:

- Create the azd environment if missing
- Sync Foundry endpoint and model into azd env
- Create `loan-setup-toolbox` in Foundry if it does not exist (MCP path `/loan-setup/mcp`)

## Local run

```powershell
cd src/loan-setup-agent
Copy-Item .env.example .env
dotnet run
```

Requires .NET 10 SDK. Endpoint: `http://localhost:8088/responses`.

## Verify

```powershell
azd ai agent show --output json
azd ai agent invoke "Summarize loan setup readiness for case APP-001"
```

## Other agents

Source for all four agents lives under `src/`. To deploy another agent, add its service block back to `azure.yaml` (see git history) or copy this pattern with the matching toolbox script.

Shared library: edit `shared/`, then run `scripts/Sync-CommonLibrary.ps1`.
