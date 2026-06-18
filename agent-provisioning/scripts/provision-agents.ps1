param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath = "agent-provisioning/config/provisioning.local.json"
)

# Optional local maintenance helper. The standard demo path uses Deploy to Azure instead.

$ErrorActionPreference = "Stop"

dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
    --config $ConfigPath `
    --agents agent-provisioning/agents

if ($LASTEXITCODE -ne 0) {
    throw "Agent provisioning failed."
}
