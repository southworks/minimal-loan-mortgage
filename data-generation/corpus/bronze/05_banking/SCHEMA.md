# 05 Banking Schema

Summarized bank statement with enough signal to detect deposits and cash flow consistency.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-BNK-001",
  "document_type": "bank_statement",
  "document_date": "2026-03-28",
  "source_system": "bank_statement",
  "party_role": "borrower",
  "statement_month": "2026-03",
  "owner_name": "Olivia Bennett",
  "beginning_balance": 18250.10,
  "ending_balance": 24110.42,
  "average_balance": 21190.75,
  "total_deposits": 11500.00,
  "large_deposits": [],
  "transactions": [
    {
      "transaction_date": "2026-03-15",
      "description": "Payroll",
      "amount": 5750.00,
      "category": "payroll"
    }
  ]
}
```

## Required fields

- `party_role`
- `statement_month`
- `owner_name`
- `beginning_balance`
- `ending_balance`
- `average_balance`
- `total_deposits`
- `large_deposits`
- `transactions`

## Required transaction fields

- `transaction_date`
- `description`
- `amount`
- `category`
