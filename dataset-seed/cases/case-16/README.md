# Jacob Miller — ltv above policy limit

- **Case id:** `case-16`
- **Legacy application id:** `APP-016`
- **Expected outcome:** `deny`
- **Primary reason:** `ltv_above_policy_limit`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `insufficient_appraisal_for_ltv`
