# Amelia Price — address mismatch requires review

- **Case id:** `case-17`
- **Legacy application id:** `APP-017`
- **Expected outcome:** `manual_review`
- **Primary reason:** `address_mismatch_requires_review`

## Ingest

Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).
The API workflow starts from these files.

## Fabric prerequisite data

Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.

## Deliberate signals

- `address_mismatch`
