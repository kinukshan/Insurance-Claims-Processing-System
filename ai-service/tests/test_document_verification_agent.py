"""Tests for the Document Verification Agent.

Covers:
- Complete evidence set → complete: true
- Missing evidence → returns missing items
- Inconsistent dates (future incident, uploads before incident)
- Malformed input (invalid dates, negative amounts)
- Schema validation (Pydantic)
- Safe failure on exceptions
- High claimed amount warnings
- No documents warning
- Duplicate document type detection
"""

from __future__ import annotations

import pytest
from datetime import date, timedelta
from pydantic import ValidationError
from agents.document_verification_agent import DocumentVerificationAgent
from schemas.claim_schema import DocumentVerificationRequest, DocumentData
from schemas.document_result_schema import DocumentVerificationResult


@pytest.fixture
def agent():
    return DocumentVerificationAgent()


def _make_request(
    claim_type: str = "Auto",
    incident_date: str | None = None,
    claimed_amount: float = 5000.0,
    documents: list[DocumentData] | None = None,
) -> DocumentVerificationRequest:
    """Helper to build a verification request."""
    if incident_date is None:
        incident_date = (date.today() - timedelta(days=7)).isoformat()
    if documents is None:
        documents = []
    return DocumentVerificationRequest(
        claim_id="test-claim-001",
        claim_type=claim_type,
        incident_date=incident_date,
        claimed_amount=claimed_amount,
        documents=documents,
    )


def _make_auto_docs() -> list[DocumentData]:
    """Create a complete set of Auto claim documents."""
    today = date.today().isoformat()
    return [
        DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at=today),
        DocumentData(document_type="Photos of Damage", file_name="photos.jpg", uploaded_at=today),
        DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at=today),
        DocumentData(document_type="Driver License", file_name="license.jpg", uploaded_at=today),
    ]


# ═════════════════════════════════════════════
# Document Completeness Tests
# ═════════════════════════════════════════════


class TestDocumentCompleteness:
    """Tests for document checklist completeness checks."""

    def test_complete_auto_docs_returns_complete(self, agent):
        request = _make_request(claim_type="Auto", documents=_make_auto_docs())
        result = agent.verify(request)
        assert result.complete is True
        assert len(result.missing_items) == 0

    def test_missing_docs_returns_missing_items(self, agent):
        docs = [DocumentData(document_type="Police Report", file_name="police.pdf")]
        request = _make_request(claim_type="Auto", documents=docs)
        result = agent.verify(request)
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items
        assert "Repair Estimate" in result.missing_items
        assert "Driver License" in result.missing_items
        assert "Police Report" not in result.missing_items

    def test_health_claim_requires_medical_docs(self, agent):
        request = _make_request(claim_type="Health", documents=[])
        result = agent.verify(request)
        assert "Medical Report" in result.missing_items
        assert "Hospital Bills" in result.missing_items

    def test_unknown_claim_type_requires_supporting_doc(self, agent):
        request = _make_request(claim_type="Unknown", documents=[])
        result = agent.verify(request)
        assert "Supporting Document" in result.missing_items

    def test_case_insensitive_document_matching(self, agent):
        docs = [
            DocumentData(document_type="police report", file_name="p.pdf"),
            DocumentData(document_type="PHOTOS OF DAMAGE", file_name="ph.jpg"),
            DocumentData(document_type="Repair Estimate", file_name="re.pdf"),
            DocumentData(document_type="driver license", file_name="dl.jpg"),
        ]
        request = _make_request(claim_type="Auto", documents=docs)
        result = agent.verify(request)
        assert len(result.missing_items) == 0


# ═════════════════════════════════════════════
# Date Consistency Tests
# ═════════════════════════════════════════════


class TestDateConsistency:
    """Tests for date-related inconsistency detection."""

    def test_future_incident_date_is_error(self, agent):
        future_date = (date.today() + timedelta(days=30)).isoformat()
        request = _make_request(incident_date=future_date, documents=_make_auto_docs())
        result = agent.verify(request)
        assert result.complete is False
        error_fields = [i.field for i in result.inconsistencies]
        assert "incident_date" in error_fields

    def test_document_uploaded_long_before_incident_is_flagged(self, agent):
        incident = (date.today() - timedelta(days=7)).isoformat()
        old_upload = (date.today() - timedelta(days=100)).isoformat()
        docs = [
            DocumentData(
                document_type="Police Report",
                file_name="old.pdf",
                uploaded_at=old_upload,
            ),
            DocumentData(document_type="Photos of Damage", file_name="ph.jpg"),
            DocumentData(document_type="Repair Estimate", file_name="re.pdf"),
            DocumentData(document_type="Driver License", file_name="dl.jpg"),
        ]
        request = _make_request(claim_type="Auto", incident_date=incident, documents=docs)
        result = agent.verify(request)
        suspect_fields = [i.field for i in result.inconsistencies]
        assert any("Police Report" in f for f in suspect_fields)


# ═════════════════════════════════════════════
# Malformed Input Tests
# ═════════════════════════════════════════════


class TestMalformedInput:
    """Tests for malformed or edge-case inputs."""

    def test_invalid_incident_date_format(self, agent):
        request = _make_request(incident_date="not-a-date", documents=_make_auto_docs())
        result = agent.verify(request)
        assert result.complete is False
        error_fields = [i.field for i in result.inconsistencies]
        assert "incident_date" in error_fields
    def test_negative_claimed_amount(self, agent):
        with pytest.raises(ValidationError):
            _make_request(
            claimed_amount=-500,
            documents=_make_auto_docs(),
        )

    def test_very_high_claimed_amount_warning(self, agent):
        request = _make_request(claimed_amount=5_000_000, documents=_make_auto_docs())
        result = agent.verify(request)
        amount_issues = [
            i for i in result.inconsistencies if i.field == "claimed_amount"
        ]
        assert len(amount_issues) > 0
        assert amount_issues[0].severity == "warning"

    def test_invalid_upload_date_in_document(self, agent):
        docs = [
            DocumentData(
                document_type="Police Report",
                file_name="p.pdf",
                uploaded_at="invalid-date",
            ),
            DocumentData(document_type="Photos of Damage", file_name="ph.jpg"),
            DocumentData(document_type="Repair Estimate", file_name="re.pdf"),
            DocumentData(document_type="Driver License", file_name="dl.jpg"),
        ]
        request = _make_request(claim_type="Auto", documents=docs)
        result = agent.verify(request)
        # Should produce a warning about invalid date, not crash
        inconsistency_fields = [i.field for i in result.inconsistencies]
        assert any("Police Report" in f for f in inconsistency_fields)


# ═════════════════════════════════════════════
# Warning Generation Tests
# ═════════════════════════════════════════════


class TestWarnings:
    """Tests for general warning generation."""

    def test_no_documents_warning(self, agent):
        request = _make_request(documents=[])
        result = agent.verify(request)
        assert any("No documents" in w for w in result.warnings)

    def test_excessive_documents_warning(self, agent):
        docs = [
            DocumentData(document_type=f"Doc {i}", file_name=f"doc{i}.pdf")
            for i in range(25)
        ]
        request = _make_request(documents=docs)
        result = agent.verify(request)
        assert any("high number" in w.lower() or "unusual" in w.lower() for w in result.warnings)

    def test_duplicate_document_types_warning(self, agent):
        docs = [
            DocumentData(document_type="Police Report", file_name="p1.pdf"),
            DocumentData(document_type="Police Report", file_name="p2.pdf"),
            DocumentData(document_type="Photos of Damage", file_name="ph.jpg"),
            DocumentData(document_type="Repair Estimate", file_name="re.pdf"),
            DocumentData(document_type="Driver License", file_name="dl.jpg"),
        ]
        request = _make_request(claim_type="Auto", documents=docs)
        result = agent.verify(request)
        assert any("duplicate" in w.lower() for w in result.warnings)


# ═════════════════════════════════════════════
# Schema Validation Tests
# ═════════════════════════════════════════════


class TestSchemaValidation:
    """Tests for Pydantic schema validation."""

    def test_valid_request_schema(self):
        request = _make_request()
        assert isinstance(request, DocumentVerificationRequest)
        assert request.claim_id == "test-claim-001"

    def test_valid_result_schema(self, agent):
        request = _make_request(documents=_make_auto_docs())
        result = agent.verify(request)
        assert isinstance(result, DocumentVerificationResult)
        assert isinstance(result.complete, bool)
        assert isinstance(result.missing_items, list)
        assert isinstance(result.inconsistencies, list)
        assert isinstance(result.warnings, list)

    def test_document_data_schema(self):
        doc = DocumentData(
            document_type="Police Report",
            file_name="report.pdf",
            uploaded_at="2024-01-15",
            verification_status="Pending",
        )
        assert doc.document_type == "Police Report"


# ═════════════════════════════════════════════
# Safe Failure Tests
# ═════════════════════════════════════════════


class TestSafeFailure:
    """Tests that the agent fails safely on unexpected errors."""

    def test_safe_failure_returns_structured_output(self):
        """Simulate an error by subclassing and verify safe failure."""

        class BrokenAgent(DocumentVerificationAgent):
            def _check_completeness(self, claim_type, documents):
                raise RuntimeError("Simulated failure")

        broken = BrokenAgent()
        request = _make_request(documents=_make_auto_docs())
        result = broken.verify(request)

        # Should NOT raise — should return a structured error
        assert result.complete is False
        assert any("failed safely" in w.lower() for w in result.warnings)


# ═════════════════════════════════════════════
# Gemini Hybrid & Invariance Tests
# ═════════════════════════════════════════════


class TestGeminiHybridDocumentVerification:
    """Tests for hybrid Gemini reasoning integration and rule invariance."""

    def test_document_verification_with_gemini_success(self):
        """When Gemini returns an explanation, ai_used is True and deterministic fields are preserved."""
        from unittest.mock import MagicMock

        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.model_name = "gemini-2.5-flash"
        mock_gemini.generate_text.return_value = "All 4 required auto documents are present and consistent."

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        request = _make_request(documents=_make_auto_docs())
        result = agent.verify(request)

        # Deterministic checks preserved
        assert result.complete is True
        assert len(result.missing_items) == 0
        # AI metadata populated
        assert result.ai_used is True
        assert result.ai_provider == "gemini"
        assert result.ai_model == "gemini-2.5-flash"
        assert result.reasoning_summary == "All 4 required auto documents are present and consistent."
        assert result.fallback_used is False

    def test_gemini_cannot_override_missing_documents(self):
        """Even if Gemini claims documents look great, missing items must keep complete=False."""
        from unittest.mock import MagicMock

        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.model_name = "gemini-2.5-flash"
        mock_gemini.generate_text.return_value = "The claimant provided damage photos."

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        # Missing Police Report, Repair Estimate, Driver License
        request = _make_request(documents=[DocumentData(document_type="Photos of Damage", file_name="p.jpg")])
        result = agent.verify(request)

        # Invariance: complete MUST be False
        assert result.complete is False
        assert "Police Report" in result.missing_items
        assert result.ai_used is True

    def test_gemini_failure_falls_back_deterministically(self):
        """When Gemini throws an exception, the agent falls back with fallback_used=True."""
        from unittest.mock import MagicMock

        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("API quota exceeded")

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        request = _make_request(documents=_make_auto_docs())
        result = agent.verify(request)

        # Deterministic checks still pass
        assert result.complete is True
        assert result.ai_used is False
        assert result.fallback_used is True
        assert result.reasoning_summary is None
