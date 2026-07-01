#!/usr/bin/env python3
"""
Refresh ground-truth rollups from committed decision JSON files.

Validates that each scenario in scenarios.py has a matching
ground-truth/APP-XXX_decision.json and regenerates ground_truth.csv.

Optional — run when decision metadata changes. Does not alter bronze or raw layers.
"""

from __future__ import annotations

import csv
import json
from pathlib import Path

from scenarios import SCENARIOS

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
GT_ROOT = DATA_GEN / "ground-truth"


def _load_decision(app_id: str) -> dict:
    path = GT_ROOT / f"{app_id}_decision.json"
    if not path.exists():
        raise FileNotFoundError(f"Missing ground-truth file for {app_id}: {path}")
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def write_ground_truth_csv() -> int:
    rows: list[dict[str, str]] = []
    for scenario in SCENARIOS:
        app_id = scenario["scenario_id"]
        decision = _load_decision(app_id)
        rows.append(
            {
                "application_id": app_id,
                "expected_decision": decision["expected_decision"],
                "primary_reason": decision["primary_reason"],
                "required_human_review": str(decision["required_human_review"]),
                "summary_explanation": decision["summary_explanation"],
            }
        )
        if decision["expected_decision"] != scenario["final_outcome"]:
            raise ValueError(
                f"{app_id}: ground-truth decision {decision['expected_decision']!r} "
                f"!= scenario final_outcome {scenario['final_outcome']!r}"
            )
        if decision["primary_reason"] != scenario["primary_reason"]:
            raise ValueError(
                f"{app_id}: ground-truth primary_reason mismatch with scenarios.py"
            )

    out = GT_ROOT / "ground_truth.csv"
    with open(out, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(
            f,
            fieldnames=[
                "application_id",
                "expected_decision",
                "primary_reason",
                "required_human_review",
                "summary_explanation",
            ],
            quoting=csv.QUOTE_ALL,
        )
        writer.writeheader()
        writer.writerows(rows)
    return len(rows)


def main() -> None:
    n = write_ground_truth_csv()
    print(f"Validated {n} scenarios and wrote {GT_ROOT / 'ground_truth.csv'}")


if __name__ == "__main__":
    main()
