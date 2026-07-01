# Format decisions — Raw layer extensions

Analysis and rationale for which formats each document type receives. Bronze JSON remains the single source of truth; all outputs are generated deterministically.

## Decision summary

| Document type | Add PDF? | Add PNG? | Decision |
|---------------|:--------:|:--------:|----------|
| Bank statement | **Yes** | **Yes** | E-statements are PDF; phone photos of paper statements are a common upload path. Needed for cash-flow / large-deposit demos (e.g. APP-018). |
| Property appraisal | **Yes** | **Yes** | Appraisers deliver multi-page PDF reports; PNG covers a photographed summary page (mobile upload). Needed for LTV demos (e.g. APP-016). |
| Employment VOE | *(exists)* | **Yes** | HR letters are often faxed or scanned to PNG/PDF; PNG adds a vision/OCR path alongside the existing PDF letter. |
| Credit report | No | **No** | Lender bureau pull only — not borrower-submitted; PNG scan would be unrealistic. |
| Loan application | No | **No** | Born-digital LOS submission; PDF already exists under `loan_amount/`. |
| Declared income summary | No | **No** | System-generated LOS sheet; not a physical scan. |
| Personal information form | No | **No** | Tabular PDF; identity scan is already PNG under `personal_details/`. |
| Paystub / Identity | No change | No change | Already txt + pdf/png as appropriate. |

## Per-type rationale

### Bank statement (`05_banking/`)

**Formats:** txt (existing) + **pdf** + **png**

| Format | Why |
|--------|-----|
| TXT | Baseline text extraction; matches `generate_raw_layer.py` output. |
| PDF | Banks issue monthly e-statements as PDF downloads — most common digital format. |
| PNG | Borrowers often photograph a printed statement or upload a mobile screenshot. Exercises vision models on tabular transactions. |

**Output path:** `00_raw/{pdf,png}/APP-XXX/banking/bank_statement_{1,2,3}.{pdf,png}`

**Not generated:** Credit-side only data stays out of borrower uploads.

---

### Property appraisal (`07_collateral/` `_appraisal.json`)

**Formats:** txt (existing) + **pdf** + **png**

| Format | Why |
|--------|-----|
| TXT | Quick parsing baseline for valuation fields. |
| PDF | Standard delivered format from licensed appraisers (USPAP report). |
| PNG | Summary / cover page photo — common when only the valuation section is uploaded from a phone. |

**Output path:** `00_raw/{pdf,png}/APP-XXX/collateral/appraisal.{pdf,png}`

**Not generated for `property_details.json`:** Address and purchase price already appear in loan application and appraisal; a separate property record PDF would duplicate data without a distinct ingestion scenario.

---

### Employment verification (`04_employment/`)

**Formats:** txt + pdf (existing) + **png** (new)

| Format | Why |
|--------|-----|
| PNG | Employers often return a signed letter via fax or email scan; PNG is the typical artifact before PDF normalization in legacy workflows. |

**Output path:** `00_raw/png/APP-XXX/employment/employment_verification.png`

---

### Credit report (`06_credit/`) — no change

**Formats:** pdf only

Bureau delivers structured data + PDF to the lender. Borrowers do not upload credit files. A PNG “scan” would misrepresent the data lineage and blur lender vs borrower document boundaries in demos.

---

### Loan application & declared income — no change

- **Loan application:** TXT + PDF (`loan_amount/loan_application_summary.pdf`) sufficient for form vs text demos.
- **Declared income summary:** PDF only — internal LOS artifact.

---

## Implementation

| Addition | Script | Function |
|----------|--------|----------|
| Bank statement PDF/PNG | `generate_agent_documents.py` | `generate_banking()` |
| Appraisal PDF/PNG | `generate_agent_documents.py` | `generate_collateral()` |
| VOE PNG | `generate_agent_documents.py` | `generate_employment()` (extended) |

After implementation, per-application pdf/png count rises from **~10 to ~19** files (× 20 apps ≈ **380** pdf/png files total).

Regenerate:

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
pip install -r requirements.txt
python3 generate_agent_documents.py
```
