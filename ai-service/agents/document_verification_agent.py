"""
Document Verification Agent

Responsibility:
    - Check required claim documents against type-specific checklists
    - Check document completeness
    - Identify missing items and inconsistencies
    - Check dates, receipts, and evidence consistency
    - Return structured verification output
    - Fail safely on any error

Does NOT override deterministic policy/business rules.
No hidden chain-of-thought storage.
"""

from __future__ import annotations

import logging
import os
from datetime import date, datetime
from schemas.claim_schema import DocumentVerificationRequest, DocumentData
from schemas.document_result_schema import DocumentVerificationResult, DocumentInconsistency

from validation.document_integrity_checker import (
    extract_pdf_text_safe,
    evaluate_content_consistency,
    find_file_on_disk,
    compute_sha256,
    detect_file_signature,
    normalize_doc_type_name,
    check_format_compatibility,
    is_format_compatible_with_extension,
    ALLOWED_EXTENSIONS,
)

logger = logging.getLogger(__name__)


class DocumentVerificationAgent:
    """
    Agentic AI agent that verifies claim documents against
    required checklists and checks for data inconsistencies.
    """

    # Required documents per claim type
    REQUIRED_DOCUMENTS: dict[str, list[str]] = {
        "Auto": ["Police Report", "Photos of Damage", "Repair Estimate", "Driver License"],
        "Motor": ["Police Report", "Photos of Damage", "Repair Estimate", "Driver License"],
        "Home": ["Photos of Damage", "Repair Estimate", "Property Deed"],
        "Health": ["Medical Report", "Hospital Bills", "Prescription", "Doctor Referral"],
        "Life": [
            "Death Certificate",
            "Policy Document",
            "Beneficiary / Nominee Identification",
            "Claim Form",
        ],
        "Travel": ["Travel Itinerary", "Receipts", "Incident Report"],
        "Property": ["Photos of Damage", "Repair Estimate", "Property Valuation"],
        "Liability": ["Incident Report", "Third Party Claim", "Legal Notice"],
        "Other": ["Supporting Document"],
    }

    def __init__(self, gemini_client_instance=None):
        if gemini_client_instance is not None:
            self._gemini = gemini_client_instance
        else:
            from services.gemini_client import gemini_client
            self._gemini = gemini_client

    def verify(self, request: DocumentVerificationRequest) -> DocumentVerificationResult:
        """
        Verify submitted documents against the required checklist
        and check for data inconsistencies with hybrid Gemini reasoning.

        Args:
            request: The verification request containing claim data and documents.

        Returns:
            Structured DocumentVerificationResult.
        """
        try:
            # Deterministic checks run first and are authoritative
            inconsistencies = self._check_inconsistencies(request)
            missing_items = self._check_completeness(request.claim_type, request.documents, inconsistencies)
            warnings = self._generate_warnings(request)
            error_inconsistencies = [inc for inc in inconsistencies if inc.severity == "error"]
            is_complete = len(missing_items) == 0 and len(error_inconsistencies) == 0

            # LLM reasoning layer (contextual explanation only)
            ai_used = False
            ai_provider = None
            ai_model = None
            reasoning_summary = None
            fallback_used = False

            if self._gemini and self._gemini.is_available:
                try:
                    inc_summary = "; ".join(f"{i.field}: {i.description}" for i in inconsistencies) if inconsistencies else "None"
                    prompt = (
                        f"Claim Type: {request.claim_type}\n"
                        f"Claimed Amount: LKR {request.claimed_amount:,.2f}\n"
                        f"Incident Date: {request.incident_date}\n"
                        f"Submitted Documents: {', '.join(d.document_type for d in request.documents) if request.documents else 'None'}\n"
                        f"Checklist Status: {'Complete' if is_complete else 'Incomplete'}\n"
                        f"Missing Required Documents: {', '.join(missing_items) if missing_items else 'None'}\n"
                        f"Detected Inconsistencies: {inc_summary}\n\n"
                        "Provide a concise, 2-3 sentence reviewer summary explaining whether the documentation is adequate, "
                        "what critical evidence is missing or mismatched, and why it is required for this claim type. "
                        "Do not approve or deny the claim."
                    )
                    system_instruction = (
                        "You are an insurance document verification reasoning assistant. "
                        "Your job is to provide clear, neutral contextual explanations for claims adjusters. "
                        "Never make financial decisions or claim approvals."
                    )

                    explanation = self._gemini.generate_text(
                        prompt=prompt,
                        system_instruction=system_instruction,
                        operation_name="document_verification",
                    )

                    if explanation:
                        ai_used = True
                        ai_provider = "gemini"
                        model = getattr(self._gemini, "model_name", "gemini-2.5-flash")
                        ai_model = model if isinstance(model, str) else "gemini-2.5-flash"
                        reasoning_summary = explanation
                    else:
                        fallback_used = True
                        logger.info(
                            "Document verification: Gemini explanation unavailable. "
                            "Using authoritative deterministic checklist result (complete=%s, missing=%d).",
                            is_complete, len(missing_items)
                        )
                except Exception as exc:
                    fallback_used = True
                    logger.warning(
                        "Document verification: Gemini call threw unexpected %s. "
                        "Using authoritative deterministic checklist result (complete=%s).",
                        type(exc).__name__, is_complete
                    )
            else:
                fallback_used = True

            return DocumentVerificationResult(
                complete=is_complete,
                missing_items=missing_items,
                inconsistencies=inconsistencies,
                warnings=warnings,
                ai_used=ai_used,
                ai_provider=ai_provider,
                ai_model=ai_model,
                reasoning_summary=reasoning_summary,
                fallback_used=fallback_used,
            )
        except Exception as e:
            # Safe failure — return structured error, never crash
            return DocumentVerificationResult(
                complete=False,
                missing_items=[],
                inconsistencies=[],
                warnings=[f"Verification failed safely: {str(e)}"],
                ai_used=False,
                fallback_used=True,
                reasoning_summary="Verification failed safely due to internal error.",
            )

    @staticmethod
    def _build_deterministic_summary(is_complete: bool, missing_items: list[str], inconsistencies: list[DocumentInconsistency]) -> str:
        """Deterministic explanation summary when Gemini is unavailable."""
        if is_complete:
            return "Deterministic document verification completed: All required documents are present and consistent with claimed types."
        parts = []
        if missing_items:
            parts.append(f"Missing required documents: {', '.join(missing_items)}")
        errors = [inc.description for inc in inconsistencies if inc.severity == "error"]
        if errors:
            parts.append(f"Document integrity issues: {'; '.join(errors)}")
        return f"Deterministic document verification completed (action required): {'; '.join(parts)}."

    @staticmethod
    def _normalize_doc_type(doc_type: str) -> str:
        """
        Normalizes document type aliases.
        'Beneficiary ID' satisfies 'Beneficiary / Nominee Identification'.
        """
        trimmed = doc_type.strip()
        lower = trimmed.lower()
        if lower in ("beneficiary id", "beneficiary / nominee identification"):
            return "Beneficiary / Nominee Identification"
        return trimmed

    def _check_completeness(
        self,
        claim_type: str,
        documents: list[DocumentData],
        inconsistencies: list[DocumentInconsistency] | None = None,
    ) -> list[str]:
        """Check which required documents are missing."""
        required = self.REQUIRED_DOCUMENTS.get(claim_type, ["Supporting Document"])

        # Determine which document types have format compatibility errors
        incompatible_types = set()
        if inconsistencies:
            for inc in inconsistencies:
                if inc.severity == "error" and any(k in inc.description.lower() for k in ("format", "extension", "rejected", "header signature")):
                    if inc.field.startswith("document:"):
                        incompatible_types.add(self._normalize_doc_type(inc.field[len("document:"):].strip()).lower())
                    elif not inc.field.startswith("incident_date") and not inc.field.startswith("claimed_amount") and not inc.field.startswith("documents:"):
                        incompatible_types.add(self._normalize_doc_type(inc.field).lower())

        submitted_normalized = set()
        for doc in documents:
            if not doc.document_type:
                continue
            norm_type = self._normalize_doc_type(doc.document_type)
            norm_type_lower = norm_type.lower()

            # Skip rejected, mismatch, or unreadable status
            if doc.verification_status and doc.verification_status.strip().lower() in ("rejected", "mismatch", "unreadable"):
                continue

            # Skip if flagged with format incompatibility
            if norm_type_lower in incompatible_types:
                continue

            # Validate extension / format compatibility
            if not is_format_compatible_with_extension(doc.file_name, norm_type):
                continue

            # Check magic bytes if file is on disk
            fp = find_file_on_disk(doc.file_url, doc.file_name)
            if fp and os.path.isfile(fp):
                try:
                    with open(fp, "rb") as bf:
                        sample = bf.read(32)
                    sig = detect_file_signature(sample)
                    if not check_format_compatibility(sig, norm_type)[0]:
                        continue
                except Exception:
                    pass

            submitted_normalized.add(norm_type_lower)

        missing = []
        for req_doc in required:
            norm_req = self._normalize_doc_type(req_doc).lower()
            if norm_req not in submitted_normalized:
                missing.append(req_doc)

        return missing

    def _check_inconsistencies(self, request: DocumentVerificationRequest) -> list[DocumentInconsistency]:
        """Check for obvious data inconsistencies in documents."""
        inconsistencies: list[DocumentInconsistency] = []

        # Parse incident date
        try:
            incident_dt = datetime.strptime(request.incident_date, "%Y-%m-%d").date()
        except (ValueError, TypeError):
            inconsistencies.append(
                DocumentInconsistency(
                    field="incident_date",
                    description=f"Invalid incident date format: {request.incident_date}",
                    severity="error",
                )
            )
            return inconsistencies

        today = date.today()

        # Check if incident date is in the future
        if incident_dt > today:
            inconsistencies.append(
                DocumentInconsistency(
                    field="incident_date",
                    description="Incident date is in the future",
                    severity="error",
                )
            )

        # Check document upload dates vs incident date
        for doc in request.documents:
            if doc.uploaded_at:
                try:
                    upload_dt = datetime.strptime(doc.uploaded_at, "%Y-%m-%d").date()

                    # Document uploaded significantly before incident is suspicious
                    if upload_dt < incident_dt:
                        days_before = (incident_dt - upload_dt).days
                        if days_before > 30:
                            inconsistencies.append(
                                DocumentInconsistency(
                                    field=f"document:{doc.document_type}",
                                    description=(
                                        f"Document '{doc.document_type}' was uploaded "
                                        f"{days_before} days before the incident date"
                                    ),
                                    severity="warning",
                                )
                            )
                except (ValueError, TypeError):
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Invalid upload date for '{doc.document_type}': {doc.uploaded_at}",
                            severity="warning",
                        )
                    )

        # Check claimed amount reasonableness
        if request.claimed_amount <= 0:
            inconsistencies.append(
                DocumentInconsistency(
                    field="claimed_amount",
                    description="Claimed amount must be positive",
                    severity="error",
                )
            )
        elif request.claimed_amount > 1_000_000:
            inconsistencies.append(
                DocumentInconsistency(
                    field="claimed_amount",
                    description=f"Very high claimed amount: LKR {request.claimed_amount:,.2f}",
                    severity="warning",
                )
            )

        # Check document integrity, duplicate reuse, readability, and content mismatch
        doc_hashes: dict[str, list[str]] = {}  # hash -> list of document_types

        for doc in request.documents:
            norm_type = self._normalize_doc_type(doc.document_type)

            # Check if document was already marked Rejected or Mismatch by backend
            if doc.verification_status and doc.verification_status.strip().lower() in ("rejected", "mismatch"):
                inconsistencies.append(
                    DocumentInconsistency(
                        field=f"document:{doc.document_type}",
                        description=f"Document '{doc.file_name}' uploaded under '{norm_type}' is rejected or incompatible.",
                        severity="error",
                    )
                )
                continue

            # Check 0 byte size passed directly on doc model
            if doc.file_size == 0:
                inconsistencies.append(
                    DocumentInconsistency(
                        field=f"document:{doc.document_type}",
                        description=f"Unreadable required document: '{doc.file_name}' for '{doc.document_type}' is empty (0 bytes).",
                        severity="error",
                    )
                )
                continue

            extracted_text = doc.extracted_text
            file_path = None
            file_bytes = None

            # Resolve file from disk if not already given extracted text
            if not extracted_text:
                file_path = find_file_on_disk(doc.file_url, doc.file_name)
                if file_path and os.path.isfile(file_path):
                    try:
                        file_size = os.path.getsize(file_path)
                        if file_size == 0:
                            inconsistencies.append(
                                DocumentInconsistency(
                                    field=f"document:{doc.document_type}",
                                    description=f"Unreadable required document: '{doc.file_name}' for '{doc.document_type}' is empty (0 bytes).",
                                    severity="error",
                                )
                            )
                            continue
                        with open(file_path, "rb") as bf:
                            file_bytes = bf.read(25 * 1024 * 1024)
                    except Exception as fe:
                        logger.warning("Error reading file %s: %s", file_path, fe)

            # Format compatibility check
            if file_bytes:
                sig = detect_file_signature(file_bytes)
                ext = os.path.splitext(doc.file_name.lower())[1] if doc.file_name else ""
                if ext == ".pdf" and sig != "pdf":
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"File '{doc.file_name}' has .pdf extension but lacks a valid PDF header signature.",
                            severity="error",
                        )
                    )
                    continue

                is_compat, compat_reason = check_format_compatibility(sig, norm_type)
                if not is_compat:
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Document type mismatch: {compat_reason}",
                            severity="error",
                        )
                    )
                    continue
            else:
                # File bytes not on disk — evaluate extension compatibility
                if not is_format_compatible_with_extension(doc.file_name, norm_type):
                    allowed = ALLOWED_EXTENSIONS.get(norm_type)
                    accepted = ", ".join(sorted(allowed)) if allowed else "valid formats"
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Document type mismatch: File '{doc.file_name}' is not valid for '{norm_type}'. Accepted formats: {accepted}.",
                            severity="error",
                        )
                    )
                    continue

            # Compute or record hash for duplicate reuse detection
            file_hash = doc.file_hash
            if not file_hash and file_bytes:
                file_hash = compute_sha256(file_bytes)

            if file_hash:
                if file_hash not in doc_hashes:
                    doc_hashes[file_hash] = []
                doc_hashes[file_hash].append(norm_type)

            # Extract text from PDF if not provided
            is_pdf = (doc.file_name and doc.file_name.lower().endswith(".pdf")) or (doc.content_type and "pdf" in doc.content_type.lower())
            if not extracted_text and file_path and is_pdf:
                text_res, err = extract_pdf_text_safe(file_path)
                if err and "empty" in err.lower():
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Unreadable required document: '{doc.file_name}' for '{doc.document_type}' is empty or unreadable.",
                            severity="error",
                        )
                    )
                    continue
                elif text_res:
                    extracted_text = text_res

            # Scanned / non-text PDF safety: If it's a PDF and has no or insufficient text (< 30 chars),
            # mark it unreadable (cannot be deterministically verified without OCR).
            # Only evaluate this if physical file bytes / file on disk were inspected, or extracted_text was explicitly provided.
            if is_pdf and (file_path or file_bytes or doc.extracted_text is not None):
                if not extracted_text or len(extracted_text.strip()) < 30:
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Unreadable required document: '{doc.file_name}' for '{doc.document_type}' contains insufficient or unreadable text (scanned or image-only PDF requires review).",
                            severity="error",
                        )
                    )
                    continue

            # Check content consistency / mismatch
            if extracted_text and len(extracted_text.strip()) >= 30:
                is_mismatch, detected_other, expl = evaluate_content_consistency(
                    extracted_text, norm_type, doc.file_name
                )
                if is_mismatch:
                    inconsistencies.append(
                        DocumentInconsistency(
                            field=f"document:{doc.document_type}",
                            description=f"Document type mismatch: {expl}",
                            severity="error",
                        )
                    )

        # Flag duplicate file hash reuse across distinct document types
        for fhash, dtypes in doc_hashes.items():
            unique_types = list(dict.fromkeys(dtypes))
            if len(unique_types) > 1:
                inconsistencies.append(
                    DocumentInconsistency(
                        field="documents:duplicate_reuse",
                        description=f"Duplicate file reuse: identical physical file content was uploaded across multiple distinct document types ({', '.join(unique_types)}).",
                        severity="error",
                    )
                )

        return inconsistencies

    def _generate_warnings(self, request: DocumentVerificationRequest) -> list[str]:
        """Generate general warnings about the claim."""
        warnings: list[str] = []

        if not request.documents:
            warnings.append("No documents have been submitted with this claim.")

        if len(request.documents) > 20:
            warnings.append(f"Unusually high number of documents ({len(request.documents)}).")

        # Check for duplicate document types
        doc_types = [doc.document_type.lower() for doc in request.documents]
        duplicates = {t for t in doc_types if doc_types.count(t) > 1}
        if duplicates:
            warnings.append(f"Duplicate document types detected: {', '.join(duplicates)}")

        return warnings
