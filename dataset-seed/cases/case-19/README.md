# Evelyn Ross — employment tenure borderline

- **Case id:** `case-19`
- **Legacy application id:** `APP-019`
- **Expected outcome:** `manual_review`
- **Primary reason:** `employment_tenure_borderline`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `employment_tenure_short`
- `income_mismatch`
- `address_mismatch`
- `low_score_or_high_dti`
