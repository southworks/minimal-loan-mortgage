#!/usr/bin/env python3
"""
Scenario definitions for the FSI Loan & Mortgage agentic workflow dataset.

Single source of truth for the 20 demo applications (APP-001 … APP-020).
Each scenario is one full path through:

  document_processing_agent -> underwriting_agent -> human approval (underwriter)
  -> responsible_ai_agent -> loan_setup_agent

Generators import this module so ground-truth rollups and dataset-seed stay aligned:

  - generate_normalized_layers.py  -> ground-truth/APP-XXX_decision.json
  - build_dataset_seed.py          -> dataset-seed/ (runtime demo package)

Trackable prefix: APP-### (loan application), like IPF-### in inventory and ING-### in HLS.
"""

from __future__ import annotations

AGENT_CHAIN = [
    "document_processing_agent",
    "underwriting_agent",
    "responsible_ai_agent",
    "loan_setup_agent",
]

BRONZE_LAYERS = [
    "01_application",
    "02_identity",
    "03_income",
    "04_employment",
    "05_banking",
    "06_credit",
    "07_collateral",
]

def scenario_folder(scenario: dict) -> str:
    """Runtime folder name — flat APP-XXX id (loan uses numbered bronze layers)."""
    return scenario['scenario_id']


SCENARIOS = [
    {
        "scenario_id": "APP-001",
        "path": "meets_credit_income_collateral_policy",
        "title": "Olivia Bennett — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Olivia Bennett",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-001",
            "raw_document_root": "00_raw/txt/APP-001",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-001",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-001",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-002",
        "path": "meets_credit_income_collateral_policy",
        "title": "Ethan Carter — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Ethan Carter",
        "co_borrower": "Sophia Carter",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-002",
            "raw_document_root": "00_raw/txt/APP-002",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-002",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-002",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-003",
        "path": "meets_credit_income_collateral_policy",
        "title": "Mia Thompson — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Mia Thompson",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-003",
            "raw_document_root": "00_raw/txt/APP-003",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-003",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-003",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-004",
        "path": "meets_credit_income_collateral_policy",
        "title": "Noah Rivera — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Noah Rivera",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-004",
            "raw_document_root": "00_raw/txt/APP-004",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-004",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-004",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-005",
        "path": "meets_credit_income_collateral_policy",
        "title": "Ava Patel — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Ava Patel",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-005",
            "raw_document_root": "00_raw/txt/APP-005",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-005",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-005",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-006",
        "path": "meets_credit_income_collateral_policy",
        "title": "Liam Brooks — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Liam Brooks",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-006",
            "raw_document_root": "00_raw/txt/APP-006",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-006",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-006",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-007",
        "path": "meets_credit_income_collateral_policy",
        "title": "Isabella Nguyen — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Isabella Nguyen",
        "co_borrower": "Lucas Nguyen",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-007",
            "raw_document_root": "00_raw/txt/APP-007",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-007",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-007",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-008",
        "path": "meets_credit_income_collateral_policy",
        "title": "James Sullivan — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "James Sullivan",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-008",
            "raw_document_root": "00_raw/txt/APP-008",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-008",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-008",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-009",
        "path": "meets_credit_income_collateral_policy",
        "title": "Charlotte Morgan — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Charlotte Morgan",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-009",
            "raw_document_root": "00_raw/txt/APP-009",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-009",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-009",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-010",
        "path": "meets_credit_income_collateral_policy",
        "title": "Benjamin Flores — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Benjamin Flores",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-010",
            "raw_document_root": "00_raw/txt/APP-010",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-010",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-010",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-011",
        "path": "meets_credit_income_collateral_policy",
        "title": "Harper Scott — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Harper Scott",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-011",
            "raw_document_root": "00_raw/txt/APP-011",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-011",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-011",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-012",
        "path": "meets_credit_income_collateral_policy",
        "title": "Daniel Kim — meets credit income collateral policy",
        "final_outcome": "approve",
        "required_human_review": False,
        "primary_reason": "meets_credit_income_collateral_policy",
        "risk_flags": [],
        "inconsistencies": [],
        "borrower": "Daniel Kim",
        "co_borrower": "Grace Kim",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-012",
            "raw_document_root": "00_raw/txt/APP-012",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-012",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-012",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-013",
        "path": "credit_score_below_threshold",
        "title": "Emily Reed — credit score below threshold",
        "final_outcome": "deny",
        "required_human_review": False,
        "primary_reason": "credit_score_below_threshold",
        "risk_flags": ["low_score_or_high_dti"],
        "inconsistencies": ["low_score_or_high_dti"],
        "borrower": "Emily Reed",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-013",
            "raw_document_root": "00_raw/txt/APP-013",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-013",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-013",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-014",
        "path": "dti_above_policy_limit",
        "title": "Michael Torres — dti above policy limit",
        "final_outcome": "deny",
        "required_human_review": False,
        "primary_reason": "dti_above_policy_limit",
        "risk_flags": ["low_score_or_high_dti"],
        "inconsistencies": ["low_score_or_high_dti"],
        "borrower": "Michael Torres",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-014",
            "raw_document_root": "00_raw/txt/APP-014",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-014",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-014",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-015",
        "path": "income_document_mismatch",
        "title": "Sofia Alvarez — income document mismatch",
        "final_outcome": "deny",
        "required_human_review": False,
        "primary_reason": "income_document_mismatch",
        "risk_flags": ["income_mismatch"],
        "inconsistencies": ["income_mismatch"],
        "borrower": "Sofia Alvarez",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-015",
            "raw_document_root": "00_raw/txt/APP-015",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-015",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-015",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-016",
        "path": "ltv_above_policy_limit",
        "title": "Jacob Miller — ltv above policy limit",
        "final_outcome": "deny",
        "required_human_review": False,
        "primary_reason": "ltv_above_policy_limit",
        "risk_flags": ["insufficient_appraisal_for_ltv"],
        "inconsistencies": ["insufficient_appraisal_for_ltv"],
        "borrower": "Jacob Miller",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-016",
            "raw_document_root": "00_raw/txt/APP-016",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-016",
        },
        "gate": None,
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-016",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-017",
        "path": "address_mismatch_requires_review",
        "title": "Amelia Price — address mismatch requires review",
        "final_outcome": "manual_review",
        "required_human_review": True,
        "primary_reason": "address_mismatch_requires_review",
        "risk_flags": ["address_mismatch"],
        "inconsistencies": ["address_mismatch"],
        "borrower": "Amelia Price",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-017",
            "raw_document_root": "00_raw/txt/APP-017",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-017",
        },
        "gate": "underwriter_review",
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-017",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-018",
        "path": "unexplained_large_deposits",
        "title": "William Cooper — unexplained large deposits",
        "final_outcome": "manual_review",
        "required_human_review": True,
        "primary_reason": "unexplained_large_deposits",
        "risk_flags": ["unexplained_deposits"],
        "inconsistencies": ["unexplained_deposits"],
        "borrower": "William Cooper",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-018",
            "raw_document_root": "00_raw/txt/APP-018",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-018",
        },
        "gate": "underwriter_review",
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-018",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-019",
        "path": "employment_tenure_borderline",
        "title": "Evelyn Ross — employment tenure borderline",
        "final_outcome": "manual_review",
        "required_human_review": True,
        "primary_reason": "employment_tenure_borderline",
        "risk_flags": ["employment_tenure_short", "income_mismatch", "address_mismatch", "low_score_or_high_dti"],
        "inconsistencies": ["employment_tenure_short", "income_mismatch", "address_mismatch", "low_score_or_high_dti"],
        "borrower": "Evelyn Ross",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-019",
            "raw_document_root": "00_raw/txt/APP-019",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-019",
        },
        "gate": "underwriter_review",
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-019",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
    {
        "scenario_id": "APP-020",
        "path": "borderline_credit_and_affordability",
        "title": "Alexander Ward — borderline credit and affordability",
        "final_outcome": "manual_review",
        "required_human_review": True,
        "primary_reason": "borderline_credit_and_affordability",
        "risk_flags": ["low_score_or_high_dti", "unexplained_deposits", "insufficient_appraisal_for_ltv"],
        "inconsistencies": ["low_score_or_high_dti", "unexplained_deposits", "insufficient_appraisal_for_ltv"],
        "borrower": "Alexander Ward",
        "co_borrower": "",
        "stages": [
    {
        "stage": "document_processing",
        "agent": "document_processing_agent",
        "agent_input": {
            "task": "extract_and_normalize_loan_documents",
            "application_id": "APP-020",
            "raw_document_root": "00_raw/txt/APP-020",
        },
        "gate": None,
        "policy_refs": ["ID-150", "IN-330"],
    },
    {
        "stage": "underwriting",
        "agent": "underwriting_agent",
        "agent_input": {
            "task": "evaluate_credit_income_collateral_and_policy",
            "application_id": "APP-020",
        },
        "gate": "underwriter_review",
        "policy_refs": ["UW-100", "CR-210", "CL-125"],
    },
    {
        "stage": "responsible_ai",
        "agent": "responsible_ai_agent",
        "agent_input": {
            "task": "review_fairness_and_model_governance",
        },
        "gate": None,
        "policy_refs": ["RAI-400"],
    },
    {
        "stage": "loan_setup",
        "agent": "loan_setup_agent",
        "agent_input": {
            "task": "prepare_loan_setup_package",
            "application_id": "APP-020",
        },
        "gate": None,
        "policy_refs": ["LS-500"],
    }
        ],
    },
]

