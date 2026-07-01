# OpenTelemetry Standardization - Loan & Mortgage Agents

## Scope
This standard applies to:
- API host: `backend/src/Api.Host`
- MCP host: `backend/src/LoanWorkflow.Mcp`
- Hosted agent runtime: `hosted-agents/src/CohereLoanAndMortgage.HostedAgents`

## Cloud-first policy
1. If `APPLICATIONINSIGHTS_CONNECTION_STRING` is present, traces, metrics, and logs are exported to Azure Monitor / Application Insights.
2. If the connection string is not present and environment is `Development`, exporters fallback to console.
3. In `Production`, console fallback is disabled by default.

## Common telemetry schema
### Activity sources
- `CohereLoanAndMortgage.Workflow`
- `CohereLoanAndMortgage.Agents`
- `CohereLoanAndMortgage.Mcp`
- `CohereLoanAndMortgage.HostedAgents`
- Agent Framework sources included:
  - `Microsoft.Agents`
  - `Microsoft.Agents.AI`
  - `Microsoft.Agents.AI.Workflows`
  - `Microsoft.Agents.AI.Foundry`

### Meters
- `CohereLoanAndMortgage.Observability`
- `CohereLoanAndMortgage.HostedAgents`

### Required correlation tags
- `workflow.run_id`
- `foundry.run_id`
- `foundry.thread_id`
- `case.id`
- `agent.role`
- `agent.name`
- `execution_mode`

### Trace instrumentation coverage
- Workflow start/resume/run lifecycle
- In-process workflow execution loop
- Agent executor invocation/completion/failure
- MCP tool execution per endpoint/tool method
- Hosted agent request handling

### Error handling standard
When exceptions occur inside instrumented boundaries:
- span status set to `Error`
- exception attached via `RecordException`

## Correlation propagation standard
### Required outbound headers (orchestration/hosted -> MCP)
- `traceparent`
- `tracestate`
- `X-Case-Id`
- `X-Agent-Role`

### Enrichment in MCP ingress
Incoming requests enrich active Activity with:
- `case.id`
- `agent.role`
- `agent.name`

## Metrics standard
### Counters
- `loan.workflow.started`
- `loan.workflow.completed`
- `loan.workflow.failed`
- `loan.workflow.awaiting_human_review`

### Histograms
- `loan.workflow.stage.duration.ms`
- `loan.workflow.agent.duration.ms`
- `loan.mcp.tool.duration.ms`
- `loan.hosted.request.duration.ms`

### Naming and dimensions
- Prefix all custom business metrics with `loan.`
- Use consistent dimensions from the correlation tag list above
- Keep stage labels stable for dashboard continuity
