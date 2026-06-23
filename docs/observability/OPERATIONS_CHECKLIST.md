# Operational Validation Checklist

## Build and startup checks
- [ ] Solution builds successfully.
- [ ] API starts and `/health` returns 200.
- [ ] MCP starts and `/health` returns 200.
- [ ] Hosted agents start with configured model and agent name.

## Exporter behavior checks
- [ ] With `APPLICATIONINSIGHTS_CONNECTION_STRING`, traces/metrics/logs arrive in App Insights.
- [ ] In local Development without connection string, telemetry is visible in console.
- [ ] In Production without connection string, no default console exporter is used.

## End-to-end correlation checks
- [ ] `operation_Id` shows linked telemetry across API workflow, agent execution, and MCP calls.
- [ ] MCP telemetry includes tags: `case.id`, `agent.role`, `agent.name`.
- [ ] Workflow and agent telemetry include tags: `workflow.run_id`, `foundry.run_id`, `foundry.thread_id`, `execution_mode`.

## Business metric checks
- [ ] `loan.workflow.started` increments when workflow starts.
- [ ] `loan.workflow.completed` increments for successful runs.
- [ ] `loan.workflow.failed` increments on failure.
- [ ] `loan.workflow.awaiting_human_review` increments at approval gate.
- [ ] Histograms capture stage and agent durations.

## Deployment automation checks
- [ ] Bicep deployment injects `APPLICATIONINSIGHTS_CONNECTION_STRING` into API and MCP Container Apps.
- [ ] `bootstrap-observability.ps1` resolves App Insights and updates both Container Apps successfully.

## Troubleshooting baseline
- [ ] Missing telemetry: verify environment variable on container app revision.
- [ ] Missing correlation: verify outbound headers and MCP enrichment middleware.
- [ ] Sparse metrics: verify workflow traffic and metric dimensions in queries.
