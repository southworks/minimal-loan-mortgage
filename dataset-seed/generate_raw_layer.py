#!/usr/bin/env python3
"""
Generate Raw layer .txt documents from Bronze JSON for all 20 loan applications.
Output: dataset-seed/00_raw/txt/APP-XXX/

Each .txt simulates a scanned physical document consumed by the Document Processing Agent.
All values are derived directly from the Bronze JSON so Raw and Bronze stay coherent,
including inconsistencies embedded in the deny/manual_review cases:
  - APP-013: credit_score_below_threshold (reflected in credit bureau pull, not raw docs)
  - APP-014: dti_above_policy_limit (high existing debt visible in bank statements)
  - APP-015: income_document_mismatch (paystub_1 gross pay differs from paystub_2 / VOE)
  - APP-016: ltv_above_policy_limit (appraised_value < purchase_price, LTV 95.4%)
  - APP-017: address_mismatch_requires_review (ID has "Apt 2", application omits it)
  - APP-018: unexplained_large_deposits ($9,200 external transfer in statement_2)
  - APP-019: employment_tenure_borderline (hire_date 2025-09-15, ~6 months)
  - APP-020: borderline_credit_and_affordability (credit 684, high DTI)
"""

import json
from datetime import datetime
from pathlib import Path

BASE = Path(__file__).resolve().parent
RAW = BASE / "00_raw" / "txt"


def load(path: Path) -> dict:
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def write_txt(app_id: str, name: str, content: str) -> None:
    out = RAW / app_id / name
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(content.strip() + "\n", encoding="utf-8")
    print(f"  {out.relative_to(BASE.parent)}")


def fmt_money(v: float) -> str:
    return f"${v:,.2f}"


def fmt_date(d: str) -> str:
    try:
        return datetime.strptime(d, "%Y-%m-%d").strftime("%B %d, %Y")
    except Exception:
        return d


# ─── Document formatters ────────────────────────────────────────────────────

def identity_txt(d: dict) -> str:
    return f"""\
DRIVER'S LICENSE — COPY / SCAN
===============================
State Issuing Authority

Full Name    : {d['full_name']}
Date of Birth: {fmt_date(d['date_of_birth'])}
Address      : {d['address']}
License No.  : {d['document_number']}
Issue Date   : {fmt_date(d['document_date'])}
Expiry Date  : {fmt_date(d['expiry_date'])}

[FRONT SCAN — DOCUMENT SUBMITTED ON {fmt_date(d['document_date'])}]
"""


def paystub_txt(d: dict, seq: int) -> str:
    return f"""\
EARNINGS STATEMENT — PAYSTUB {seq}
===================================
Employer : {d['employer']}
Employee : {d['employee_name']}
Pay Date : {fmt_date(d['pay_date'])}
Period   : {fmt_date(d['pay_period_start'])} – {fmt_date(d['pay_period_end'])}
Frequency: {d['pay_frequency'].replace('_', ' ').title()}

                   CURRENT PERIOD    YTD GROSS
Gross Pay       :  {fmt_money(d['gross_pay']):<18} {fmt_money(d['ytd_gross'])}
Net Pay         :  {fmt_money(d['net_pay'])}

[DOCUMENT PROVIDED BY BORROWER — {fmt_date(d['document_date'])}]
"""


def voe_txt(d: dict) -> str:
    annual = d['base_salary_annual']
    bonus = d.get('bonus_annual', 0)
    return f"""\
VERIFICATION OF EMPLOYMENT
===========================
Issued by : {d['employer']}
Date      : {fmt_date(d['document_date'])}

This letter confirms the employment of:

  Name      : {d['employee_name']}
  Title     : {d['job_title']}
  Status    : {d['employment_status'].replace('_', ' ').title()}
  Hire Date : {fmt_date(d['hire_date'])}

COMPENSATION
  Base Annual Salary : {fmt_money(annual)}
  Annual Bonus       : {fmt_money(bonus)}

This verification is provided at the request of the employee for mortgage
application purposes. Please direct any questions to our HR department.

Authorized Signature: _________________________
{d['employer']} — Human Resources
"""


def bank_statement_txt(d: dict) -> str:
    month = d['statement_month']
    txn_lines = []
    for t in d.get('transactions', []):
        sign = "+" if t['amount'] > 0 else ""
        txn_lines.append(
            f"  {t['transaction_date']}  {t['description']:<32} {sign}{fmt_money(t['amount'])}"
        )
    txn_block = "\n".join(txn_lines) if txn_lines else "  (no transactions)"

    large_deposit_block = ""
    if d.get('large_deposits'):
        lines = []
        for ld in d['large_deposits']:
            lines.append(
                f"  {ld['transaction_date']}  {ld['description']:<32} +{fmt_money(ld['amount'])}"
                f"  *** LARGE DEPOSIT — SOURCE UNVERIFIED ***"
            )
        large_deposit_block = (
            "\nFLAGGED LARGE DEPOSITS (>= $5,000)\n"
            + "\n".join(lines) + "\n"
        )

    header = f"BANK STATEMENT — {month}"
    return f"""\
{header}
{'=' * len(header)}
Account Holder : {d['owner_name']}
Statement Date : {fmt_date(d['document_date'])}
Period         : {month}

SUMMARY
  Beginning Balance : {fmt_money(d['beginning_balance'])}
  Total Deposits    : {fmt_money(d['total_deposits'])}
  Ending Balance    : {fmt_money(d['ending_balance'])}
  Average Balance   : {fmt_money(d['average_balance'])}

TRANSACTIONS
{txn_block}
{large_deposit_block}
[OFFICIAL BANK STATEMENT — SUBMITTED BY BORROWER]
"""


def loan_application_txt(d: dict) -> str:
    borrower = d.get('borrower', {})
    co_b = d.get('co_borrower')
    prop = d.get('property', {})
    fin = d.get('declared_financials', {})

    co_block = ""
    if co_b:
        co_block = f"""
CO-BORROWER INFORMATION
  Full Name      : {co_b.get('full_name', 'N/A')}
"""

    return f"""\
UNIFORM RESIDENTIAL LOAN APPLICATION (FORM 1003)
=================================================
Application ID   : {d['application_id']}
Application Date : {fmt_date(d['document_date'])}
Source System    : {d.get('source_system', 'N/A')}

LOAN REQUEST
  Purpose          : {d.get('loan_purpose', 'N/A').replace('_', ' ').title()}
  Product          : {d.get('loan_product', 'N/A').replace('_', ' ').title()}
  Term             : {d.get('loan_term_months', 'N/A')} months
  Requested Amount : {fmt_money(d.get('requested_loan_amount', 0))}
  Purchase Price   : {fmt_money(d.get('purchase_price', 0))}
  Down Payment     : {fmt_money(d.get('down_payment', 0))}
  Est. Interest    : {d.get('interest_rate_estimate', 'N/A')}%
  Occupancy Type   : {d.get('occupancy_type', 'N/A').replace('_', ' ').title()}

BORROWER INFORMATION
  Full Name      : {borrower.get('full_name', 'N/A')}
  Date of Birth  : {fmt_date(borrower.get('date_of_birth', ''))}
  Current Address: {borrower.get('current_address', 'N/A')}
{co_block}
PROPERTY
  Address        : {prop.get('address', 'N/A')}
  City / State   : {prop.get('city', 'N/A')}, {prop.get('state', 'N/A')}
  Property Type  : {prop.get('property_type', 'N/A').replace('_', ' ').title()}

DECLARED FINANCIALS
  Monthly Income : {fmt_money(fin.get('declared_monthly_income', 0))}
  Monthly Debt   : {fmt_money(fin.get('existing_monthly_debt', 0))}
  Liquid Assets  : {fmt_money(fin.get('liquid_assets', 0))}

[BORROWER DECLARATION — SUBMITTED VIA LOAN ORIGINATION SYSTEM]
"""


def appraisal_txt(d: dict) -> str:
    ltv = d.get('calculated_ltv', 'N/A')
    return f"""\
PROPERTY APPRAISAL REPORT
==========================
Report Date    : {fmt_date(d['document_date'])}
Property Type  : {d['property_type'].replace('_', ' ').title()}
Occupancy Type : {d['occupancy_type'].replace('_', ' ').title()}

PROPERTY ADDRESS
  {d['property_address']}

VALUATION
  Purchase Price   : {fmt_money(d['purchase_price'])}
  Appraised Value  : {fmt_money(d['appraised_value'])}
  Calculated LTV   : {ltv}%
  Condition Rating : {d.get('condition_rating', 'N/A')}

APPRAISER'S CERTIFICATION
The subject property has been inspected and appraised in accordance with
USPAP guidelines. The appraised value reflects current market conditions
as of the effective date above.

Appraiser Signature: _________________________
License No.: APR-{d['document_id'][-6:]}
"""


# ─── Main ───────────────────────────────────────────────────────────────────

def main():
    apps = sorted(BASE.glob("01_application/APP-*.json"))
    total = 0

    for app_path in apps:
        app = load(app_path)
        app_id = app['application_id']
        print(f"\n{app_id}")

        # Loan application form (customer-submitted request document)
        write_txt(app_id, "loan_application.txt", loan_application_txt(app))
        total += 1

        # Identity
        p = BASE / "02_identity" / f"{app_id}_id.json"
        if p.exists():
            write_txt(app_id, "identity.txt", identity_txt(load(p)))
            total += 1

        # Paystubs (2 per borrower)
        for seq in [1, 2]:
            p = BASE / "03_income" / f"{app_id}_paystub_{seq}.json"
            if p.exists():
                write_txt(app_id, f"paystub_{seq}.txt", paystub_txt(load(p), seq))
                total += 1

        # Employment verification
        p = BASE / "04_employment" / f"{app_id}_voe.json"
        if p.exists():
            write_txt(app_id, "employment_verification.txt", voe_txt(load(p)))
            total += 1

        # Bank statements (3 per borrower)
        for seq in [1, 2, 3]:
            p = BASE / "05_banking" / f"{app_id}_statement_{seq}.json"
            if p.exists():
                write_txt(app_id, f"bank_statement_{seq}.txt", bank_statement_txt(load(p)))
                total += 1

        # Appraisal
        p = BASE / "07_collateral" / f"{app_id}_appraisal.json"
        if p.exists():
            write_txt(app_id, "appraisal.txt", appraisal_txt(load(p)))
            total += 1

    print(f"\nDone — {total} files written to {RAW.relative_to(BASE.parent)}")


if __name__ == "__main__":
    main()
