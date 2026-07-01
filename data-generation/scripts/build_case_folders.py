#!/usr/bin/env python3
"""
Build dataset-seed/cases/ demo folders from corpus exports.

Each case folder matches the inventory/HLS demo layout:

    dataset-seed/cases/case-XX/
      README.md                     (generated — preserved if already present)
      ingest/                       flat borrower-upload documents (txt + pdf/png)
      fabric-pre-requisite-data/    case-scoped bronze JSON for MCP tools
        01_application/
        02_identity/
        ...

Also writes dataset-seed/cases/catalog.json and copies shared policies to
dataset-seed/policies/.

Run AFTER generate_raw_layer.py (and optionally generate_agent_documents.py).
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from scenarios import (
    BRONZE_LAYERS,
    OUTCOME_TAGS,
    SCENARIOS,
    application_id,
    case_folder,
)

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
REPO = DATA_GEN.parent
CORPUS = DATA_GEN / "corpus"
SEED = REPO / "dataset-seed"
CASES_DIR = SEED / "cases"

LEGACY_FLAT_DIRS = [
    *BRONZE_LAYERS,
    "case_matrix.json",
]


def _copy_file(src: Path, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)


def _copy_tree(src: Path, dest: Path) -> int:
    if not src.exists():
        return 0
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(src, dest)
    return sum(1 for path in dest.rglob("*") if path.is_file())


def _bronze_files_for_app(layer: str, app_id: str) -> list[Path]:
    layer_dir = CORPUS / "bronze" / layer
    if not layer_dir.exists():
        return []
    if layer == "01_application":
        candidate = layer_dir / f"{app_id}.json"
        return [candidate] if candidate.exists() else []
    return sorted(layer_dir.glob(f"{app_id}_*.json"))


def _write_readme(case_dir: Path, scenario: dict) -> None:
    readme = case_dir / "README.md"
    if readme.exists():
        return

    case_id = case_folder(scenario)
    app_id = application_id(scenario)
    outcome = scenario["final_outcome"]
    lines = [
        f"# {scenario['title']}",
        "",
        f"- **Case id:** `{case_id}`",
        f"- **Legacy application id:** `{app_id}`",
        f"- **Expected outcome:** `{outcome}`",
        f"- **Primary reason:** `{scenario['primary_reason']}`",
        "",
        "## Ingest",
        "",
        "Flat borrower-upload documents in `ingest/` (txt plus optional pdf/png).",
        "The API workflow starts from these files.",
        "",
        "## Fabric prerequisite data",
        "",
        "Case-scoped bronze JSON under `fabric-pre-requisite-data/` for MCP tools.",
        "",
    ]
    if scenario["inconsistencies"]:
        lines.extend(
            [
                "## Deliberate signals",
                "",
                *[f"- `{flag}`" for flag in scenario["inconsistencies"]],
                "",
            ]
        )
    readme.write_text("\n".join(lines), encoding="utf-8")


def _build_ingest(case_dir: Path, app_id: str) -> int:
    ingest_dir = case_dir / "ingest"
    if ingest_dir.exists():
        shutil.rmtree(ingest_dir)
    ingest_dir.mkdir(parents=True)

    count = 0
    txt_src = CORPUS / "raw" / "txt" / app_id
    if not txt_src.exists():
        raise FileNotFoundError(f"Missing raw txt corpus for {app_id}: {txt_src}")

    for src in sorted(txt_src.glob("*.txt")):
        _copy_file(src, ingest_dir / src.name)
        count += 1

    for fmt in ("pdf", "png"):
        fmt_src = CORPUS / "raw" / fmt / app_id
        if fmt_src.exists():
            count += _copy_tree(fmt_src, ingest_dir / fmt)

    return count


def _build_fabric_prerequisites(case_dir: Path, app_id: str) -> int:
    prereq_dir = case_dir / "fabric-pre-requisite-data"
    if prereq_dir.exists():
        shutil.rmtree(prereq_dir)

    count = 0
    for layer in BRONZE_LAYERS:
        files = _bronze_files_for_app(layer, app_id)
        if not files:
            continue
        dest_layer = prereq_dir / layer
        dest_layer.mkdir(parents=True, exist_ok=True)
        for src in files:
            _copy_file(src, dest_layer / src.name)
            count += 1
    return count


def _build_case(scenario: dict) -> tuple[int, int]:
    case_dir = CASES_DIR / case_folder(scenario)
    case_dir.mkdir(parents=True, exist_ok=True)
    ingest_files = _build_ingest(case_dir, application_id(scenario))
    prereq_files = _build_fabric_prerequisites(case_dir, application_id(scenario))
    _write_readme(case_dir, scenario)
    return ingest_files, prereq_files


def _write_catalog() -> None:
    entries = []
    for scenario in SCENARIOS:
        case_id = case_folder(scenario)
        app_id = application_id(scenario)
        outcome = scenario["final_outcome"]
        entries.append(
            {
                "caseId": case_id,
                "title": scenario["borrower"],
                "description": (
                    f"Mortgage application {app_id} for {scenario['borrower']}. "
                    f"Expected outcome: {outcome.replace('_', ' ')} ({scenario['primary_reason']})."
                ),
                "outcomeTag": OUTCOME_TAGS.get(outcome, outcome),
                "legacyId": app_id,
                "applicationId": app_id,
                "context": {
                    "borrower": scenario["borrower"],
                    "coBorrower": scenario.get("co_borrower") or "",
                    "expectedDecision": outcome,
                    "primaryReason": scenario["primary_reason"],
                    "requiredHumanReview": scenario["required_human_review"],
                    "riskFlags": scenario.get("risk_flags") or [],
                    "inconsistencies": scenario.get("inconsistencies") or [],
                },
            }
        )

    catalog_path = CASES_DIR / "catalog.json"
    catalog_path.write_text(json.dumps(entries, indent=2) + "\n", encoding="utf-8")


def _remove_legacy_flat_layout() -> None:
    for name in LEGACY_FLAT_DIRS:
        target = SEED / name
        if target.is_file():
            target.unlink()
        elif target.is_dir():
            shutil.rmtree(target)


def _copy_shared_policies() -> int:
    return _copy_tree(CORPUS / "policies", SEED / "policies")


def main() -> None:
    CASES_DIR.mkdir(parents=True, exist_ok=True)
    ingest_total = 0
    prereq_total = 0

    for scenario in SCENARIOS:
        ingest_files, prereq_files = _build_case(scenario)
        rel = f"cases/{case_folder(scenario)}"
        print(f"{rel}: {ingest_files} ingest + {prereq_files} prerequisite files")
        ingest_total += ingest_files
        prereq_total += prereq_files

    _write_catalog()
    policy_files = _copy_shared_policies()
    _remove_legacy_flat_layout()

    print(f"\nWrote cases/catalog.json ({len(SCENARIOS)} cases)")
    print(f"Copied shared policies/ ({policy_files} files)")
    print(
        f"Done — {ingest_total} ingest files and {prereq_total} prerequisite files "
        f"under dataset-seed/cases/"
    )


if __name__ == "__main__":
    main()
