#!/usr/bin/env python3
"""
Build dataset-seed/ demo package from data-generation/corpus/.

Copies bronze JSON, policy RAG, and generated raw documents into the runtime
layout consumed by the API, MCP, Fabric seed scripts, and Blazor UI.

Run AFTER generate_raw_layer.py (and optionally generate_agent_documents.py).
"""

from __future__ import annotations

import shutil
from pathlib import Path

from scenarios import SCENARIOS

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
REPO = DATA_GEN.parent
CORPUS = DATA_GEN / "corpus"
SEED = REPO / "dataset-seed"

BRONZE_LAYERS = [
    "01_application",
    "02_identity",
    "03_income",
    "04_employment",
    "05_banking",
    "06_credit",
    "07_collateral",
]


def _copy_tree(src: Path, dest: Path) -> int:
    if not src.exists():
        raise FileNotFoundError(f"Missing corpus path: {src}")
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(src, dest)
    return sum(1 for path in dest.rglob("*") if path.is_file())


def main() -> None:
    SEED.mkdir(parents=True, exist_ok=True)
    total = 0

    for layer in BRONZE_LAYERS:
        n = _copy_tree(CORPUS / "bronze" / layer, SEED / layer)
        print(f"  {layer}/ — {n} files")
        total += n

    n = _copy_tree(CORPUS / "policy_rag", SEED / "08_policy_rag")
    print(f"  08_policy_rag/ — {n} files")
    total += n

    n = _copy_tree(CORPUS / "raw", SEED / "00_raw")
    print(f"  00_raw/ — {n} files")
    total += n

    matrix_src = SCRIPTS / "case_matrix.source.json"
    if not matrix_src.exists():
        raise FileNotFoundError(f"Missing case matrix source: {matrix_src}")
    shutil.copy2(matrix_src, SEED / "case_matrix.json")
    total += 1
    print("  case_matrix.json")

    print(
        f"\nDone — {total} files written to {SEED.relative_to(REPO)} "
        f"({len(SCENARIOS)} scenarios)"
    )


if __name__ == "__main__":
    main()
