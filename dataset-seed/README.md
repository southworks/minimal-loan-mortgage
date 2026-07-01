# Loan & Mortgage Demo Dataset

Demo-ready inputs for the Cohere Loan & Mortgage agentic workflow. Pick a case id and start the workflow via the API or Blazor UI.

## Quick start

1. **Pick a case** — `case-01` through `case-20` (see `cases/catalog.json`)
2. **Start workflow** — `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/start`
3. **MCP / Fabric** — case-scoped bronze JSON in `cases/{caseId}/fabric-pre-requisite-data/`; shared policies in `policies/`

Each case folder is self-contained:

```
cases/case-01/
  ingest/                       borrower-upload documents (workflow start)
  fabric-pre-requisite-data/    bronze JSON for MCP tools
  README.md
```

## Highlight cases

| Case | Legacy app | Borrower | Outcome |
|------|------------|----------|---------|
| case-01 | APP-001 | Olivia Bennett | approve |
| case-13 | APP-013 | Emily Reed | deny |
| case-15 | APP-015 | Sofia Alvarez | deny |
| case-17 | APP-017 | Amelia Price | manual_review |
| case-18 | APP-018 | William Cooper | manual_review |

Full scenario definitions: [`../data-generation/scripts/scenarios.py`](../data-generation/scripts/scenarios.py).

## Reference / rebuild

Generation scripts, corpus, and ground truth live in [`../data-generation/`](../data-generation/). See [`../data-generation/README.md`](../data-generation/README.md#how-runtime-discovers-scenarios) to regenerate this folder or add a scenario.
