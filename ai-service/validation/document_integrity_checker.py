"""
Deterministic Document Integrity and Consistency Checker

Provides deterministic document verification signals:
- File signature / magic bytes validation
- Safe PDF and text content extraction (safe size limits, error handling)
- Conservative document-type keyword indicator matching
- Cross-category document-type mismatch detection
- Reused physical file / hash detection across distinct document types
- Fails closed on unreadable or corrupted files

Never executes uploaded content. Safe failure on all parse errors.
"""

from __future__ import annotations

import hashlib
import logging
import os
import re
from typing import Optional, Tuple, Dict, List

logger = logging.getLogger(__name__)

# Max file size accepted for server parsing (25 MB)
MAX_PARSE_FILE_SIZE_BYTES = 25 * 1024 * 1024

# Conservative document-type keyword signals (primary = weight 2, secondary = weight 1)
DOCUMENT_TYPE_SIGNALS: Dict[str, Dict[str, List[str]]] = {
    "Police Report": {
        "primary": [
            "police",
            "station",
            "officer",
            "collision",
            "fir",
            "general diary",
            "gd entry",
            "law enforcement",
            "constable",
            "patrol",
            "traffic police",
            "traffic accident",
            "motor vehicle accident",
        ],
        "secondary": [
            "accident",
            "incident",
            "report",
            "investigation",
            "witness",
            "damage",
            "vehicle",
        ],
    },
    "Repair Estimate": {
        "primary": [
            "repair",
            "estimate",
            "labour",
            "labor",
            "parts",
            "workshop",
            "garage",
            "quotation",
            "contractor",
            "body shop",
            "mechanic",
            "body repair",
            "automotive",
        ],
        "secondary": [
            "total",
            "replacement",
            "cost",
            "materials",
            "subtotal",
            "tax",
            "hours",
            "estimated",
        ],
    },
    "Driver License": {
        "primary": [
            "driver",
            "driving",
            "licence",
            "license",
            "permit",
            "class of vehicle",
            "driving licence",
            "driver license",
        ],
        "secondary": [
            "date of birth",
            "dob",
            "expiry",
            "validity",
            "issued",
            "license number",
            "licence number",
        ],
    },
    "Death Certificate": {
        "primary": [
            "death",
            "deceased",
            "cause of death",
            "coroner",
            "died",
            "burial",
            "death certificate",
            "certify the death",
        ],
        "secondary": [
            "certificate",
            "registrar",
            "hospital",
            "medical",
            "date of death",
        ],
    },
    "Beneficiary / Nominee Identification": {
        "primary": [
            "beneficiary",
            "nominee",
            "relationship to insured",
            "nominee identification",
            "beneficiary identification",
            "kin",
            "spouse",
        ],
        "secondary": [
            "identification",
            "identity",
            "national id",
            "passport",
            "full name",
            "nic",
        ],
    },
    "Policy Document": {
        "primary": [
            "policy schedule",
            "policy document",
            "policyholder",
            "sum assured",
            "premium payable",
            "terms and conditions",
        ],
        "secondary": [
            "policy",
            "coverage",
            "insured",
            "insurance",
            "underwriter",
        ],
    },
    "Claim Form": {
        "primary": [
            "claim form",
            "claimant declaration",
            "signature of claimant",
            "claim details",
        ],
        "secondary": [
            "claimant",
            "declaration",
            "policy number",
            "loss",
        ],
    },
    "Medical Report": {
        "primary": [
            "medical report",
            "clinical diagnosis",
            "physician",
            "treatment plan",
            "hospitalization",
            "attending physician",
        ],
        "secondary": [
            "medical",
            "patient",
            "doctor",
            "diagnosis",
            "examination",
            "treatment",
        ],
    },
    "Hospital Bills": {
        "primary": [
            "hospital bill",
            "invoice",
            "room charges",
            "admission fee",
            "discharge summary",
            "pharmacy charges",
        ],
        "secondary": [
            "bill",
            "total",
            "charges",
            "patient",
            "hospital",
        ],
    },
    "Prescription": {
        "primary": [
            "prescription",
            "rx",
            "dosage",
            "dispensed",
            "medication",
            "pharmacy",
        ],
        "secondary": [
            "doctor",
            "instructions",
            "tablets",
            "capsules",
            "prescribed",
        ],
    },
    "Doctor Referral": {
        "primary": [
            "referral letter",
            "referring physician",
            "consultant",
            "referred to",
        ],
        "secondary": [
            "referral",
            "doctor",
            "specialist",
            "patient",
        ],
    },
    "Property Deed": {
        "primary": [
            "title deed",
            "property deed",
            "conveyance",
            "land registry",
            "cadastral",
            "parcel number",
        ],
        "secondary": [
            "deed",
            "property",
            "owner",
            "ownership",
            "land",
        ],
    },
    "Property Valuation": {
        "primary": [
            "valuation report",
            "property valuation",
            "appraisal",
            "surveyor",
            "market valuation",
        ],
        "secondary": [
            "valuation",
            "property",
            "assessed value",
            "replacement cost",
        ],
    },
    "Travel Itinerary": {
        "primary": [
            "flight",
            "boarding pass",
            "e-ticket",
            "airline",
            "passenger",
            "booking reference",
            "itinerary",
        ],
        "secondary": [
            "travel",
            "departure",
            "arrival",
            "hotel",
            "reservation",
        ],
    },
}


def normalize_doc_type_name(doc_type: str) -> str:
    """Normalizes document type aliases."""
    if not doc_type:
        return ""
    trimmed = doc_type.strip()
    lower = trimmed.lower()
    if lower in ("beneficiary id", "beneficiary / nominee identification"):
        return "Beneficiary / Nominee Identification"
    return trimmed


def detect_file_signature(data: bytes) -> str:
    """Detect format from magic bytes."""
    if not data or len(data) < 4:
        return "empty"
    if data.startswith(b"%PDF"):
        return "pdf"
    if data.startswith(b"\xff\xd8\xff"):
        return "jpeg"
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "png"
    if data.startswith(b"RIFF") and len(data) >= 12 and data[8:12] == b"WEBP":
        return "webp"
    # DOCX / XLSX / ZIP archives (PK\x03\x04)
    if data.startswith(b"PK\x03\x04"):
        return "zip_archive"
    return "unknown"


ALLOWED_FILE_SIGNATURES: Dict[str, set[str]] = {
    "Photos of Damage": {"jpeg", "png", "webp"},
    "Police Report": {"pdf"},
    "Repair Estimate": {"pdf"},
    "Driver License": {"jpeg", "png", "webp", "pdf"},
    "Death Certificate": {"pdf"},
    "Medical Report": {"pdf"},
    "Hospital Bills": {"pdf"},
    "Prescription": {"pdf", "jpeg", "png"},
    "Doctor Referral": {"pdf"},
    "Property Deed": {"pdf"},
    "Property Valuation": {"pdf"},
    "Beneficiary / Nominee Identification": {"pdf", "jpeg", "png"},
    "Policy Document": {"pdf"},
    "Claim Form": {"pdf"},
    "Travel Itinerary": {"pdf"},
}

ALLOWED_EXTENSIONS: Dict[str, set[str]] = {
    "Photos of Damage": {".jpg", ".jpeg", ".png", ".webp"},
    "Police Report": {".pdf"},
    "Repair Estimate": {".pdf"},
    "Driver License": {".jpg", ".jpeg", ".png", ".webp", ".pdf"},
    "Death Certificate": {".pdf"},
    "Medical Report": {".pdf"},
    "Hospital Bills": {".pdf"},
    "Prescription": {".pdf", ".jpg", ".jpeg", ".png"},
    "Doctor Referral": {".pdf"},
    "Property Deed": {".pdf"},
    "Property Valuation": {".pdf"},
    "Beneficiary / Nominee Identification": {".pdf", ".jpg", ".jpeg", ".png"},
    "Policy Document": {".pdf"},
    "Claim Form": {".pdf"},
    "Travel Itinerary": {".pdf"},
}


def check_format_compatibility(detected_sig: str, doc_type: str) -> Tuple[bool, str]:
    """
    Checks whether the detected file signature is allowed for the given document type.
    Returns (is_compatible, reason).
    """
    norm_type = normalize_doc_type_name(doc_type)
    allowed = ALLOWED_FILE_SIGNATURES.get(norm_type)
    if allowed is None:
        return True, f"Document type '{norm_type}' accepts any valid file format."
    if detected_sig in allowed:
        return True, f"File format '{detected_sig}' is valid for '{norm_type}'."
    accepted_str = ", ".join(sorted(allowed))
    return False, f"File type '{detected_sig}' is not valid for '{norm_type}'. Accepted formats: {accepted_str}."


def is_format_compatible_with_extension(file_name: str, doc_type: str) -> bool:
    """
    Fallback extension check when physical file bytes are unavailable.
    """
    if not file_name:
        return False
    _, ext = os.path.splitext(file_name.lower())
    norm_type = normalize_doc_type_name(doc_type)
    allowed = ALLOWED_EXTENSIONS.get(norm_type)
    if allowed is None:
        return True
    return ext in allowed


def compute_sha256(data: bytes) -> str:
    """Computes SHA-256 hash of bytes."""
    return hashlib.sha256(data).hexdigest()


def find_file_on_disk(file_url: Optional[str], file_name: Optional[str]) -> Optional[str]:
    """Locates an uploaded file on disk safely."""
    if file_url and os.path.isfile(file_url):
        return os.path.abspath(file_url)

    candidates = []
    if file_url:
        candidates.append(file_url.lstrip("/"))
        candidates.append(os.path.basename(file_url))
    if file_name:
        candidates.append(file_name)

    search_dirs = [
        os.environ.get("UPLOADS_DIR", ""),
        ".",
        "uploads",
        "../backend/src/InsuranceClaims.Api/uploads",
        "backend/src/InsuranceClaims.Api/uploads",
    ]

    for d in search_dirs:
        if not d or not os.path.isdir(d):
            continue
        for c in candidates:
            p = os.path.join(d, c)
            if os.path.isfile(p):
                return os.path.abspath(p)
        # Search by file name match if provided (e.g. {guid}_{filename})
        if file_name:
            try:
                for entry in os.listdir(d):
                    if entry == file_name or entry.endswith(f"_{file_name}"):
                        full_p = os.path.join(d, entry)
                        if os.path.isfile(full_p):
                            return os.path.abspath(full_p)
            except Exception:
                pass
    return None


def extract_pdf_text_safe(file_path: str, max_bytes: int = MAX_PARSE_FILE_SIZE_BYTES) -> Tuple[Optional[str], Optional[str]]:
    """
    Extracts text from a PDF file safely.
    Returns (extracted_text, error_message).
    """
    try:
        size = os.path.getsize(file_path)
        if size == 0:
            return None, "File is empty (0 bytes)."
        if size > max_bytes:
            return None, f"File size exceeds maximum parsing limit ({size} bytes)."

        # Try pypdf first if available
        try:
            import pypdf
            reader = pypdf.PdfReader(file_path)
            pages_text = []
            for page in reader.pages[:20]:  # Limit to first 20 pages
                extracted = page.extract_text()
                if extracted:
                    pages_text.append(extracted)
            return " ".join(pages_text), None
        except Exception as pypdf_err:
            logger.debug("pypdf extraction failed, falling back to basic stream extraction: %s", pypdf_err)

        # Fallback to basic string / stream scanning
        with open(file_path, "rb") as f:
            data = f.read(max_bytes)

        if not data.startswith(b"%PDF"):
            return None, "Invalid PDF header signature."

        strings = re.findall(b"\\(([A-Za-z0-9 ,._/:;()\\-]{3,})\\)", data)
        if strings:
            text = " ".join(s.decode("latin1", errors="ignore") for s in strings)
            return text, None

        return "", None
    except Exception as exc:
        return None, f"Failed to extract PDF text safely: {str(exc)}"


def check_category_validity(norm_type: str, text_lower: str) -> bool:
    """
    Deterministically validates whether extracted text contains credible evidence
    matching the declared DocumentType category.
    Uses grouped signals / threshold logic so legitimate variation is accepted
    without requiring an exact hard-coded template.
    """
    if not text_lower or len(text_lower.strip()) < 30:
        return False

    def contains_any(*kws: str) -> bool:
        return any(k in text_lower for k in kws)

    def count_matches(*kws: str) -> int:
        return sum(1 for k in kws if k in text_lower)

    if norm_type == "Doctor Referral":
        has_referral_intent = contains_any(
            "referral", "referred", "refer", "referring", "kindly evaluate",
            "please evaluate", "specialist", "referred to", "consultation request",
            "evaluate the patient", "for evaluation", "for review", "further review",
            "further management", "second opinion", "referral letter", "referral reason",
        )
        has_medical_context = contains_any(
            "doctor", "physician", "dr.", "dr ", "consultant", "patient",
            "clinic", "hospital", "medical", "clinical",
        )
        has_referral_details = contains_any(
            "referring doctor", "referring physician", "referring dr", "general physician",
            "patient", "patient id", "patient name", "department", "orthopedic",
            "cardiology", "neurology", "oncology", "surgery", "referral reason",
            "reason for referral", "evaluation", "assessment", "complaint", "condition",
            "symptoms", "injury", "specialist", "consultant",
        )
        return has_referral_intent and has_medical_context and has_referral_details

    elif norm_type == "Medical Report":
        has_report_context = contains_any(
            "medical", "clinical", "report", "examination", "lab report",
            "test report", "hospital", "clinic", "pathology", "radiology",
            "investigation", "laboratory",
        )
        has_patient_context = contains_any(
            "patient", "patient name", "patient id", "dob", "date of birth",
            "age", "admitted", "history",
        )
        has_provider_context = contains_any(
            "doctor", "physician", "dr.", "dr ", "consultant", "attending physician",
            "surgeon", "specialist", "practitioner",
        )
        has_clinical_findings = contains_any(
            "diagnosis", "clinical diagnosis", "findings", "clinical findings",
            "assessment", "treatment", "treatment plan", "symptoms", "condition",
            "impression", "complaint", "injury", "prescribed",
        )
        distinct_groups = sum([has_report_context, has_patient_context, has_provider_context, has_clinical_findings])
        return (
            (distinct_groups >= 3 and has_clinical_findings)
            or (has_report_context and has_provider_context and has_clinical_findings)
            or (distinct_groups >= 2 and has_clinical_findings and (has_report_context or has_patient_context))
        )

    elif norm_type == "Hospital Bills":
        has_billing_evidence = contains_any(
            "invoice", "bill", "statement", "charges", "fee", "receipt",
            "payment", "billing", "account", "room charges", "admission fee",
            "pharmacy charges", "discharge summary", "itemized",
        )
        has_facility = contains_any(
            "hospital", "clinic", "medical center", "healthcare", "pharmacy",
            "dispensary", "laboratory", "nawaloka", "medical",
        )
        has_amounts = contains_any(
            "total", "subtotal", "amount", "tax", "balance", "paid",
            "due", "payment status", "invoice no", "bill no", "invoice date",
            "bill date", "lkr", "rs.", "usd", "$", "account no", "patient id",
        )
        return has_billing_evidence and has_amounts and (has_facility or contains_any("patient", "dr.", "doctor"))

    elif norm_type == "Prescription":
        has_rx_context = contains_any(
            "prescription", "prescribed", "rx", "dispensed", "dispense",
            "directions", "instructions", "take", "medicines", "medication", "sig",
        )
        has_dosage = contains_any(
            "dosage", "dose", "tablet", "tablets", "capsule", "capsules",
            "mg", "ml", "syrup", "ointment", "drops", "daily", "times daily",
            "every 8 hours", "every 6 hours", "od", "bd", "tid", "qid",
            "stat", "prn", "paracetamol", "ibuprofen", "amoxicillin",
        )
        has_provider_or_patient = contains_any(
            "doctor", "physician", "dr.", "dr ", "prescribed by", "patient",
            "patient name", "patient id", "clinic", "hospital", "pharmacy",
        )
        return (has_rx_context or has_dosage) and has_provider_or_patient

    elif norm_type == "Police Report":
        p = contains_any("police", "station", "officer", "collision", "fir", "general diary", "gd entry", "law enforcement", "constable", "patrol", "traffic police", "traffic accident", "motor vehicle accident")
        s_count = count_matches("accident", "incident", "report", "investigation", "witness", "damage", "vehicle")
        return p or s_count >= 2

    elif norm_type == "Repair Estimate":
        p = contains_any("repair", "estimate", "labour", "labor", "parts", "workshop", "garage", "quotation", "contractor", "body shop", "mechanic", "body repair", "automotive")
        s = contains_any("total", "replacement", "cost", "materials", "subtotal", "tax", "hours", "estimated")
        return p and (s or count_matches("repair", "estimate", "parts", "labor", "labour", "workshop") >= 2)

    elif norm_type == "Driver License":
        return contains_any("driver", "driving", "licence", "license", "permit", "class of vehicle", "driving licence", "driver license")

    elif norm_type == "Death Certificate":
        return contains_any("death", "deceased", "cause of death", "coroner", "died", "burial", "death certificate", "certify the death")

    elif norm_type == "Beneficiary / Nominee Identification":
        p = contains_any("beneficiary", "nominee", "relationship to insured", "nominee identification", "beneficiary identification", "kin", "spouse")
        s = contains_any("identification", "identity", "national id", "passport", "full name", "nic")
        return p or (s and ("beneficiary" in text_lower or "nominee" in text_lower))

    elif norm_type == "Policy Document":
        p = contains_any("policy schedule", "policy document", "policyholder", "sum assured", "premium payable", "terms and conditions")
        s_count = count_matches("policy", "coverage", "insured", "insurance", "underwriter")
        return p or s_count >= 2

    elif norm_type == "Claim Form":
        p = contains_any("claim form", "claimant declaration", "signature of claimant", "claim details")
        s_count = count_matches("claimant", "declaration", "policy number", "loss")
        return p or s_count >= 2

    elif norm_type == "Property Deed":
        p = contains_any("title deed", "property deed", "conveyance", "land registry", "cadastral", "parcel number")
        s_count = count_matches("deed", "property", "owner", "ownership", "land")
        return p or (s_count >= 2 and ("deed" in text_lower or "title" in text_lower))

    elif norm_type == "Property Valuation":
        p = contains_any("valuation report", "property valuation", "appraisal", "surveyor", "market valuation")
        s_count = count_matches("valuation", "property", "assessed value", "replacement cost")
        return p or s_count >= 2

    elif norm_type == "Travel Itinerary":
        p = contains_any("flight", "boarding pass", "e-ticket", "airline", "passenger", "booking reference", "itinerary")
        s_count = count_matches("travel", "departure", "arrival", "hotel", "reservation")
        return p or s_count >= 2

    return True


def evaluate_content_consistency(
    text: str,
    claimed_type: str,
    file_name: str = "",
) -> Tuple[bool, Optional[str], str]:
    """
    Determines if extracted text is reasonably consistent with claimed_type
    or is an obvious mismatch resembling another document type.

    Returns:
        (is_mismatch, detected_other_type, explanation)
    """
    norm_claimed = normalize_doc_type_name(claimed_type)
    text_lower = text.lower() if text else ""
    file_name_lower = file_name.lower() if file_name else ""

    # Image-based document types (e.g., Photos of Damage) don't require text
    if norm_claimed == "Photos of Damage":
        return False, None, "Photos of Damage format compatibility validated separately via file signature check."

    # If text is too short to evaluate (< 30 characters), rely on secondary checks only
    if len(text_lower.strip()) < 30:
        return False, None, "Insufficient text content for conclusive semantic evaluation."

    scores: Dict[str, int] = {}
    matched_kws: Dict[str, List[str]] = {}

    for ctype, sigs in DOCUMENT_TYPE_SIGNALS.items():
        primary_hits = [kw for kw in sigs["primary"] if kw in text_lower]
        secondary_hits = [kw for kw in sigs["secondary"] if kw in text_lower]
        score = (len(primary_hits) * 2) + len(secondary_hits)
        scores[ctype] = score
        matched_kws[ctype] = primary_hits + secondary_hits

    claimed_score = scores.get(norm_claimed, 0)

    # Find highest matching other category
    other_scores = {k: v for k, v in scores.items() if k != norm_claimed}
    top_other_type = None
    top_other_score = 0
    if other_scores:
        top_other_type, top_other_score = max(other_scores.items(), key=lambda x: x[1])

    # Secondary signal: filename mentions other type (weak signal per rules)
    filename_suggests_other = False
    if top_other_type in ("Beneficiary / Nominee Identification", "Death Certificate"):
        if any(term in file_name_lower for term in ("beneficiary", "nominee", "death_cert")):
            filename_suggests_other = True

    has_top_other_primary = bool(
        top_other_type
        and any(
            kw in text_lower for kw in DOCUMENT_TYPE_SIGNALS.get(top_other_type, {}).get("primary", [])
        )
    )

    # 1. Evaluate whether the document satisfies the semantic evidence rules for declared category
    is_declared_category_valid = check_category_validity(norm_claimed, text_lower)

    if not is_declared_category_valid:
        if top_other_score >= 3 and has_top_other_primary and top_other_type:
            other_matches = matched_kws.get(top_other_type, [])
            expl = (
                f"Uploaded file under '{norm_claimed}' lacks expected indicators "
                f"and strongly resembles '{top_other_type}' (detected signals: {', '.join(other_matches[:4])})."
            )
            return True, top_other_type, expl

        if filename_suggests_other and top_other_score >= 2 and top_other_type:
            other_matches = matched_kws.get(top_other_type, [])
            expl = (
                f"Uploaded file under '{norm_claimed}' lacks expected indicators "
                f"and strongly resembles '{top_other_type}' (detected signals: {', '.join(other_matches[:4])})."
            )
            return True, top_other_type, expl

        # Unrelated content (e.g. software architecture slides, recipes, novels)
        return True, None, f"The document content does not appear consistent with {norm_claimed}."

    # 2. Even if declared category has some matches, check if another category overwhelmingly dominates
    is_cross_type_mismatch = False
    if claimed_score <= 1 and top_other_score >= 4 and has_top_other_primary and top_other_type:
        is_cross_type_mismatch = True

    if is_cross_type_mismatch and top_other_type:
        other_matches = matched_kws.get(top_other_type, [])
        expl = (
            f"Uploaded file under '{norm_claimed}' lacks expected indicators "
            f"and strongly resembles '{top_other_type}' (detected signals: {', '.join(other_matches[:4])})."
        )
        return True, top_other_type, expl

    return False, None, f"Document content is consistent with '{norm_claimed}'."
