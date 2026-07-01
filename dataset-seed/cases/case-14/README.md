# Michael Torres — dti above policy limit

- **Case id:** `case-14`
- **Legacy application id:** `APP-014`
- **Expected outcome:** `deny`
- **Primary reason:** `dti_above_policy_limit`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `low_score_or_high_dti`
