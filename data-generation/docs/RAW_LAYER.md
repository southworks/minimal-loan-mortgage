# Raw Layer — FSI Loan & Mortgage Dataset Seed

This document tracks the structure, generation logic, and scenario coverage of the Raw layer (`00_raw/`). Update this file whenever a new scenario or document type is added.

## What is the Raw layer?

The Raw layer contains unstructured documents in **txt**, **pdf**, and **png** formats that simulate materials submitted to or pulled for a loan file. They are inputs to **Document Processing** and downstream agents in the FSI agentic pipeline.

```
Bronze (JSON)  →  generate scripts  →  00_raw/{txt,pdf,png}  →  Agents  →  decisions
```

The Bronze JSON files (`01_application/`, `02_identity/`, etc.) represent what agents **should extract** from the Raw documents. For deny and manual_review cases, deliberate inconsistencies are embedded in the source JSON so generated Raw docs preserve them.

## Folder structure

```
data-generation/
  corpus/
    bronze/                          ← source of truth (01_application … 07_collateral)
    policy_rag/                      ← underwriting policies
    raw/
      txt/                           ← generate_raw_layer.py
        APP-001/
          loan_application.txt
          identity.txt
          paystub_1.txt
          ...
      pdf/                           ← generate_agent_documents.py
      png/                           ← generate_agent_documents.py
  scripts/
    generate_raw_layer.py
    generate_agent_documents.py
    build_dataset_seed.py            → copies corpus into dataset-seed/
    scenarios.py
    requirements.txt                 ← pdf/png only

dataset-seed/                        ← runtime demo package (built output)
  00_raw/                            ← same layout as corpus/raw/
  01_application/ … 08_policy_rag/
  case_matrix.json
```

9 txt files per application × 20 applications = **180 txt files**.  
~11 pdf + ~8 png per application × 20 applications = **~380 pdf/png files**.

See [AGENT_INPUTS.md](AGENT_INPUTS.md) for pdf/png category mapping, [FORMAT_DECISIONS.md](FORMAT_DECISIONS.md) for per-type format rationale, and **Format strategy** below.

## Format strategy

Each document type is emitted in one or more formats to exercise different agent ingestion paths. All formats are derived from the same Bronze JSON and carry the same field values (including deliberate inconsistencies).

| Document type | TXT (`00_raw/txt/`) | PDF (`00_raw/pdf/`) | PNG (`00_raw/png/`) | Bronze source | Rationale |
|---------------|:-------------------:|:-------------------:|:-------------------:|---------------|-----------|
| Loan application (URLA / request) | `loan_application.txt` | `loan_amount/loan_application_summary.pdf` | — | `01_application/` | **TXT** — fast baseline for text-only agents and unit tests. **PDF** — structured form layout, typical of e-signed application packages. |
| Personal information (borrower profile) | *(fields in `loan_application.txt`)* | `personal_details/personal_information.pdf` | — | `01_application/` + `02_identity/` | **PDF** — tabular borrower summary; separates “form data” from the ID scan. |
| Identity (driver's license) | `identity.txt` | — | `personal_details/drivers_license.png` | `02_identity/` | **TXT** — plain-text OCR baseline. **PNG** — simulates a phone/camera scan of a physical ID; exercises vision/OCR models. No PDF: IDs are rarely delivered as born-digital PDFs. |
| Paystub | `paystub_1.txt`, `paystub_2.txt` | `income/paystub_*.pdf` | `income/paystub_*.png` | `03_income/` | **TXT** — simplest extraction path. **PDF** — payroll portal download format. **PNG** — photo/scan of a paper stub. |
| Declared income summary | — | `income/declared_income_summary.pdf` | — | `01_application/` (`declared_financials`) | **PDF only** — LOS-generated summary sheet; not a borrower-uploaded scan. |
| Employment verification (VOE) | `employment_verification.txt` | `employment/employment_verification.pdf` | `employment/employment_verification.png` | `04_employment/` | **TXT** — letter text for parsing tests. **PDF** — signed HR letter on letterhead. **PNG** — fax/scan of signed letter (legacy upload path). |
| Bank statement | `bank_statement_1–3.txt` | `banking/bank_statement_*.pdf` | `banking/bank_statement_*.png` | `05_banking/` | **TXT** — baseline. **PDF** — e-statement download. **PNG** — photo of printed statement; flags large deposits in APP-018. |
| Property appraisal | `appraisal.txt` | `collateral/appraisal.pdf` | `collateral/appraisal.png` | `07_collateral/` | **TXT** — valuation baseline. **PDF** — USPAP report. **PNG** — photographed summary page; LTV signal in APP-016. |
| Credit report | — | `credit_history/credit_report.pdf` | — | `06_credit/` | **PDF only** — bureau pull is lender-side, not a borrower upload; no txt/png because borrowers do not submit credit files. |

### Format roles in agent demos

| Format | Typical agent pipeline | Why we include it |
|--------|------------------------|-------------------|
| **TXT** | Direct LLM context, no OCR | Zero-dependency baseline; deterministic; smallest artifacts |
| **PDF** | PDF parser, layout-aware extraction, or vision | Most common format for forms, letters, and bureau reports |
| **PNG** | Vision / OCR on scanned images | Proves agents handle camera uploads and non-selectable text |

### Scripts per format

| Format | Generator | Dependencies |
|--------|-----------|--------------|
| TXT | `generate_raw_layer.py` | Python 3 stdlib only |
| PDF, PNG | `generate_agent_documents.py` | `reportlab`, `Pillow` (`requirements.txt`) |

When adding a new document type, choose format(s) based on how a real borrower or lender would receive it, document the decision in [FORMAT_DECISIONS.md](FORMAT_DECISIONS.md), then update this table and the relevant formatter.

## Version control

Generated raw documents under `data-generation/corpus/raw/` and the built `dataset-seed/00_raw/` package are **committed** (same pattern as `inesite-agentic-inventory-planning` and `insesite-hls-agentic-rd-knowledge`).

**Source of truth:** Bronze JSON in `corpus/bronze/` plus `generate_raw_layer.py` and `generate_agent_documents.py`.

After editing bronze JSON, regenerate and rebuild:

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
pip install -r requirements.txt
python3 generate_agent_documents.py
python3 build_dataset_seed.py
```

## Generation scripts

```bash
cd data-generation/scripts
python3 generate_raw_layer.py                    # corpus/raw/txt/
pip install -r requirements.txt
python3 generate_agent_documents.py              # corpus/raw/pdf/ and corpus/raw/png/
python3 build_dataset_seed.py                    # dataset-seed/
```

**When to re-run:**
- After editing any Bronze JSON (to keep Raw coherent).
- After adding a new application.
- After extending a formatter to add new fields.

## Document types

| File | Source JSON folder | Simulates |
|------|--------------------|-----------|
| `loan_application.txt` | `01_application/` | URLA Form 1003 — loan request, borrower info, declared financials, property |
| `identity.txt` | `02_identity/` | Driver's license scan |
| `paystub_1.txt` | `03_income/` (`_paystub_1`) | Most recent earnings statement |
| `paystub_2.txt` | `03_income/` (`_paystub_2`) | Previous earnings statement |
| `employment_verification.txt` | `04_employment/` | VOE letter from employer |
| `bank_statement_1.txt` | `05_banking/` (`_statement_1`) | Bank statement — most recent month |
| `bank_statement_2.txt` | `05_banking/` (`_statement_2`) | Bank statement — previous month |
| `bank_statement_3.txt` | `05_banking/` (`_statement_3`) | Bank statement — two months prior |
| `appraisal.txt` | `07_collateral/` (`_appraisal`) | Property appraisal report |

`loan_application.txt` is the customer-facing representation of `01_application/APP-XXX.json`. The JSON is the ground truth the agent must be able to extract from it. The backend previously read `01_application/` JSON directly; that flow migrates to reading via `IRawDocumentStore` (tracked in US 128596).

Credit reports (`06_credit/`) are not part of the TXT Raw layer — they are pulled directly from the credit bureau by the lender. A **PDF** credit report is generated under `00_raw/pdf/` for agent demos; see [Format strategy](#format-strategy).

---

## Scenario log

Each entry documents a case's decision type, the inconsistency embedded in the Raw layer, and which files carry it.

### Approve cases (APP-001 to APP-012)

All approve cases have clean, consistent Raw documents. No deliberate inconsistencies.

| App | Borrower | Employer | Credit Score | Notes |
|-----|----------|----------|-------------|-------|
| APP-001 | Olivia Bennett | Frontier Health Systems | 758 | — |
| APP-002 | Ethan Carter | Sonoran Logistics | 744 | co-borrower: Sophia Carter |
| APP-003 | Mia Thompson | BlueWave Medical | 732 | — |
| APP-004 | Noah Rivera | Lone Star DataWorks | 781 | — |
| APP-005 | Ava Patel | Meridian Consulting Group | 726 | — |
| APP-006 | Liam Brooks | Apex Utility Services | 749 | — |
| APP-007 | Isabella Nguyen | Northwest Cloud Labs | 770 | co-borrower: Lucas Nguyen |
| APP-008 | James Sullivan | Crescent Biotech | 738 | — |
| APP-009 | Charlotte Morgan | Harbor Advisory Partners | 722 | — |
| APP-010 | Benjamin Flores | Twin Rivers Manufacturing | 747 | — |
| APP-011 | Harper Scott | Golden State Renewables | 719 | — |
| APP-012 | Daniel Kim | Cascade Tech Services | 762 | co-borrower: Grace Kim |

---

### Deny cases (APP-013 to APP-016)

#### APP-013 — `credit_score_below_threshold`

- **Borrower:** Emily Reed | **Employer:** Peachtree Media Group
- **Credit score:** 604 (threshold: 620)
- **Raw signal:** The credit report is a bureau pull (not in Raw layer). Raw docs are clean and consistent.
- **How agent detects it:** Cross-reference with credit bureau result in `06_credit/APP-013_credit.json` — `collections_flag: true`, `delinquencies_12m: 2`, `credit_utilization: 61%`.
- **Files with inconsistency:** none (signal is in Bronze credit JSON, not Raw)

---

#### APP-014 — `dti_above_policy_limit`

- **Borrower:** Michael Torres | **Employer:** Desert Fleet Services
- **Declared income:** $8,500/mo | **Existing debt obligations:** $4,700/mo (credit report)
- **Estimated mortgage payment:** ~$1,920/mo on $307,500 at 6.4% / 30yr
- **DTI:** ($4,700 + $1,920) / $8,500 = **77.9%** (policy limit: ~43%)
- **Raw signal:** Paystubs confirm income of $4,250/semi-monthly ($8,500/mo). No large deposits, no address mismatch.
- **How agent detects it:** Computes DTI from income + credit obligations. The income and debt figures are internally consistent — the denial is a policy arithmetic outcome.
- **Files with inconsistency:** none in Raw; signal emerges from the ratio across `03_income/`, `06_credit/`, and the requested loan amount in `loan_application.txt`.

---

#### APP-015 — `income_document_mismatch`

- **Borrower:** Sofia Alvarez | **Employer:** Pacific Horizon Labs
- **Declared monthly income:** $10,333 (→ $124,000/yr, matching VOE)
- **paystub_1 gross pay:** $4,236.67/period → annualized ~$101,680 ← **mismatch**
- **paystub_2 gross pay:** $5,166.67/period → annualized $124,000 ← consistent with VOE
- **Raw signal:** `paystub_1.txt` shows $4,236.67 current period gross; `paystub_2.txt` shows $5,166.67. The two paystubs disagree with each other and paystub_1 disagrees with the VOE and declared income.
- **How agent detects it:** Compares gross pay across both paystubs and against VOE `base_salary_annual`. Discrepancy of ~$22,320/yr between the two paystubs triggers the mismatch flag.
- **Files with inconsistency:** `00_raw/txt/APP-015/paystub_1.txt`, `00_raw/txt/APP-015/paystub_2.txt`

---

#### APP-016 — `ltv_above_policy_limit`

- **Borrower:** Jacob Miller | **Employer:** Midwest Telecom Partners
- **Purchase price:** $620,000 | **Down payment:** $62,000 (10%) | **Loan:** $558,000
- **Appraised value:** $585,000 (below purchase price)
- **LTV:** $558,000 / $585,000 = **95.4%** (policy limit: 80%)
- **Raw signal:** `appraisal.txt` shows Appraised Value $585,000 < Purchase Price $620,000, with Calculated LTV explicitly stated as 95.4%.
- **How agent detects it:** Reads appraised value from appraisal, computes LTV against loan amount, compares to policy threshold.
- **Files with inconsistency:** `00_raw/txt/APP-016/appraisal.txt`

---

### Manual review cases (APP-017 to APP-020)

#### APP-017 — `address_mismatch_requires_review`

- **Borrower:** Amelia Price | **Employer:** Triangle Analytics
- **Application address:** `604 Birch Terrace, Raleigh, NC 27608` (in `loan_application.txt`)
- **Driver's license address:** `604 Birch Terrace Apt 2, Raleigh, NC 27608` (in `identity.txt`)
- **Inconsistency:** The loan application omits the apartment unit. Same street and ZIP, but the ID document includes "Apt 2" which the application form does not.
- **Raw signal:** `loan_application.txt` → `Current Address: 604 Birch Terrace, Raleigh, NC 27608`; `identity.txt` → `Address: 604 Birch Terrace Apt 2, Raleigh, NC 27608`.
- **How agent detects it:** Compares borrower address in `loan_application.txt` against the address in `identity.txt`. Unit/apartment discrepancy triggers address mismatch flag.
- **Files with inconsistency:** `00_raw/txt/APP-017/loan_application.txt`, `00_raw/txt/APP-017/identity.txt`

---

#### APP-018 — `unexplained_large_deposits`

- **Borrower:** William Cooper | **Employer:** Sunline Hospitality Group
- **Large deposit:** $9,200 on 2026-02-11, described as "External transfer" — no source documented
- **Raw signal:** `bank_statement_2.txt` (February 2026) lists the transaction and marks it as `*** LARGE DEPOSIT — SOURCE UNVERIFIED ***`. The January and March statements are clean.
- **How agent detects it:** Scans bank statements for deposits ≥ $5,000 with non-payroll descriptions. Flags transfers without documented source for underwriter review.
- **Files with inconsistency:** `00_raw/txt/APP-018/bank_statement_2.txt`

---

#### APP-019 — `employment_tenure_borderline`

- **Borrower:** Evelyn Ross | **Employer:** Wasatch Energy Solutions
- **Hire date:** September 15, 2025 — approximately **6 months** before application date (March 23, 2026)
- **Policy:** Most guidelines require 2 years of continuous employment at current employer for salaried borrowers; 6 months is borderline for manual review.
- **Secondary signal:** Driver's license address (`604 Birch Terrace Apt 2, Raleigh, NC 27608`) differs from application address (`87 Summit Park, Salt Lake City, UT 84103`) — different states, suggesting a recent relocation that aligns with the short tenure.
- **Raw signal:** `employment_verification.txt` shows `Hire Date: September 15, 2025`. `identity.txt` shows an NC address while the application declares UT.
- **How agent detects it:** Computes months between hire date and application date. Flags if < 24 months; also flags state mismatch between ID and application address.
- **Files with inconsistency:** `00_raw/txt/APP-019/employment_verification.txt`, `00_raw/txt/APP-019/identity.txt`

---

#### APP-020 — `borderline_credit_and_affordability`

- **Borrower:** Alexander Ward | **Employer:** Capitol Medical Devices
- **Credit score:** 684 (above hard threshold 620, but borderline for standard products)
- **Existing debt obligations:** $4,300/mo | **Declared income:** $11,417/mo
- **Estimated mortgage payment:** ~$2,720/mo on $430,500 at 6.5% / 30yr
- **DTI:** ($4,300 + $2,720) / $11,417 = **61.5%** (high, but below the hard 43% threshold only for manually-reviewed products — triggers human review)
- **Raw signal:** All Raw docs are consistent and clean. The concern is in the aggregate financial profile visible across credit + income + loan amount.
- **How agent detects it:** Credit score and DTI are each individually below hard denial thresholds but both together are in the borderline band that requires human underwriter sign-off.
- **Files with inconsistency:** none in Raw; signal is in aggregate across `06_credit/`, `03_income/`, and `loan_application.txt`.

---

## Adding a new scenario

Follow these steps to add a new application (e.g., APP-021):

1. **Create Bronze JSON files** for all applicable folders (`01_application/`, `02_identity/`, `03_income/`, `04_employment/`, `05_banking/`, `06_credit/`, `07_collateral/`, `09_decision_ground_truth/`). Use existing files as templates.

2. **Embed the inconsistency in the Bronze JSON.** The Raw layer is derived from it — the inconsistency must exist in the JSON first (e.g., set a low `gross_pay` in `_paystub_1.json` to reproduce an income mismatch).

3. **Re-run the generation script:**
   ```bash
   cd data-generation/scripts
   python3 generate_raw_layer.py
   ```

4. **Update `dataset_summary.json`** — increment `application_count`, update `document_counts`, and add the new case to `decision_distribution` and `inconsistency_coverage`.

5. **Update `ground_truth.csv`** with the new row (application_id, expected_decision, primary_reason, required_human_review, summary_explanation).

6. **Add a new entry to this file** under the appropriate section (Approve / Deny / Manual review), documenting the borrower, the embedded inconsistency, and which Raw files carry the signal.

### Extending a document type

To add a new field to an existing formatter (e.g., add `ssn_last4` to `identity.txt`):

1. Add the field to the relevant Bronze JSON schema (`02_identity/SCHEMA.md`).
2. Edit the corresponding formatter function in `generate_raw_layer.py` (e.g., `identity_txt()`).
3. Re-run the script.
4. Update the table in the **Document types** section above.
