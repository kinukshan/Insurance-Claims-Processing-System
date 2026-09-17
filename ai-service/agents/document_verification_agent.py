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

from datetime import date, datetime
from schemas.claim_schema import DocumentVerificationRequest, DocumentData
from schemas.document_result_schema import DocumentVerificationResult, DocumentInconsistency


class DocumentVerificationAgent:
    """
    Agentic AI agent that verifies claim documents against
    required checklists and checks for data inconsistencies.
    """

    # Required documents per claim type
    REQUIRED_DOCUMENTS: dict[str, list[str]] = {
        "Auto": ["Police Report", "Photos of Damage", "Repair Estimate", "Driver License"],
        "Home": ["Photos of Damage", "Repair Estimate", "Property Deed"],
        "Health": ["Medical Report", "Hospital Bills", "Prescription", "Doctor Referral"],
        "Life": ["Death Certificate", "Beneficiary ID", "Policy Document"],
        "Travel": ["Travel Itinerary", "Receipts", "Incident Report"],
        "Property": ["Photos of Damage", "Repair Estimate", "Property Valuation"],
        "Liability": ["Incident Report", "Third Party Claim", "Legal Notice"],
        "Other": ["Supporting Document"],
    }

    def verify(self, request: DocumentVerificationRequest) -> DocumentVerificationResult:
        """
        Verify submitted documents against the required checklist
        and check for data inconsistencies.

        Args:
            request: The verification request containing claim data and documents.

        Returns:
            Structured DocumentVerificationResult.
        """
        try:
            missing_items = self._check_completeness(request.claim_type, request.documents)
            inconsistencies = self._check_inconsistencies(request)
            warnings = self._generate_warnings(request)

            return DocumentVerificationResult(
                complete=len(missing_items) == 0 and len(inconsistencies) == 0,
                missing_items=missing_items,
                inconsistencies=inconsistencies,
                warnings=warnings,
            )
        except Exception as e:
            # Safe failure — return structured error, never crash
            return DocumentVerificationResult(
                complete=False,
                missing_items=[],
                inconsistencies=[],
                warnings=[f"Verification failed safely: {str(e)}"],
            )

    def _check_completeness(self, claim_type: str, documents: list[DocumentData]) -> list[str]:
        """Check which required documents are missing."""
        required = self.REQUIRED_DOCUMENTS.get(claim_type, ["Supporting Document"])
        submitted_types = {doc.document_type.strip() for doc in documents}

        missing = []
        for req_doc in required:
            # Case-insensitive comparison
            if not any(req_doc.lower() == s.lower() for s in submitted_types):
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
                    description=f"Very high claimed amount: ${request.claimed_amount:,.2f}",
                    severity="warning",
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
