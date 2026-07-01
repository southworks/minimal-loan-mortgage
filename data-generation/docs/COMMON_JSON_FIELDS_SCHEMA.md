# Common JSON Fields Schema

Common fields for all JSON documents in the dataset.

## Sample of required fields

```json
{
  "application_id": "APP-001",
  "document_id": "APP-001-APP-001",
  "document_type": "application",
  "document_date": "2026-03-23",
  "source_system": "loan_origination_system"
}
```

## Field definitions

- `application_id`: unique identifier for the loan or mortgage case.
- `document_id`: unique identifier for the document within the case.
- `document_type`: document type represented by the JSON file.
- `document_date`: document date in `YYYY-MM-DD` format.
- `source_system`: system or source that produced the document.

## Notes

- `application_id` must exist in all documents for the same case.
- `document_id` must be unique.
- `document_type` should be consistent with the folder where the file is stored.
- `document_date` helps order evidence for retrieval and auditability.
- `source_system` helps explain the origin of the evidence within the flow.
