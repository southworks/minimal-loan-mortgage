# Alexander Ward — borderline credit and affordability

- **Case id:** `case-20`
- **Legacy application id:** `APP-020`
- **Expected outcome:** `manual_review`
- **Primary reason:** `borderline_credit_and_affordability`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `low_score_or_high_dti`
- `unexplained_deposits`
- `insufficient_appraisal_for_ltv`
