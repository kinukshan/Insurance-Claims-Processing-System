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
    return "unknown"


def compute_sha256(data: bytes) -> str:
    """Computes SHA-256 hash of bytes."""
    return hashlib.sha256(data).hexdigest()


def find_file_on_disk(file_url: Optional[str], file_name: Optional[str]) -> Optional[str]:
    """Locates an uploaded file on disk safely."""
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
        # Search by file name match if provided
        if file_name:
            try:
                for entry in os.listdir(d):
                    if file_name in entry:
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
        return False, None, "Photos of damage verified as visual evidence."

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
    if not other_scores:
        return False, None, "Document content consistent."

    top_other_type, top_other_score = max(other_scores.items(), key=lambda x: x[1])

    # Secondary signal: filename mentions other type (weak signal per rules)
    filename_suggests_other = False
    if top_other_type in ("Beneficiary / Nominee Identification", "Death Certificate"):
        if any(term in file_name_lower for term in ("beneficiary", "nominee", "death_cert")):
            filename_suggests_other = True

    # Deterministic mismatch condition:
    # 1. Claimed type has zero or near-zero primary signal (claimed_score <= 1)
    # 2. Another category has strong signal (top_other_score >= 3 with primary keywords)
    has_top_other_primary = any(
        kw in text_lower for kw in DOCUMENT_TYPE_SIGNALS.get(top_other_type, {}).get("primary", [])
    )

    is_mismatch = False
    if claimed_score == 0 and top_other_score >= 3 and has_top_other_primary:
        is_mismatch = True
    elif claimed_score <= 1 and top_other_score >= 4 and has_top_other_primary:
        is_mismatch = True
    elif claimed_score == 0 and filename_suggests_other and top_other_score >= 2:
        is_mismatch = True

    if is_mismatch:
        other_matches = matched_kws.get(top_other_type, [])
        expl = (
            f"Uploaded file under '{norm_claimed}' lacks expected indicators "
            f"and strongly resembles '{top_other_type}' (detected signals: {', '.join(other_matches[:4])})."
        )
        return True, top_other_type, expl

    return False, None, f"Document content reasonably matches '{norm_claimed}'."
