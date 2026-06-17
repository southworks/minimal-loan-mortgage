param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath = "agent-provisioning/config/provisioning.local.json"
)

$ErrorActionPreference = "Stop"

dotnet run --project agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
    --config $ConfigPath `
    --agents agent-provisioning/agents

if ($LASTEXITCODE -ne 0) {
    throw "Agent provisioning failed."
}
