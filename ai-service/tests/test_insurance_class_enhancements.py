"""
Tests for Insurance Class / Life Insurance enhancements in AI Service.
Verifies requirement 42:
- compatibility matrix matches backend
- historical Auto/Home accepted
- unknown combinations fail closed
- Life documents match backend
- Beneficiary ID alias accepted
- alias deduplication works
- deterministic invalid payout skips Gemini
- ai_used=false on deterministic invalid
- Gemini failure does not change deterministic result
- fallback remains bounded/fast
"""

import pytest
from unittest.mock import MagicMock
from validation.business_rule_validator import (
    validate_claim_policy_compatibility,
    validate_deductible_correctness,
    normalize_policy_type,
)
from agents.validation_agent import ValidationSafetyAgent
from agents.document_verification_agent import DocumentVerificationAgent
from schemas.payout_result_schema import PayoutValidationRequest
from schemas.claim_schema import DocumentVerificationRequest, DocumentData


class TestCompatibilityMatrix:
    """Matrix tests matching C# PolicyClaimCompatibility."""

    @pytest.mark.parametrize(
        "policy,claim",
        [
            ("Motor Insurance", "Motor"),
            ("Motor Insurance", "Auto"),  # Historical Auto accepted
            ("Motor", "Motor"),
            ("Auto Insurance", "Auto"),
            ("Health Insurance", "Health"),
            ("Health", "Health"),
            ("Home Insurance", "Property"),
            ("Home Insurance", "Home"),  # Historical Home accepted
            ("Home / Property Insurance", "Property"),
            ("Property Insurance", "Property"),
            ("Life Insurance", "Life"),
            ("Life", "Life"),
        ],
    )
    def test_compatible_combinations_pass(self, policy, claim):
        assert validate_claim_policy_compatibility(claim, policy) is None

    @pytest.mark.parametrize(
        "policy,claim",
        [
            ("Motor Insurance", "Property"),
            ("Motor Insurance", "Health"),
            ("Motor Insurance", "Life"),
            ("Health Insurance", "Motor"),
            ("Health Insurance", "Property"),
            ("Health Insurance", "Life"),
            ("Home Insurance", "Motor"),
            ("Home Insurance", "Health"),
            ("Home Insurance", "Life"),
            ("Life Insurance", "Property"),
            ("Life Insurance", "Health"),
            ("Life Insurance", "Motor"),
            ("Life Insurance", "Auto"),
            # Unknown / future products fail closed
            ("Travel Insurance", "Travel"),
            ("Pension", "Life"),
            ("Annuity", "Life"),
            ("Marine", "Property"),
            ("Unknown Insurance", "Motor"),
            # Unsupported claim types
            ("Motor Insurance", "Liability"),
            ("Motor Insurance", "Other"),
            ("Motor Insurance", "Travel"),
            ("Life Insurance", "Other"),
        ],
    )
    def test_incompatible_combinations_fail_closed(self, policy, claim):
        violation = validate_claim_policy_compatibility(claim, policy)
        assert violation is not None
        assert "COMPATIBILITY_VIOLATION" in violation


class TestLifeDeductibleRule:
    """Project rule: Life Insurance + Life claim -> effective deductible = 0."""

    def test_life_deductible_zero_enforced(self):
        # Even if request has non-zero deductible, expected calculation uses 0 for Life
        result = validate_deductible_correctness(
            approved_claim_amount=80000.0,
            coverage_limit=100000.0,
            deductible=5000.0,  # Legacy/tampered non-zero deductible
            proposed_payout=80000.0,  # min(80000, 100000) = 80000
            policy_type="Life Insurance",
            claim_type="Life",
        )
        assert result is None  # Passes because effective deductible is 0

    def test_life_payout_capped_at_coverage_limit(self):
        result = validate_deductible_correctness(
            approved_claim_amount=150000.0,
            coverage_limit=100000.0,
            deductible=0.0,
            proposed_payout=100000.0,  # Capped at coverage limit
            policy_type="Life Insurance",
            claim_type="Life",
        )
        assert result is None


class TestDeterministicShortCircuit:
    """Deterministic invalid proposals must skip Gemini and return ai_used=False."""

    def test_invalid_payout_skips_gemini(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        agent = ValidationSafetyAgent(gemini_client_instance=mock_gemini)

        # Incompatible pair: Life + Motor
        request = PayoutValidationRequest(
            claim_id="CLM-SHORT-001",
            policy_type="Life Insurance",
            claim_type="Motor",
            approved_claim_amount=10000.0,
            coverage_limit=50000.0,
            deductible=0.0,
            proposed_payout=10000.0,
        )

        result = agent.validate_payout_proposal(request)

        assert result.valid is False
        assert result.ai_used is False
        assert result.fallback_used is False
        mock_gemini.generate_text.assert_not_called()

    def test_gemini_failure_preserves_deterministic_valid_result(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("500 Internal Server Error")

        agent = ValidationSafetyAgent(gemini_client_instance=mock_gemini)

        request = PayoutValidationRequest(
            claim_id="CLM-LIFE-001",
            policy_type="Life Insurance",
            claim_type="Life",
            approved_claim_amount=50000.0,
            coverage_limit=100000.0,
            deductible=0.0,
            proposed_payout=50000.0,
        )

        result = agent.validate_payout_proposal(request)

        # Deterministic result is strictly preserved
        assert result.valid is True
        assert result.ai_used is False
        assert result.fallback_used is True
        assert result.requires_human_approval is True


class TestLifeDocumentChecklistAndAliases:
    """Life document verification and Beneficiary ID alias handling."""

    def test_life_all_canonical_documents_complete(self):
        agent = DocumentVerificationAgent()
        docs = [
            DocumentData(document_type="Death Certificate", file_name="death.pdf"),
            DocumentData(document_type="Policy Document", file_name="policy.pdf"),
            DocumentData(document_type="Beneficiary / Nominee Identification", file_name="beneficiary_id.pdf"),
            DocumentData(document_type="Claim Form", file_name="claim_form.pdf"),
        ]
        missing = agent._check_completeness("Life", docs)
        assert missing == []

    def test_beneficiary_id_alias_satisfies_requirement(self):
        agent = DocumentVerificationAgent()
        # Upload historical "Beneficiary ID" instead of full canonical name
        docs = [
            DocumentData(document_type="Death Certificate", file_name="death.pdf"),
            DocumentData(document_type="Policy Document", file_name="policy.pdf"),
            DocumentData(document_type="Beneficiary ID", file_name="b_id.pdf"),
            DocumentData(document_type="Claim Form", file_name="claim_form.pdf"),
        ]
        missing = agent._check_completeness("Life", docs)
        assert missing == []

    def test_duplicate_aliases_do_not_duplicate_requirements(self):
        agent = DocumentVerificationAgent()
        # Upload both "Beneficiary ID" and "Beneficiary / Nominee Identification"
        docs = [
            DocumentData(document_type="Beneficiary ID", file_name="b_id.pdf"),
            DocumentData(document_type="Beneficiary / Nominee Identification", file_name="b_nominee.pdf"),
        ]
        missing = agent._check_completeness("Life", docs)
        # Should be missing Death Certificate, Policy Document, Claim Form (exactly 3, not needing Beneficiary ID again)
        assert len(missing) == 3
        assert "Death Certificate" in missing
        assert "Policy Document" in missing
        assert "Claim Form" in missing
        assert "Beneficiary / Nominee Identification" not in missing

    def test_motor_checklist_matches_auto(self):
        agent = DocumentVerificationAgent()
        auto_required = agent.REQUIRED_DOCUMENTS["Auto"]
        motor_required = agent.REQUIRED_DOCUMENTS["Motor"]
        assert auto_required == motor_required


class TestPercentageDeductibleRules:
    """Test suite for percentage deductible rules and coverage capping."""

    @pytest.mark.parametrize(
        "policy_type,claim_type,approved,limit,percentage,expected_ded,expected_payout",
        [
            ("Motor Insurance", "Motor", 20000.0, 100000.0, 5.0, 1000.0, 19000.0),
            ("Health Insurance", "Health", 20000.0, 100000.0, 10.0, 2000.0, 18000.0),
            ("Home Insurance", "Property", 20000.0, 100000.0, 10.0, 2000.0, 18000.0),
            ("Life Insurance", "Life", 20000.0, 100000.0, 0.0, 0.0, 20000.0),
        ],
    )
    def test_percentage_matrix(
        self, policy_type, claim_type, approved, limit, percentage, expected_ded, expected_payout
    ):
        result = validate_deductible_correctness(
            approved_claim_amount=approved,
            coverage_limit=limit,
            deductible=expected_ded,
            proposed_payout=expected_payout,
            policy_type=policy_type,
            claim_type=claim_type,
            deductible_percentage=percentage,
        )
        assert result is None

    def test_coverage_capping_motor(self):
        """Motor coverage limit: $50,000, Approved claim: $80,000 -> eligible: $50,000, ded: 5% = $2,500, payout: $47,500"""
        result = validate_deductible_correctness(
            approved_claim_amount=80000.0,
            coverage_limit=50000.0,
            deductible=2500.0,
            proposed_payout=47500.0,
            policy_type="Motor Insurance",
            claim_type="Motor",
            deductible_percentage=5.0,
        )
        assert result is None

    def test_incorrect_percentage_deductible_detected(self):
        """If proposed payout does not deduct the correct percentage, flag violation."""
        result = validate_deductible_correctness(
            approved_claim_amount=20000.0,
            coverage_limit=100000.0,
            deductible=1000.0,
            proposed_payout=20000.0,  # Forgot to subtract deductible
            policy_type="Motor Insurance",
            claim_type="Motor",
            deductible_percentage=5.0,
        )
        assert result is not None
        assert "DEDUCTIBLE_ERROR" in result
