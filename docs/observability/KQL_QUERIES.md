# KQL Query Pack

## 1) Timeline por case

```kusto
let caseId = "APP-001";
union traces, requests, dependencies, exceptions, customEvents
| where timestamp > ago(24h)
| where tostring(customDimensions["case.id"]) == caseId
| project timestamp, itemType, operation_Id, operation_ParentId, name, message, severityLevel, customDimensions
| order by timestamp asc
```

## 2) Correlacion end-to-end por operation id

```kusto
let opId = "<operation-id>";
union requests, dependencies, traces, exceptions
| where operation_Id == opId
| project timestamp, itemType, operation_Id, operation_ParentId, name, resultCode, success, duration, message, customDimensions
| order by timestamp asc
```

## 3) Metricas custom del workflow

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in (
    "loan.workflow.started",
    "loan.workflow.completed",
    "loan.workflow.failed",
    "loan.workflow.awaiting_human_review",
    "loan.workflow.stage.duration.ms",
    "loan.workflow.agent.duration.ms",
    "loan.mcp.tool.duration.ms",
    "loan.hosted.request.duration.ms")
| project timestamp, name, value, customDimensions
| order by timestamp desc
```

## 4) Validacion de tags enriquecidos MCP

```kusto
dependencies
| where timestamp > ago(24h)
| where tostring(customDimensions["agent.name"]) contains "agent"
    or tostring(customDimensions["agent.role"]) != ""
    or tostring(customDimensions["case.id"]) != ""
| project timestamp, name, operation_Id,
          caseId = tostring(customDimensions["case.id"]),
          agentRole = tostring(customDimensions["agent.role"]),
          agentName = tostring(customDimensions["agent.name"]),
          workflowRunId = tostring(customDimensions["workflow.run_id"]),
          foundryRunId = tostring(customDimensions["foundry.run_id"]),
          foundryThreadId = tostring(customDimensions["foundry.thread_id"]),
          executionMode = tostring(customDimensions["execution_mode"])
| order by timestamp desc
```

## 5) Duracion por etapa de workflow

```kusto
customMetrics
| where timestamp > ago(24h)
| where name == "loan.workflow.stage.duration.ms"
| summarize avgMs = avg(value), p95Ms = percentile(value, 95), count() by stage = tostring(customDimensions["workflow.stage"])
| order by avgMs desc
```

## 6) Duracion por agente

```kusto
customMetrics
| where timestamp > ago(24h)
| where name == "loan.workflow.agent.duration.ms"
| summarize avgMs = avg(value), p95Ms = percentile(value, 95), count() by
    agentRole = tostring(customDimensions["agent.role"]),
    agentName = tostring(customDimensions["agent.name"])
| order by avgMs desc
```
