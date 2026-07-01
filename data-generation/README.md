# FSI Loan & Mortgage Data Generation

Reference material for building and validating the loan-mortgage dataset. Not required to run a demo — use [`../dataset-seed/`](../dataset-seed/) for that.

## Layout

```
data-generation/
  corpus/
    bronze/                  hand-authored structured JSON (01_application … 07_collateral)
    policies/                underwriting policy text for RAG seeding
    raw/                     generated txt/pdf/png loan documents
  ground-truth/              expected decision JSON + ground_truth.csv
  scripts/                   generators, scenarios.py, build_case_folders.py
  docs/                      RAW_LAYER, AGENT_INPUTS, schemas
```

## Regenerate demo package

```bash
cd data-generation/scripts
python3 generate_raw_layer.py              # corpus/raw/txt/
pip install -r requirements.txt
python3 generate_agent_documents.py        # corpus/raw/pdf|png/
python3 build_case_folders.py              # dataset-seed/cases/*/ingest + fabric-pre-requisite-data/
```

Optional:

```bash
python3 generate_agent_documents.py --app APP-XXX   # single application only
python3 generate_normalized_layers.py             # validate + refresh ground_truth.csv
```

## How runtime discovers scenarios

New cases land in `dataset-seed/` after you run the build scripts. The running app picks them up from that package — there is no separate registration step in frontend or backend code.

| Layer | Behavior |
|-------|----------|
| **UI** | `DatasetSeedCatalogService` scans `dataset-seed/cases/*/` (each folder needs `ingest/loan_application.txt`). `catalog.json` is auto-written by `build_case_folders.py` and only enriches metadata (borrower, outcome). |
| **API** | `LocalCaseDocumentService` accepts any `caseId` whose `cases/{caseId}/ingest/` exists in the bundled seed — no C# allow-list. |
| **Case folders** | `case-01` … `case-NN`, mapped from `CASE_FOLDERS` in `scenarios.py`. |

After regenerating data, rebuild and redeploy the artifact that embeds `dataset-seed/` (and run the Fabric seed step on Azure if applicable).

## How to add a scenario

New scenarios are written into `dataset-seed/`. They do **not** appear in a running app until you rebuild the generated assets, republish container images or deployment packages, and redeploy.

### 1. Plan the scenario

- Pick an existing **path** (outcome pattern) or define a new one in `scripts/scenarios.py` — see current paths in that file (`meets_credit_income_collateral_policy`, `income_document_mismatch`, `employment_tenure_borderline`, etc.).
- Choose the next legacy id (`APP-XXX`) and demo folder (`case-XX`). Extend `CASE_FOLDERS` in `scenarios.py` if you exceed the current range.

### 2. Author Bronze JSON

Create files under `corpus/bronze/` for the new application (clone an existing `APP-*` as template):

| Folder | Files |
|--------|--------|
| `01_application/` | `APP-XXX.json` |
| `02_identity/` | `APP-XXX_id.json` |
| `03_income/` | `APP-XXX_paystub_1.json`, `APP-XXX_paystub_2.json` |
| `04_employment/` | `APP-XXX_voe.json` |
| `05_banking/` | `APP-XXX_statement_1.json` … `_3.json` |
| `06_credit/` | `APP-XXX_credit.json` |
| `07_collateral/` | `APP-XXX_appraisal.json`, `APP-XXX_property_details.json` |

**Embed deliberate signals in Bronze** (low score, income mismatch, short tenure, etc.). The raw txt/pdf/png layers are derived from these files — the signal must exist in JSON first.

See per-layer schemas in `corpus/bronze/*/SCHEMA.md`.

### 3. Declare the scenario

Add an entry to `SCENARIOS` in `scripts/scenarios.py`:

- `scenario_id`, `path`, `title`, `final_outcome`, `primary_reason`
- `risk_flags`, `inconsistencies`, `borrower`, `stages[]` (agent chain, gates, policy refs)
- Map `APP-XXX` → `case-XX` in `CASE_FOLDERS`

Add matching ground truth: `ground-truth/APP-XXX_decision.json` (`expected_decision`, `primary_reason`, `required_human_review`, etc.).

### 4. Regenerate derived assets

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 generate_agent_documents.py        # omit --app to rebuild all; or --app APP-XXX
python3 build_case_folders.py
python3 generate_normalized_layers.py      # validates scenarios.py vs ground-truth JSON
```

### 5. Review output

Check before committing:

- `../dataset-seed/cases/case-XX/` — `ingest/`, `fabric-pre-requisite-data/`, `README.md`
- `../dataset-seed/cases/catalog.json` (auto-generated)
- `ground-truth/APP-XXX_decision.json`, `ground-truth/ground_truth.csv`
- `scripts/dataset_summary.json` (counts and decision distribution)

### 6. Document the scenario

Add a log entry to [`docs/RAW_LAYER.md`](docs/RAW_LAYER.md): borrower, outcome, deliberate signal, and which raw files carry it.

### 7. Rebuild and redeploy

Rebuild any image or deployment package that embeds `dataset-seed/`. For Azure, redeploy so the API container and Fabric seed step receive the new case. No extra UI/backend wiring — see [How runtime discovers scenarios](#how-runtime-discovers-scenarios).

### Extending a document type

To add a new field or document type, update the Bronze schema, the formatter in `generate_raw_layer.py` / `generate_agent_documents.py`, re-run the pipeline, and update [`docs/RAW_LAYER.md`](docs/RAW_LAYER.md) and [`docs/AGENT_INPUTS.md`](docs/AGENT_INPUTS.md).

## Key docs

- [`docs/RAW_LAYER.md`](docs/RAW_LAYER.md) — raw document types and scenario log
- [`docs/AGENT_INPUTS.md`](docs/AGENT_INPUTS.md) — pdf/png category mapping
- [`docs/FORMAT_DECISIONS.md`](docs/FORMAT_DECISIONS.md) — per-format rationale
- [`ground-truth/SCHEMA.md`](ground-truth/SCHEMA.md) — decision ground-truth schema
