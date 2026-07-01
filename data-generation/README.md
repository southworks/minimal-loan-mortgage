# FSI Loan & Mortgage Data Generation

Reference material for building and validating the loan-mortgage dataset. Not required to run a demo — use [`../dataset-seed/`](../dataset-seed/) for that.

## Layout

```
data-generation/
  corpus/
    bronze/                  hand-authored structured JSON (01_application … 07_collateral)
    policies/              underwriting policy text for RAG seeding
    raw/                     generated txt/pdf/png loan documents (all 20 applications)
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
python3 generate_normalized_layers.py      # validate + refresh ground_truth.csv
```

## How to add a scenario

New scenarios are generated into `dataset-seed/` and only affect the running app after the generated assets are rebuilt, container images or deployment packages are republished, and Azure is redeployed.

1. Add the new legacy application (`APP-XXX`) under `corpus/bronze/`.
2. Add the scenario entry and `APP-XXX` -> `case-XX` mapping in `scripts/scenarios.py`.
3. Run the generation commands above.
4. Review `../dataset-seed/cases/{caseId}/`, `../dataset-seed/cases/catalog.json`, `ground-truth/`, and `scripts/dataset_summary.json`.
5. Update [`docs/RAW_LAYER.md`](docs/RAW_LAYER.md) with the scenario log and then rebuild/redeploy the app assets that embed `dataset-seed/`.

## Key docs

- [`docs/RAW_LAYER.md`](docs/RAW_LAYER.md) — raw document types and scenario log
- [`docs/AGENT_INPUTS.md`](docs/AGENT_INPUTS.md) — pdf/png category mapping
- [`docs/FORMAT_DECISIONS.md`](docs/FORMAT_DECISIONS.md) — per-format rationale
- [`ground-truth/SCHEMA.md`](ground-truth/SCHEMA.md) — decision ground-truth schema
