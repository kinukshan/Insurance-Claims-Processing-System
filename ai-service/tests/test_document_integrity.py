"""
Comprehensive tests for document integrity, content consistency, duplicate reuse,
and fraud-risk integration in the AI service.
"""

from datetime import date, datetime, timedelta
import uuid
import pytest
from agents.document_verification_agent import DocumentVerificationAgent
from agents.fraud_risk_agent import FraudRiskAgent
from schemas.claim_schema import DocumentVerificationRequest, DocumentData, ClaimData
from schemas.risk_result_schema import RiskAssessmentResult
from unittest.mock import MagicMock


def _make_doc_request(documents: list[DocumentData]) -> DocumentVerificationRequest:
    return DocumentVerificationRequest(
        claim_id="test-motor-001",
        claim_type="Motor",
        incident_date=(date.today() - timedelta(days=3)).isoformat(),
        claimed_amount=4500.0,
        documents=documents,
    )


class TestDocumentIntegrityAndContentConsistency:
    """Test deterministic document content verification and mismatch detection."""

    def test_exact_regression_beneficiary_pdf_uploaded_as_police_report(self):
        """
        Exact regression test:
        Motor claim requires: Police Report, Photos of Damage, Repair Estimate, Driver License.
        All 4 are uploaded, but Police Report contains beneficiary/nominee identification content.
        Expected:
        - presence complete (missing_items == [])
        - verification complete is False
        - Document type mismatch inconsistency on Police Report
        """
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Police Report",
                file_name="dummy_beneficiary_nominee_identification.pdf",
                extracted_text="Beneficiary / Nominee Identification Form. Relationship to insured: Spouse. NIC: 912345678V. Full Name of Nominee.",
                file_size=10240,
                file_hash="hash_beneficiary_001",
            ),
            DocumentData(
                document_type="Photos of Damage",
                file_name="damage_front.jpg",
                file_size=20480,
                file_hash="hash_photo_001",
            ),
            DocumentData(
                document_type="Repair Estimate",
                file_name="estimate.pdf",
                extracted_text="Auto Body Workshop. Vehicle Repair Estimate. Parts and labour subtotal. Total quotation: $4,500.",
                file_size=15360,
                file_hash="hash_estimate_001",
            ),
            DocumentData(
                document_type="Driver License",
                file_name="license.pdf",
                extracted_text="Driving Licence. Driver License Number: B1234567. Date of birth: 1985-04-12. Class of vehicle: Motor Car.",
                file_size=8192,
                file_hash="hash_license_001",
            ),
        ]

        result = agent.verify(_make_doc_request(docs))

        # Presence check: all 4 required document types are uploaded
        assert len(result.missing_items) == 0

        # Verification check: MUST NOT be complete
        assert result.complete is False

        # Inconsistencies must flag the Police Report mismatch
        mismatch_items = [
            inc for inc in result.inconsistencies
            if "Police Report" in inc.field and "Beneficiary" in inc.description
        ]
        assert len(mismatch_items) >= 1
        assert mismatch_items[0].severity == "error"

    def test_multiple_mismatched_documents(self):
        """
        Test multiple mismatched files uploaded for required Motor documents.
        Expected:
        - complete is False
        - multiple inconsistency errors
        """
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Police Report",
                file_name="doc1.pdf",
                extracted_text="Beneficiary / Nominee Identification nominee details and relationship to insured.",
                file_size=5000,
                file_hash="h1",
            ),
            DocumentData(
                document_type="Photos of Damage",
                file_name="doc2.jpg",
                file_size=5000,
                file_hash="h2",
            ),
            DocumentData(
                document_type="Repair Estimate",
                file_name="doc3.pdf",
                extracted_text="Airline Flight Itinerary and Boarding Pass. E-ticket booking reference and travel details.",
                file_size=5000,
                file_hash="h3",
            ),
            DocumentData(
                document_type="Driver License",
                file_name="doc4.jpg",
                file_size=5000,
                file_hash="h4",
            ),
        ]

        result = agent.verify(_make_doc_request(docs))
        assert result.complete is False
        assert len(result.inconsistencies) >= 2

    def test_valid_consistent_motor_documents_pass(self):
        """
        Valid Motor claim documents with consistent content must complete successfully.
        """
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Police Report",
                file_name="police_report.pdf",
                extracted_text="Police Station Traffic Division. Incident Report regarding motor vehicle collision. Police Officer FIR GD Entry.",
                file_size=10000,
                file_hash="h_police",
            ),
            DocumentData(
                document_type="Photos of Damage",
                file_name="front_bumper.jpg",
                file_size=25000,
                file_hash="h_photo",
            ),
            DocumentData(
                document_type="Repair Estimate",
                file_name="garage_estimate.pdf",
                extracted_text="Authorized Garage Repair Estimate. Replacement parts, labor hours, total estimated cost.",
                file_size=12000,
                file_hash="h_est",
            ),
            DocumentData(
                document_type="Driver License",
                file_name="dl.pdf",
                extracted_text="National Driving Licence. Driver License Class of Vehicle Motor Car. Date of birth and expiry.",
                file_size=8000,
                file_hash="h_dl",
            ),
        ]

        result = agent.verify(_make_doc_request(docs))
        assert result.complete is True
        assert len(result.missing_items) == 0
        assert len(result.inconsistencies) == 0

    def test_duplicate_file_hash_reuse_across_distinct_types(self):
        """
        Uploading identical file bytes (same SHA256) across multiple required document types
        must be flagged deterministically.
        """
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        shared_hash = "identical_sha256_hash_12345"
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", file_hash=shared_hash, file_size=5000),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", file_hash=shared_hash, file_size=5000),
            DocumentData(document_type="Photos of Damage", file_name="photo.jpg", file_hash="diff_hash", file_size=5000),
            DocumentData(document_type="Driver License", file_name="license.jpg", file_hash="diff_hash2", file_size=5000),
        ]

        result = agent.verify(_make_doc_request(docs))
        assert result.complete is False
        reuse_flags = [inc for inc in result.inconsistencies if "Duplicate file reuse" in inc.description]
        assert len(reuse_flags) >= 1

    def test_unreadable_or_zero_byte_document_fails(self):
        """Zero-byte or unreadable file must be flagged as an error."""
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", file_size=0),
            DocumentData(document_type="Photos of Damage", file_name="photo.jpg", file_size=5000),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", file_size=5000),
            DocumentData(document_type="Driver License", file_name="license.jpg", file_size=5000),
        ]

        result = agent.verify(_make_doc_request(docs))
        assert result.complete is False
        assert any("empty" in inc.description.lower() for inc in result.inconsistencies)


class TestFraudRiskAgentDocumentIntegration:
    """Test that FraudRiskAgent deterministically ingests document signals."""

    @pytest.mark.asyncio
    async def test_risk_assessment_with_single_document_mismatch(self):
        """
        One document type mismatch should:
        - increase risk score by 40.0
        - add DocumentTypeMismatch fraud flag
        - escalate recommendation
        """
        agent = FraudRiskAgent(gemini_client_instance=None)
        claim = ClaimData(
            claim_id=uuid.uuid4(),
            policy_holder_id=uuid.uuid4(),
            claim_amount=5000.0,
            description="Vehicle collision",
            incident_date=datetime.now(),
            incident_location="Colombo",
            claim_type="Motor",
            document_flags=["DocumentTypeMismatch: Police Report content resembles Beneficiary Identification"],
        )

        result = await agent.assess(claim)
        assert result.risk_score >= 40.0
        assert result.recommendation == "escalate"
        flag_types = [f.flag_type for f in result.flags]
        assert "DocumentTypeMismatch" in flag_types

    @pytest.mark.asyncio
    async def test_risk_assessment_with_multiple_document_mismatches_produces_high_risk(self):
        """
        Multiple document mismatches should produce score >= 80 (High/Critical) and escalate.
        """
        agent = FraudRiskAgent(gemini_client_instance=None)
        claim = ClaimData(
            claim_id=uuid.uuid4(),
            policy_holder_id=uuid.uuid4(),
            claim_amount=5000.0,
            description="Vehicle collision",
            incident_date=datetime.now(),
            incident_location="Colombo",
            claim_type="Motor",
            document_flags=[
                "DocumentTypeMismatch: Police Report mismatch",
                "DocumentTypeMismatch: Repair Estimate mismatch",
            ],
        )

        result = await agent.assess(claim)
        assert result.risk_score >= 80.0
        assert result.recommendation == "escalate"
        assert len(result.flags) >= 2

    @pytest.mark.asyncio
    async def test_risk_assessment_clean_documents_produce_zero_document_risk(self):
        """
        Clean documents with no flags produce 0 document risk.
        """
        agent = FraudRiskAgent(gemini_client_instance=None)
        claim = ClaimData(
            claim_id=uuid.uuid4(),
            policy_holder_id=uuid.uuid4(),
            claim_amount=5000.0,
            description="Normal claim",
            incident_date=datetime.now(),
            incident_location="Colombo",
            claim_type="Motor",
            document_flags=[],
        )

        result = await agent.assess(claim)
        assert result.risk_score == 0.0
        assert result.recommendation == "proceed"
        assert len(result.flags) == 0

    @pytest.mark.asyncio
    async def test_gemini_fallback_preserves_deterministic_flags_and_score(self):
        """
        When Gemini is unavailable, deterministic document flags and scores MUST remain intact.
        """
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("Gemini service down")

        agent = FraudRiskAgent(gemini_client_instance=mock_gemini)
        claim = ClaimData(
            claim_id=uuid.uuid4(),
            policy_holder_id=uuid.uuid4(),
            claim_amount=5000.0,
            description="Claim with bad docs",
            incident_date=datetime.now(),
            incident_location="Colombo",
            claim_type="Motor",
            document_flags=["DocumentTypeMismatch: Fake police report"],
        )

        result = await agent.assess(claim)
        assert result.fallback_used is True
        assert result.risk_score >= 40.0
        assert result.recommendation == "escalate"
        assert any(f.flag_type == "DocumentTypeMismatch" for f in result.flags)
