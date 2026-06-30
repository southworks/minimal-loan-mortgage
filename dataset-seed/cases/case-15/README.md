# Sofia Alvarez — income document mismatch

- **Case id:** `case-15`
- **Legacy application id:** `APP-015`
- **Expected outcome:** `deny`
- **Primary reason:** `income_document_mismatch`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `income_mismatch`
