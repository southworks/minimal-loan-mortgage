# Agent Input Documents — PDF & PNG

PDF and PNG outputs live under **`00_raw/pdf/`** and **`00_raw/png/`**, alongside txt in **`00_raw/txt/`**.

`00_raw/` is gitignored — regenerate after clone (see **Version control** in [RAW_LAYER.md](RAW_LAYER.md)).

- **Full format matrix:** [Format strategy](RAW_LAYER.md#format-strategy) in `RAW_LAYER.md`
- **Per-type decisions and rationale:** [FORMAT_DECISIONS.md](FORMAT_DECISIONS.md)

## Category mapping (demo groupings)

| Category | Primary Bronze source | PDF (`00_raw/pdf/`) | PNG (`00_raw/png/`) |
|----------|----------------------|---------------------|---------------------|
| **Personal details** | `01_application/` + `02_identity/` | `personal_details/personal_information.pdf` | `personal_details/drivers_license.png` |
| **Income** | `03_income/` + `declared_financials` | `income/declared_income_summary.pdf`, `paystub_*.pdf` | `income/paystub_*.png` |
| **Employment** | `04_employment/` | `employment/employment_verification.pdf` | `employment/employment_verification.png` |
| **Banking** | `05_banking/` | `banking/bank_statement_*.pdf` (×3) | `banking/bank_statement_*.png` (×3) |
| **Collateral** | `07_collateral/` (`_appraisal`) | `collateral/appraisal.pdf` | `collateral/appraisal.png` |
| **Credit history** | `06_credit/` | `credit_history/credit_report.pdf` | — (bureau pull; see FORMAT_DECISIONS.md) |
| **Loan amount** | `01_application/` | `loan_amount/loan_application_summary.pdf` | — |

TXT equivalents for all borrower-submitted docs are under `00_raw/txt/` — see [Document types](RAW_LAYER.md#document-types).

## Generate

```bash
cd data-generation/scripts
pip install -r requirements.txt
python3 generate_agent_documents.py              # all 20 apps
python3 generate_agent_documents.py --app APP-001
python3 generate_agent_documents.py --formats pdf
python3 generate_agent_documents.py --formats png
```

Re-run after editing Bronze JSON. For txt files, also run `python3 generate_raw_layer.py`.

## Example layout

```
00_raw/
├── txt/APP-001/bank_statement_1.txt
├── pdf/APP-001/banking/bank_statement_1.pdf
├── pdf/APP-001/collateral/appraisal.pdf
└── png/APP-001/banking/bank_statement_1.png
```
