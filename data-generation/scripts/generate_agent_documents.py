#!/usr/bin/env python3
"""
Generate PDF and PNG agent inputs from corpus Bronze JSON.

Output: data-generation/corpus/raw/pdf/APP-XXX/<category>/ and raw/png/APP-XXX/<category>/

Categories (demo formats for AI document agents):
  personal_details/  — borrower form (PDF) + driver's license scan (PNG)
  income/            — paystub PDFs + PNG scans
  employment/        — verification of employment (PDF + PNG scan)
  banking/           — bank statement PDFs + PNG scans (3 months)
  collateral/        — property appraisal (PDF + PNG summary)
  credit_history/    — bureau credit report (PDF)
  loan_amount/       — loan application / amount summary (PDF)

Format decisions: see FORMAT_DECISIONS.md

Source folders:
  01_application/  personal details, declared income, loan amount
  02_identity/     personal details (ID scan)
  03_income/       income paystubs
  04_employment/   employment verification
  06_credit/       credit history (bureau pull, not borrower-submitted)

Usage:
  pip install -r requirements.txt
  python3 generate_agent_documents.py
  python3 generate_agent_documents.py --app APP-001
  python3 generate_agent_documents.py --formats pdf
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
BRONZE = DATA_GEN / "corpus" / "bronze"
RAW = DATA_GEN / "corpus" / "raw"


def category_dir(app_id: str, category: str, fmt: str) -> Path:
    return RAW / fmt / app_id / category

# Muted palette for synthetic document styling
HEADER_BG = colors.HexColor("#1e3a5f")
ACCENT = colors.HexColor("#2c5282")
LIGHT_ROW = colors.HexColor("#f7fafc")


def load(path: Path) -> dict:
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def fmt_money(v: float | int) -> str:
    return f"${float(v):,.2f}"


def fmt_date(d: str) -> str:
    try:
        return datetime.strptime(d, "%Y-%m-%d").strftime("%B %d, %Y")
    except (ValueError, TypeError):
        return str(d)


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def write_pdf(path: Path, title: str, subtitle: str, sections: list[tuple[str, list[tuple[str, str]]]]) -> None:
    """Write a simple branded PDF with titled sections of label/value rows."""
    ensure_dir(path.parent)
    doc = SimpleDocTemplate(
        str(path),
        pagesize=letter,
        leftMargin=0.75 * inch,
        rightMargin=0.75 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
    )
    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        "DocTitle",
        parent=styles["Heading1"],
        fontSize=16,
        textColor=HEADER_BG,
        spaceAfter=4,
    )
    subtitle_style = ParagraphStyle(
        "DocSubtitle",
        parent=styles["Normal"],
        fontSize=9,
        textColor=colors.grey,
        spaceAfter=14,
    )
    section_style = ParagraphStyle(
        "Section",
        parent=styles["Heading2"],
        fontSize=11,
        textColor=ACCENT,
        spaceBefore=10,
        spaceAfter=6,
    )
    footer_style = ParagraphStyle(
        "Footer",
        parent=styles["Normal"],
        fontSize=8,
        textColor=colors.grey,
        spaceBefore=20,
    )

    story: list = [
        Paragraph(title, title_style),
        Paragraph(subtitle, subtitle_style),
    ]

    for section_title, rows in sections:
        story.append(Paragraph(section_title, section_style))
        if not rows:
            continue
        table = Table([["Field", "Value"]] + [[k, v] for k, v in rows], colWidths=[2.2 * inch, 4.3 * inch])
        table.setStyle(
            TableStyle(
                [
                    ("BACKGROUND", (0, 0), (-1, 0), HEADER_BG),
                    ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                    ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                    ("FONTSIZE", (0, 0), (-1, -1), 9),
                    ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, LIGHT_ROW]),
                    ("GRID", (0, 0), (-1, -1), 0.25, colors.lightgrey),
                    ("VALIGN", (0, 0), (-1, -1), "TOP"),
                    ("LEFTPADDING", (0, 0), (-1, -1), 6),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                    ("TOPPADDING", (0, 0), (-1, -1), 5),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
                ]
            )
        )
        story.append(table)
        story.append(Spacer(1, 6))

    story.append(
        Paragraph(
            "SYNTHETIC DOCUMENT — Generated for agentic loan processing demo. Not for production use.",
            footer_style,
        )
    )
    doc.build(story)


def _font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "arial.ttf",
    ]
    for path in candidates:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def write_id_scan_png(path: Path, identity: dict) -> None:
    """Render a driver's-license-style scan image."""
    ensure_dir(path.parent)
    w, h = 860, 540
    img = Image.new("RGB", (w, h), color=(245, 242, 235))
    draw = ImageDraw.Draw(img)

    # Card background
    margin = 30
    draw.rounded_rectangle(
        [margin, margin, w - margin, h - margin],
        radius=18,
        fill=(230, 240, 248),
        outline=(30, 64, 110),
        width=3,
    )

    title_font = _font(28, bold=True)
    label_font = _font(16)
    value_font = _font(20, bold=True)
    small_font = _font(14)

    draw.text((55, 50), "DRIVER LICENSE", fill=(30, 64, 110), font=title_font)
    draw.text((55, 95), identity.get("full_name", ""), fill=(20, 20, 20), font=value_font)

    fields = [
        ("DOB", fmt_date(identity.get("date_of_birth", ""))),
        ("Address", identity.get("address", "")),
        ("License No.", identity.get("document_number", "")),
        ("Expires", fmt_date(identity.get("expiry_date", ""))),
    ]
    y = 150
    for label, value in fields:
        draw.text((55, y), f"{label}:", fill=(80, 80, 80), font=label_font)
        draw.text((200, y), value, fill=(20, 20, 20), font=label_font)
        y += 42

    # Photo placeholder
    draw.rounded_rectangle([w - 230, 130, w - 70, 310], radius=8, fill=(200, 210, 220), outline=(120, 130, 140))
    draw.text((w - 205, 200), "PHOTO", fill=(100, 110, 120), font=small_font)

    draw.text(
        (55, h - 70),
        f"SCAN DATE: {fmt_date(identity.get('document_date', ''))}  |  SYNTHETIC ID DOCUMENT",
        fill=(120, 120, 120),
        font=small_font,
    )
    img.save(path, format="PNG")


def write_paystub_png(path: Path, paystub: dict, seq: int) -> None:
    """Render a paystub as a PNG scan."""
    ensure_dir(path.parent)
    w, h = 900, 620
    img = Image.new("RGB", (w, h), color=(252, 252, 250))
    draw = ImageDraw.Draw(img)
    title_font = _font(24, bold=True)
    body_font = _font(16)
    bold_font = _font(18, bold=True)

    draw.text((40, 30), f"EARNINGS STATEMENT — PAY PERIOD {seq}", fill=(30, 64, 110), font=title_font)
    lines = [
        f"Employer:  {paystub.get('employer', '')}",
        f"Employee:  {paystub.get('employee_name', '')}",
        f"Pay Date:  {fmt_date(paystub.get('pay_date', ''))}",
        f"Period:    {fmt_date(paystub.get('pay_period_start', ''))} – {fmt_date(paystub.get('pay_period_end', ''))}",
        "",
        f"Gross Pay: {fmt_money(paystub.get('gross_pay', 0))}",
        f"Net Pay:   {fmt_money(paystub.get('net_pay', 0))}",
        f"YTD Gross: {fmt_money(paystub.get('ytd_gross', 0))}",
    ]
    y = 90
    for line in lines:
        font = bold_font if line.startswith("Gross") or line.startswith("Net") else body_font
        draw.text((40, y), line, fill=(30, 30, 30), font=font)
        y += 36

    draw.text((40, h - 50), "SYNTHETIC PAYSTUB — SUBMITTED BY BORROWER", fill=(130, 130, 130), font=body_font)
    img.save(path, format="PNG")


def bank_statement_sections(stmt: dict) -> list[tuple[str, list[tuple[str, str]]]]:
    sections: list[tuple[str, list[tuple[str, str]]]] = [
        (
            "Account Summary",
            [
                ("Account Holder", stmt.get("owner_name", "")),
                ("Statement Month", stmt.get("statement_month", "")),
                ("Statement Date", fmt_date(stmt.get("document_date", ""))),
                ("Beginning Balance", fmt_money(stmt.get("beginning_balance", 0))),
                ("Total Deposits", fmt_money(stmt.get("total_deposits", 0))),
                ("Ending Balance", fmt_money(stmt.get("ending_balance", 0))),
                ("Average Balance", fmt_money(stmt.get("average_balance", 0))),
            ],
        ),
    ]
    txn_rows = [
        (f"{t['transaction_date']} — {t['description']}", fmt_money(t["amount"]))
        for t in stmt.get("transactions", [])
    ]
    if txn_rows:
        sections.append(("Transactions", txn_rows))
    large = stmt.get("large_deposits") or []
    if large:
        sections.append(
            (
                "Flagged Large Deposits (>= $5,000)",
                [
                    (f"{ld['transaction_date']} — {ld['description']}", f"{fmt_money(ld['amount'])} — SOURCE UNVERIFIED")
                    for ld in large
                ],
            )
        )
    return sections


def write_bank_statement_png(path: Path, stmt: dict, seq: int) -> None:
    ensure_dir(path.parent)
    w, h = 920, 720
    img = Image.new("RGB", (w, h), color=(252, 252, 250))
    draw = ImageDraw.Draw(img)
    title_font = _font(22, bold=True)
    body_font = _font(15)
    bold_font = _font(16, bold=True)

    month = stmt.get("statement_month", "")
    draw.text((40, 28), f"BANK STATEMENT — {month} (#{seq})", fill=(30, 64, 110), font=title_font)
    header_lines = [
        f"Account Holder : {stmt.get('owner_name', '')}",
        f"Statement Date : {fmt_date(stmt.get('document_date', ''))}",
        f"Ending Balance : {fmt_money(stmt.get('ending_balance', 0))}",
    ]
    y = 75
    for line in header_lines:
        draw.text((40, y), line, fill=(30, 30, 30), font=body_font)
        y += 30

    draw.text((40, y + 10), "TRANSACTIONS", fill=(30, 64, 110), font=bold_font)
    y += 45
    for t in stmt.get("transactions", []):
        sign = "+" if t["amount"] > 0 else ""
        line = f"{t['transaction_date']}  {t['description'][:28]:<28} {sign}{fmt_money(t['amount'])}"
        draw.text((40, y), line, fill=(30, 30, 30), font=body_font)
        y += 28

    for ld in stmt.get("large_deposits") or []:
        draw.text(
            (40, y),
            f"*** LARGE DEPOSIT: {ld['transaction_date']} {ld['description']} +{fmt_money(ld['amount'])} ***",
            fill=(160, 40, 40),
            font=body_font,
        )
        y += 28

    draw.text((40, h - 45), "SYNTHETIC BANK STATEMENT — SUBMITTED BY BORROWER", fill=(130, 130, 130), font=body_font)
    img.save(path, format="PNG")


def write_appraisal_png(path: Path, appraisal: dict) -> None:
    ensure_dir(path.parent)
    w, h = 900, 560
    img = Image.new("RGB", (w, h), color=(250, 250, 248))
    draw = ImageDraw.Draw(img)
    title_font = _font(24, bold=True)
    body_font = _font(17)
    label_font = _font(16)

    draw.text((40, 30), "PROPERTY APPRAISAL — SUMMARY PAGE", fill=(30, 64, 110), font=title_font)
    fields = [
        ("Report Date", fmt_date(appraisal.get("document_date", ""))),
        ("Property", appraisal.get("property_address", "")),
        ("Property Type", appraisal.get("property_type", "").replace("_", " ").title()),
        ("Purchase Price", fmt_money(appraisal.get("purchase_price", 0))),
        ("Appraised Value", fmt_money(appraisal.get("appraised_value", 0))),
        ("Calculated LTV", f"{appraisal.get('calculated_ltv', 'N/A')}%"),
        ("Condition", appraisal.get("condition_rating", "N/A")),
    ]
    y = 90
    for label, value in fields:
        draw.text((40, y), f"{label}:", fill=(80, 80, 80), font=label_font)
        draw.text((220, y), str(value), fill=(20, 20, 20), font=body_font)
        y += 38

    draw.text((40, h - 45), "SYNTHETIC APPRAISAL SUMMARY — USPAP REPORT EXCERPT", fill=(130, 130, 130), font=label_font)
    img.save(path, format="PNG")


def write_voe_png(path: Path, voe: dict) -> None:
    ensure_dir(path.parent)
    w, h = 880, 640
    img = Image.new("RGB", (w, h), color=(255, 255, 253))
    draw = ImageDraw.Draw(img)
    title_font = _font(22, bold=True)
    body_font = _font(16)

    draw.text((40, 35), "VERIFICATION OF EMPLOYMENT", fill=(30, 64, 110), font=title_font)
    lines = [
        f"Issued by : {voe.get('employer', '')}",
        f"Date      : {fmt_date(voe.get('document_date', ''))}",
        "",
        f"Employee  : {voe.get('employee_name', '')}",
        f"Title     : {voe.get('job_title', '')}",
        f"Status    : {voe.get('employment_status', '').replace('_', ' ').title()}",
        f"Hire Date : {fmt_date(voe.get('hire_date', ''))}",
        "",
        f"Base Salary : {fmt_money(voe.get('base_salary_annual', 0))} / year",
        f"Bonus       : {fmt_money(voe.get('bonus_annual', 0))} / year",
        "",
        "Authorized Signature: _________________________",
    ]
    y = 85
    for line in lines:
        draw.text((40, y), line, fill=(30, 30, 30), font=body_font)
        y += 32

    draw.text((40, h - 45), "SYNTHETIC VOE — SCANNED HR LETTER", fill=(130, 130, 130), font=body_font)
    img.save(path, format="PNG")


# ─── Category builders ───────────────────────────────────────────────────────


def generate_personal_details(app_id: str, app: dict, identity: dict, formats: set[str]) -> int:
    count = 0
    borrower = app.get("borrower", {})
    co = app.get("co_borrower")

    rows = [
        ("Application ID", app_id),
        ("Borrower Name", borrower.get("full_name", "")),
        ("Date of Birth", fmt_date(borrower.get("date_of_birth", ""))),
        ("Current Address", borrower.get("current_address", "")),
        ("Property Address", app.get("property", {}).get("address", "")),
        ("Property City / State", f"{app.get('property', {}).get('city', '')}, {app.get('property', {}).get('state', '')}"),
        ("Occupancy", app.get("occupancy_type", "").replace("_", " ").title()),
    ]
    if co:
        rows.append(("Co-Borrower", co.get("full_name", "")))

    if "pdf" in formats:
        write_pdf(
            category_dir(app_id, "personal_details", "pdf") / "personal_information.pdf",
            "Borrower Personal Information",
            f"Application {app_id}  |  Document date: {fmt_date(app.get('document_date', ''))}",
            [
                ("Applicant", rows),
                (
                    "Identity Document (on file)",
                    [
                        ("Document Type", identity.get("document_type", "drivers_license").replace("_", " ").title()),
                        ("Name on ID", identity.get("full_name", "")),
                        ("ID Address", identity.get("address", "")),
                        ("License Number", identity.get("document_number", "")),
                        ("Expiry", fmt_date(identity.get("expiry_date", ""))),
                    ],
                ),
            ],
        )
        count += 1

    if "png" in formats:
        write_id_scan_png(category_dir(app_id, "personal_details", "png") / "drivers_license.png", identity)
        count += 1

    return count


def generate_income(app_id: str, app: dict, formats: set[str]) -> int:
    count = 0
    fin = app.get("declared_financials", {})

    if "pdf" in formats:
        write_pdf(
            category_dir(app_id, "income", "pdf") / "declared_income_summary.pdf",
            "Declared Income Summary",
            f"Application {app_id}  |  Source: loan origination system",
            [
                (
                    "Borrower Declarations",
                    [
                        ("Declared Monthly Income", fmt_money(fin.get("declared_monthly_income", 0))),
                        ("Declared Annual Income (est.)", fmt_money(float(fin.get("declared_monthly_income", 0)) * 12)),
                        ("Existing Monthly Debt", fmt_money(fin.get("existing_monthly_debt", 0))),
                        ("Liquid Assets", fmt_money(fin.get("liquid_assets", 0))),
                    ],
                ),
            ],
        )
        count += 1

    for seq in [1, 2]:
        pay_path = BRONZE / "03_income" / f"{app_id}_paystub_{seq}.json"
        if not pay_path.exists():
            continue
        pay = load(pay_path)
        if "pdf" in formats:
            write_pdf(
                category_dir(app_id, "income", "pdf") / f"paystub_{seq}.pdf",
                f"Earnings Statement — Paystub {seq}",
                f"{pay.get('employer', '')}  |  Pay date: {fmt_date(pay.get('pay_date', ''))}",
                [
                    (
                        "Pay Details",
                        [
                            ("Employee", pay.get("employee_name", "")),
                            ("Employer", pay.get("employer", "")),
                            ("Pay Frequency", pay.get("pay_frequency", "").replace("_", " ").title()),
                            ("Pay Period", f"{fmt_date(pay.get('pay_period_start', ''))} – {fmt_date(pay.get('pay_period_end', ''))}"),
                            ("Gross Pay", fmt_money(pay.get("gross_pay", 0))),
                            ("Net Pay", fmt_money(pay.get("net_pay", 0))),
                            ("YTD Gross", fmt_money(pay.get("ytd_gross", 0))),
                        ],
                    ),
                ],
            )
            count += 1
        if "png" in formats:
            write_paystub_png(category_dir(app_id, "income", "png") / f"paystub_{seq}.png", pay, seq)
            count += 1

    return count


def generate_employment(app_id: str, formats: set[str]) -> int:
    voe_path = BRONZE / "04_employment" / f"{app_id}_voe.json"
    if not voe_path.exists():
        return 0
    voe = load(voe_path)
    count = 0

    if "pdf" in formats:
        write_pdf(
            category_dir(app_id, "employment", "pdf") / "employment_verification.pdf",
            "Verification of Employment",
            f"Issued by {voe.get('employer', '')}  |  {fmt_date(voe.get('document_date', ''))}",
            [
                (
                    "Employment",
                    [
                        ("Employee", voe.get("employee_name", "")),
                        ("Employer", voe.get("employer", "")),
                        ("Job Title", voe.get("job_title", "")),
                        ("Status", voe.get("employment_status", "").replace("_", " ").title()),
                        ("Hire Date", fmt_date(voe.get("hire_date", ""))),
                    ],
                ),
                (
                    "Compensation",
                    [
                        ("Base Annual Salary", fmt_money(voe.get("base_salary_annual", 0))),
                        ("Annual Bonus", fmt_money(voe.get("bonus_annual", 0))),
                    ],
                ),
            ],
        )
        count += 1

    if "png" in formats:
        write_voe_png(category_dir(app_id, "employment", "png") / "employment_verification.png", voe)
        count += 1

    return count


def generate_banking(app_id: str, formats: set[str]) -> int:
    count = 0
    for seq in [1, 2, 3]:
        stmt_path = BRONZE / "05_banking" / f"{app_id}_statement_{seq}.json"
        if not stmt_path.exists():
            continue
        stmt = load(stmt_path)
        month = stmt.get("statement_month", "")
        if "pdf" in formats:
            write_pdf(
                category_dir(app_id, "banking", "pdf") / f"bank_statement_{seq}.pdf",
                f"Bank Statement — {month}",
                f"Account holder: {stmt.get('owner_name', '')}  |  Period: {month}",
                bank_statement_sections(stmt),
            )
            count += 1
        if "png" in formats:
            write_bank_statement_png(
                category_dir(app_id, "banking", "png") / f"bank_statement_{seq}.png",
                stmt,
                seq,
            )
            count += 1
    return count


def generate_collateral(app_id: str, formats: set[str]) -> int:
    appraisal_path = BRONZE / "07_collateral" / f"{app_id}_appraisal.json"
    if not appraisal_path.exists():
        return 0
    appraisal = load(appraisal_path)
    count = 0

    if "pdf" in formats:
        write_pdf(
            category_dir(app_id, "collateral", "pdf") / "appraisal.pdf",
            "Property Appraisal Report",
            f"{appraisal.get('property_address', '')}  |  {fmt_date(appraisal.get('document_date', ''))}",
            [
                (
                    "Subject Property",
                    [
                        ("Address", appraisal.get("property_address", "")),
                        ("Property Type", appraisal.get("property_type", "").replace("_", " ").title()),
                        ("Occupancy", appraisal.get("occupancy_type", "").replace("_", " ").title()),
                    ],
                ),
                (
                    "Valuation",
                    [
                        ("Purchase Price", fmt_money(appraisal.get("purchase_price", 0))),
                        ("Appraised Value", fmt_money(appraisal.get("appraised_value", 0))),
                        ("Calculated LTV", f"{appraisal.get('calculated_ltv', 'N/A')}%"),
                        ("Condition Rating", appraisal.get("condition_rating", "N/A")),
                    ],
                ),
            ],
        )
        count += 1

    if "png" in formats:
        write_appraisal_png(category_dir(app_id, "collateral", "png") / "appraisal.png", appraisal)
        count += 1

    return count


def generate_credit_history(app_id: str, formats: set[str]) -> int:
    credit_path = BRONZE / "06_credit" / f"{app_id}_credit.json"
    if not credit_path.exists():
        return 0
    credit = load(credit_path)
    count = 0

    tradeline_rows = [
        (t.get("type", "").replace("_", " ").title(), f"{t.get('status', '')} — balance {fmt_money(t.get('balance', 0))}")
        for t in credit.get("tradelines", [])
    ]

    if "pdf" in formats:
        sections = [
            (
                "Bureau Summary",
                [
                    ("Borrower", credit.get("borrower_name", "")),
                    ("Credit Score", str(credit.get("credit_score", ""))),
                    ("Credit Utilization", f"{credit.get('credit_utilization', 0)}%"),
                    ("Monthly Debt Obligations", fmt_money(credit.get("monthly_debt_obligations", 0))),
                    ("Delinquencies (12 mo)", str(credit.get("delinquencies_12m", 0))),
                    ("Recent Inquiries", str(credit.get("recent_inquiries", 0))),
                    ("Collections", "Yes" if credit.get("collections_flag") else "No"),
                    ("Bankruptcy", "Yes" if credit.get("bankruptcy_flag") else "No"),
                ],
            ),
        ]
        if tradeline_rows:
            sections.append(("Tradelines", tradeline_rows))

        write_pdf(
            category_dir(app_id, "credit_history", "pdf") / "credit_report.pdf",
            "Credit Bureau Report",
            f"Pull date: {fmt_date(credit.get('document_date', ''))}  |  Source: credit bureau (lender pull)",
            sections,
        )
        count += 1

    return count


def generate_loan_amount(app_id: str, app: dict, formats: set[str]) -> int:
    count = 0
    prop = app.get("property", {})

    ltv = ""
    if app.get("purchase_price") and app.get("requested_loan_amount"):
        ltv = f"{(app['requested_loan_amount'] / app['purchase_price']) * 100:.1f}%"

    if "pdf" in formats:
        write_pdf(
            category_dir(app_id, "loan_amount", "pdf") / "loan_application_summary.pdf",
            "Loan Application — Amount & Terms",
            f"Application {app_id}  |  {fmt_date(app.get('document_date', ''))}",
            [
                (
                    "Loan Request",
                    [
                        ("Loan Purpose", app.get("loan_purpose", "").replace("_", " ").title()),
                        ("Loan Product", app.get("loan_product", "").replace("_", " ").title()),
                        ("Requested Loan Amount", fmt_money(app.get("requested_loan_amount", 0))),
                        ("Purchase Price", fmt_money(app.get("purchase_price", 0))),
                        ("Down Payment", fmt_money(app.get("down_payment", 0))),
                        ("Loan-to-Value (est.)", ltv),
                        ("Term (months)", str(app.get("loan_term_months", ""))),
                        ("Interest Rate (est.)", f"{app.get('interest_rate_estimate', 'N/A')}%"),
                    ],
                ),
                (
                    "Subject Property",
                    [
                        ("Address", prop.get("address", "")),
                        ("City", prop.get("city", "")),
                        ("State", prop.get("state", "")),
                        ("Property Type", prop.get("property_type", "").replace("_", " ").title()),
                    ],
                ),
            ],
        )
        count += 1

    return count


def generate_for_app(app_id: str, formats: set[str]) -> int:
    app_path = BRONZE / "01_application" / f"{app_id}.json"
    identity_path = BRONZE / "02_identity" / f"{app_id}_id.json"
    if not app_path.exists():
        raise FileNotFoundError(f"Application not found: {app_path}")
    if not identity_path.exists():
        raise FileNotFoundError(f"Identity not found: {identity_path}")

    app = load(app_path)
    identity = load(identity_path)

    total = 0
    total += generate_personal_details(app_id, app, identity, formats)
    total += generate_income(app_id, app, formats)
    total += generate_employment(app_id, formats)
    total += generate_banking(app_id, formats)
    total += generate_collateral(app_id, formats)
    total += generate_credit_history(app_id, formats)
    total += generate_loan_amount(app_id, app, formats)
    return total


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate PDF/PNG agent inputs from corpus Bronze JSON.")
    parser.add_argument("--app", help="Single application id (e.g. APP-001). Default: all apps.")
    parser.add_argument(
        "--formats",
        default="pdf,png",
        help="Comma-separated output formats: pdf, png (default: pdf,png)",
    )
    args = parser.parse_args()

    formats = {f.strip().lower() for f in args.formats.split(",") if f.strip()}
    if not formats <= {"pdf", "png"}:
        raise SystemExit("Supported formats: pdf, png")

    if args.app:
        apps = [args.app]
    else:
        apps = sorted(p.stem for p in BRONZE.glob("01_application/APP-*.json"))

    grand_total = 0
    print(f"Generating agent inputs → {RAW.relative_to(DATA_GEN.parent)}/{{pdf,png}}")
    print(f"Formats: {', '.join(sorted(formats))}\n")

    for app_id in apps:
        n = generate_for_app(app_id, formats)
        grand_total += n
        print(f"  {app_id}: {n} files")

    print(f"\nDone — {grand_total} files written for {len(apps)} application(s).")


if __name__ == "__main__":
    main()
