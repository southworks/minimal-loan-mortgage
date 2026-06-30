# Emily Reed — credit score below threshold

- **Case id:** `case-13`
- **Legacy application id:** `APP-013`
- **Expected outcome:** `deny`
- **Primary reason:** `credit_score_below_threshold`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `low_score_or_high_dti`
