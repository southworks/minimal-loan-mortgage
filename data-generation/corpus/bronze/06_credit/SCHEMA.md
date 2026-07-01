# 06 Credit Schema

Summarized credit report used by underwriting.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-CRD-001",
  "document_type": "credit_report",
  "document_date": "2026-03-23",
  "source_system": "credit_bureau",
  "party_role": "borrower",
  "borrower_name": "Olivia Bennett",
  "credit_score": 758,
  "credit_utilization": 21,
  "monthly_debt_obligations": 2100,
  "delinquencies_12m": 0,
  "recent_inquiries": 1
}
```

## Required fields

- `party_role`
- `borrower_name`
- `credit_score`
- `credit_utilization`
- `monthly_debt_obligations`
- `delinquencies_12m`
- `recent_inquiries`

## Optional with real MVP value

- `tradelines`
- `collections_flag`
- `bankruptcy_flag`
