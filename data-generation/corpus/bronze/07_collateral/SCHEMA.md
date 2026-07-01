# 07 Collateral Schema

This folder contains two document types: `property_details` and `appraisal`.

## Sample of required fields for property_details

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-COL-PRP-001",
  "document_type": "property_details",
  "document_date": "2026-03-23",
  "source_system": "property_record",
  "property_address": "2211 Birch Court, Denver, CO 80211",
  "property_type": "single_family",
  "occupancy_type": "primary_residence",
  "purchase_price": 520000
}
```

## Required fields for property_details

- `property_address`
- `property_type`
- `occupancy_type`
- `purchase_price`

## Sample of required fields for appraisal

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-COL-APR-001",
  "document_type": "appraisal",
  "document_date": "2026-03-15",
  "source_system": "appraisal_report",
  "property_address": "2211 Birch Court, Denver, CO 80211",
  "property_type": "single_family",
  "occupancy_type": "primary_residence",
  "purchase_price": 520000,
  "appraised_value": 535000,
  "calculated_ltv": 72.9
}
```

## Required fields for appraisal

- `property_address`
- `property_type`
- `occupancy_type`
- `purchase_price`
- `appraised_value`
- `calculated_ltv`

## Optional with real MVP value

- `condition_rating`
