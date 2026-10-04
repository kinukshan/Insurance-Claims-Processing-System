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


class TestDocumentTypeIntegrityRegressions:
    """
    Comprehensive regression tests for Document Type Integrity Validation:
    - DOCX tagged as Photos of Damage -> rejected/flagged, incomplete
    - Valid JPEG/PNG tagged Photos of Damage -> accepted
    - DOCX renamed to .jpg -> rejected based on actual magic bytes
    - Police Report, Repair Estimate, Driver License format rules
    - Motor claim with valid docs + invalid DOCX pretending to be Photos of Damage -> complete = false
    - Motor claim with valid image -> complete = true
    - Gemini safety (success or failure must not override deterministic decisions)
    """

    DOCX_BYTES = b"PK\x03\x04\x14\x00\x06\x00\x08\x00"
    JPEG_BYTES = b"\xff\xd8\xff\xe0\x00\x10JFIF\x00\x01"
    PNG_BYTES = b"\x89PNG\r\n\x1a\n\x00\x00\x00\r"

    def test_docx_tagged_photos_of_damage_returns_incomplete_and_inconsistency(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Photos of Damage",
                file_name="Assignment__ ABD (1).docx",
            )
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-claim-001",
            claim_type="Motor",
            incident_date=date.today().isoformat(),
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items
        assert any("Photos of Damage" in inc.field for inc in result.inconsistencies)

    def test_valid_jpeg_tagged_photos_of_damage_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Photos of Damage",
                file_name="damage.jpg",
            )
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-claim-002",
            claim_type="Motor",
            incident_date=date.today().isoformat(),
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert "Photos of Damage" not in result.missing_items
        assert not any("Photos of Damage" in inc.field for inc in result.inconsistencies)

    def test_valid_png_tagged_photos_of_damage_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Photos of Damage",
                file_name="damage.png",
            )
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-claim-003",
            claim_type="Motor",
            incident_date=date.today().isoformat(),
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert "Photos of Damage" not in result.missing_items
        assert not any("Photos of Damage" in inc.field for inc in result.inconsistencies)

    def test_docx_renamed_to_jpg_rejected_by_magic_bytes(self, tmp_path):
        # Create a file named damage.jpg whose actual bytes are a zip/docx
        fake_jpg = tmp_path / "sneaky_damage.jpg"
        fake_jpg.write_bytes(self.DOCX_BYTES)

        agent = DocumentVerificationAgent(gemini_client_instance=None)
        docs = [
            DocumentData(
                document_type="Photos of Damage",
                file_name="sneaky_damage.jpg",
                file_url=str(fake_jpg),
            )
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-claim-004",
            claim_type="Motor",
            incident_date=date.today().isoformat(),
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items
        assert any("zip_archive" in inc.description or "not valid" in inc.description for inc in result.inconsistencies)

    def test_motor_claim_with_invalid_docx_damage_photo_is_incomplete(self):
        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at=today),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at=today),
            DocumentData(document_type="Driver License", file_name="license.pdf", uploaded_at=today),
            DocumentData(document_type="Photos of Damage", file_name="Assignment__ ABD (1).docx", uploaded_at=today),
        ]
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        request = DocumentVerificationRequest(
            claim_id="reg-claim-005",
            claim_type="Motor",
            incident_date=today,
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items
        assert any("Photos of Damage" in inc.field for inc in result.inconsistencies)

    def test_motor_claim_with_valid_damage_image_is_complete(self):
        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at=today),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at=today),
            DocumentData(document_type="Driver License", file_name="license.pdf", uploaded_at=today),
            DocumentData(document_type="Photos of Damage", file_name="damage.jpg", uploaded_at=today),
        ]
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        request = DocumentVerificationRequest(
            claim_id="reg-claim-006",
            claim_type="Motor",
            incident_date=today,
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is True
        assert len(result.missing_items) == 0

    def test_gemini_success_cannot_override_deterministic_invalid_result(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.return_value = "All documents look great, verification approved."

        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at=today),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at=today),
            DocumentData(document_type="Driver License", file_name="license.pdf", uploaded_at=today),
            DocumentData(document_type="Photos of Damage", file_name="Assignment__ ABD (1).docx", uploaded_at=today),
        ]
        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        request = DocumentVerificationRequest(
            claim_id="reg-claim-007",
            claim_type="Motor",
            incident_date=today,
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        # Even though Gemini returned glowing text, complete must remain False deterministically
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items

    def test_gemini_fallback_preserves_deterministic_incomplete_decision(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("Gemini unreachable")

        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at=today),
            DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at=today),
            DocumentData(document_type="Driver License", file_name="license.pdf", uploaded_at=today),
            DocumentData(document_type="Photos of Damage", file_name="Assignment__ ABD (1).docx", uploaded_at=today),
        ]
        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        request = DocumentVerificationRequest(
            claim_id="reg-claim-008",
            claim_type="Motor",
            incident_date=today,
            claimed_amount=5000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is False
        assert result.fallback_used is True
        assert "Photos of Damage" in result.missing_items


class TestHealthDocumentSemanticRegressions:
    """
    Regression tests for Health claim document semantic content validation:
    - Architecture lecture slides tagged Doctor Referral -> Mismatch
    - Valid dummy Doctor Referral -> Accepted
    - Doctor Referral wording variation -> Accepted
    - Medical Report tagged Doctor Referral -> Mismatch
    - Hospital Bill tagged Medical Report -> Mismatch
    - Prescription tagged Hospital Bills -> Mismatch
    - Valid Medical Report, Hospital Bill, Prescription -> Accepted
    - Scanned/non-text PDF -> Unreadable
    - Architecture PDF renamed doctor_referral.pdf -> Mismatch
    - Gemini cannot override deterministic mismatch
    - Gemini failure preserves deterministic mismatch
    - Health claim completeness checks
    """

    ARCHITECTURE_TEXT = (
        "Lecture 06: Software Architecture Patterns. Layered Architecture, Model-View-Controller (MVC), "
        "Event-Driven Architecture, Microservices, Domain-Driven Design, Repository Pattern, Dependency Injection. "
        "University Computer Science Department slides."
    )

    GENUINE_REFERRAL_TEXT = (
        "Doctor Referral Letter. Date: 22 Sep 2026. To: Orthopedic Department Nawaloka Hospital, Colombo. "
        "Patient: Saki Ki. Patient ID: TEST-PATIENT-001. Referral Reason: The patient was examined following "
        "an accident and reports persistent left arm and shoulder pain. Request: Kindly evaluate the patient "
        "for further management. Referring Doctor: Dr. Saman Perera MBBS MD."
    )

    REFERRAL_VARIATION_TEXT = (
        "Consultation Request & Referral. To: Cardiology Specialist Clinic, National Hospital. "
        "We refer this patient Mr. David Perera for second opinion and clinical review of chest symptoms. "
        "Referring Physician: Dr. Nimal Silva General Practitioner."
    )

    MEDICAL_REPORT_TEXT = (
        "General Hospital Colombo Medical Examination Report. Patient Name: Jane Doe. "
        "Attending Physician: Dr. Silva MD. Clinical Findings: Patient presents with acute fracture of the left tibia. "
        "Clinical diagnosis: Closed displaced fracture. Treatment plan: Surgical reduction and internal fixation."
    )

    HOSPITAL_BILL_TEXT = (
        "Asiri Central Hospital Invoice and Billing Statement. Bill No: INV-98241. "
        "Patient: John Doe. Bed charges, pharmacy charges, laboratory fees. Subtotal: LKR 45,000. "
        "Total Amount Due: LKR 52,500. Payment method: Cash/Card."
    )

    PRESCRIPTION_TEXT = (
        "Dr. Kamal Perera Clinic Medical Prescription Slip. Patient: Saki Ki. "
        "Rx: Amoxicillin 500mg capsules, 1 capsule 3 times daily for 7 days. "
        "Paracetamol 500mg tablets, 2 tablets every 6 hours PRN for pain. "
        "Doctor: Dr. Kamal Perera MBBS."
    )

    def test_health1_architecture_pdf_tagged_doctor_referral_is_mismatch(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="06 - Architecture Patterns.pdf",
            extracted_text=self.ARCHITECTURE_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-001",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Doctor Referral" in inc.field and "mismatch" in inc.description.lower() for inc in result.inconsistencies)

    def test_health2_valid_dummy_doctor_referral_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="dummy_doctor_referral.pdf",
            extracted_text=self.GENUINE_REFERRAL_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-002",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        referral_errs = [inc for inc in result.inconsistencies if "Doctor Referral" in inc.field and inc.severity == "error"]
        assert len(referral_errs) == 0

    def test_health3_doctor_referral_wording_variation_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="referral_letter.pdf",
            extracted_text=self.REFERRAL_VARIATION_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-003",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        referral_errs = [inc for inc in result.inconsistencies if "Doctor Referral" in inc.field and inc.severity == "error"]
        assert len(referral_errs) == 0

    def test_health4_medical_report_tagged_doctor_referral_is_mismatch(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="medical_report.pdf",
            extracted_text=self.MEDICAL_REPORT_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-004",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Doctor Referral" in inc.field for inc in result.inconsistencies)

    def test_health5_hospital_bill_tagged_medical_report_is_mismatch(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Medical Report",
            file_name="hospital_bill.pdf",
            extracted_text=self.HOSPITAL_BILL_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-005",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Medical Report" in inc.field for inc in result.inconsistencies)

    def test_health6_prescription_tagged_hospital_bills_is_mismatch(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Hospital Bills",
            file_name="prescription.pdf",
            extracted_text=self.PRESCRIPTION_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-006",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Hospital Bills" in inc.field for inc in result.inconsistencies)

    def test_health7_valid_medical_report_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Medical Report",
            file_name="medical_report.pdf",
            extracted_text=self.MEDICAL_REPORT_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-007",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        errs = [inc for inc in result.inconsistencies if "Medical Report" in inc.field and inc.severity == "error"]
        assert len(errs) == 0

    def test_health8_valid_hospital_bill_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Hospital Bills",
            file_name="hospital_bill.pdf",
            extracted_text=self.HOSPITAL_BILL_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-008",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        errs = [inc for inc in result.inconsistencies if "Hospital Bills" in inc.field and inc.severity == "error"]
        assert len(errs) == 0

    def test_health9_valid_prescription_accepted(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Prescription",
            file_name="prescription.pdf",
            extracted_text=self.PRESCRIPTION_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-009",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        errs = [inc for inc in result.inconsistencies if "Prescription" in inc.field and inc.severity == "error"]
        assert len(errs) == 0

    def test_health10_scanned_pdf_with_empty_text_is_unreadable(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="scanned_referral.pdf",
            extracted_text="",  # Scanned PDF returning empty text
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-010",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("unreadable" in inc.description.lower() for inc in result.inconsistencies)

    def test_health11_architecture_pdf_renamed_to_doctor_referral_still_mismatch(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="doctor_referral.pdf",
            extracted_text=self.ARCHITECTURE_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-011",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Doctor Referral" in inc.field for inc in result.inconsistencies)

    def test_health12_gemini_success_cannot_override_deterministic_mismatch(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.return_value = "Everything looks completely valid and complete."

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="06 - Architecture Patterns.pdf",
            extracted_text=self.ARCHITECTURE_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-012",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Doctor Referral" in inc.field for inc in result.inconsistencies)

    def test_health13_gemini_unavailable_preserves_deterministic_mismatch(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("503 Service Unavailable")

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        doc = DocumentData(
            document_type="Doctor Referral",
            file_name="06 - Architecture Patterns.pdf",
            extracted_text=self.ARCHITECTURE_TEXT,
            file_size=15000,
        )
        request = DocumentVerificationRequest(
            claim_id="reg-health-013",
            claim_type="Health",
            incident_date=date.today().isoformat(),
            claimed_amount=50000.0,
            documents=[doc],
        )
        result = agent.verify(request)
        assert result.complete is False
        assert result.fallback_used is True
        assert any("Doctor Referral" in inc.field for inc in result.inconsistencies)

    def test_health14_three_valid_docs_plus_architecture_referral_incomplete(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Medical Report", file_name="med.pdf", extracted_text=self.MEDICAL_REPORT_TEXT, file_size=10000),
            DocumentData(document_type="Hospital Bills", file_name="bill.pdf", extracted_text=self.HOSPITAL_BILL_TEXT, file_size=10000),
            DocumentData(document_type="Prescription", file_name="rx.pdf", extracted_text=self.PRESCRIPTION_TEXT, file_size=10000),
            DocumentData(document_type="Doctor Referral", file_name="06 - Architecture Patterns.pdf", extracted_text=self.ARCHITECTURE_TEXT, file_size=10000),
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-health-014",
            claim_type="Health",
            incident_date=today,
            claimed_amount=50000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is False
        assert any("Doctor Referral" in inc.field for inc in result.inconsistencies)

    def test_health15_all_four_valid_health_docs_complete(self):
        agent = DocumentVerificationAgent(gemini_client_instance=None)
        today = date.today().isoformat()
        docs = [
            DocumentData(document_type="Medical Report", file_name="med.pdf", extracted_text=self.MEDICAL_REPORT_TEXT, file_size=10000),
            DocumentData(document_type="Hospital Bills", file_name="bill.pdf", extracted_text=self.HOSPITAL_BILL_TEXT, file_size=10000),
            DocumentData(document_type="Prescription", file_name="rx.pdf", extracted_text=self.PRESCRIPTION_TEXT, file_size=10000),
            DocumentData(document_type="Doctor Referral", file_name="dummy_doctor_referral.pdf", extracted_text=self.GENUINE_REFERRAL_TEXT, file_size=10000),
        ]
        request = DocumentVerificationRequest(
            claim_id="reg-health-015",
            claim_type="Health",
            incident_date=today,
            claimed_amount=50000.0,
            documents=docs,
        )
        result = agent.verify(request)
        assert result.complete is True
        assert len(result.missing_items) == 0
        assert len([inc for inc in result.inconsistencies if inc.severity == "error"]) == 0
