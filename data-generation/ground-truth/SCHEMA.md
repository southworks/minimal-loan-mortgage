# 09 Decision Ground Truth Schema

Expected case decision for evaluation and demo purposes.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-DEC-001",
  "document_type": "decision_ground_truth",
  "document_date": "2026-03-23",
  "source_system": "underwriting_ground_truth",
  "expected_decision": "approve",
  "primary_reason": "meets_credit_income_collateral_policy",
  "required_human_review": false,
  "risk_flags": [],
  "top_policy_refs": [
    "UW-100",
    "CR-210",
    "CL-125"
  ],
  "summary_explanation": "Application meets income, credit, and collateral guidelines."
}
```

## Required fields

- `expected_decision`
- `primary_reason`
- `required_human_review`
- `risk_flags`
- `top_policy_refs`
- `summary_explanation`

## Optional with real MVP value

- `secondary_reasons`
