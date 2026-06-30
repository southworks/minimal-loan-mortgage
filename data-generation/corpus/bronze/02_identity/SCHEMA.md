# 02 Identity Schema

Identity document for the borrower or co-borrower.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-ID-001",
  "document_type": "drivers_license",
  "document_date": "2026-03-15",
  "source_system": "identity_document",
  "party_role": "borrower",
  "full_name": "Olivia Bennett",
  "date_of_birth": "1988-04-12",
  "address": "114 Cedar Lane, Denver, CO 80206",
  "document_number": "D123456"
}
```

## Required fields

- `party_role`
- `full_name`
- `date_of_birth`
- `address`
- `document_number`

## Optional with real MVP value

- `expiry_date`
