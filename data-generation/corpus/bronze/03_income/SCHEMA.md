# 03 Income Schema

Income document, typically a paystub.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-INC-001",
  "document_type": "paystub",
  "document_date": "2026-02-23",
  "source_system": "payroll_document",
  "party_role": "borrower",
  "employee_name": "Olivia Bennett",
  "employer": "Frontier Health Systems",
  "pay_date": "2026-02-23",
  "gross_pay": 5307.69,
  "net_pay": 3920.11,
  "pay_frequency": "semi_monthly"
}
```

## Required fields

- `party_role`
- `employee_name`
- `employer`
- `pay_date`
- `gross_pay`
- `net_pay`
- `pay_frequency`

## Optional with real MVP value

- `pay_period_start`
- `pay_period_end`
- `ytd_gross`
