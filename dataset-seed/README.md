# Loan & Mortgage Demo Dataset

Demo-ready inputs for the Cohere Loan & Mortgage agentic workflow. Pick an application ID and start the workflow via the API or Blazor UI.

## Quick start

1. **Pick a case** — `APP-001` through `APP-020` (see `case_matrix.json` for borrower summaries)
2. **Start workflow** — `POST /api/loan-mortgage/applications/{caseId}/workflow/basic/start`
3. **MCP / Fabric** — structured bronze JSON in `01_application/` … `07_collateral/`; policies in `08_policy_rag/`

## Highlight cases

| App | Borrower | Outcome | Primary reason |
|-----|----------|---------|----------------|
| APP-001 | Olivia Bennett | approve | clean happy path |
| APP-013 | Emily Reed | deny | credit score below threshold |
| APP-015 | Sofia Alvarez | deny | income document mismatch |
| APP-017 | Amelia Price | manual_review | address mismatch |
| APP-018 | William Cooper | manual_review | unexplained large deposits |

Full scenario definitions: [`../data-generation/scripts/scenarios.py`](../data-generation/scripts/scenarios.py).

## Reference / rebuild

Generation scripts, corpus, and ground truth live in [`../data-generation/`](../data-generation/). Regenerate this folder with `build_dataset_seed.py` (see that README).
