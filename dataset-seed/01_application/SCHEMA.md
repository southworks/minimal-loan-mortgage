# 01 Application Schema

Primary application document.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-APP-001",
  "document_type": "application",
  "document_date": "2026-03-23",
  "source_system": "loan_origination_system",
  "loan_purpose": "purchase",
  "occupancy_type": "primary_residence",
  "loan_product": "fixed_rate_mortgage",
  "loan_term_months": 360,
  "requested_loan_amount": 390000,
  "purchase_price": 520000,
  "down_payment": 130000,
  "borrower": {
    "full_name": "Olivia Bennett",
    "date_of_birth": "1988-04-12",
    "current_address": "114 Cedar Lane, Denver, CO 80206"
  },
  "property": {
    "address": "2211 Birch Court, Denver, CO 80211",
    "city": "Denver",
    "state": "CO",
    "property_type": "single_family"
  },
  "declared_financials": {
    "declared_monthly_income": 11500,
    "existing_monthly_debt": 2100,
    "liquid_assets": 60000
  }
}
```

## Required fields

- `loan_purpose`
- `occupancy_type`
- `loan_product`
- `loan_term_months`
- `requested_loan_amount`
- `purchase_price`
- `down_payment`
- `borrower.full_name`
- `borrower.date_of_birth`
- `borrower.current_address`
- `property.address`
- `property.city`
- `property.state`
- `property.property_type`
- `declared_financials.declared_monthly_income`
- `declared_financials.existing_monthly_debt`
- `declared_financials.liquid_assets`

## Optional with real MVP value

- `co_borrower`
- `interest_rate_estimate`
