# 04 Employment Schema

Employment verification for the borrower or co-borrower.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-EMP-001",
  "document_type": "employment_verification",
  "document_date": "2026-03-23",
  "source_system": "employer_verification",
  "party_role": "borrower",
  "employee_name": "Olivia Bennett",
  "employer": "Frontier Health Systems",
  "employment_status": "full_time",
  "hire_date": "2019-03-18",
  "base_salary_annual": 138000
}
```

## Required fields

- `party_role`
- `employee_name`
- `employer`
- `employment_status`
- `hire_date`
- `base_salary_annual`

## Optional with real MVP value

- `job_title`
- `bonus_annual`
