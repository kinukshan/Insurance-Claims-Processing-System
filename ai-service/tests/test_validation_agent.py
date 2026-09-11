"""Tests for the Validation / Safety Agent."""

import sys
import os
import pytest

# Add parent directory to path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from agents.validation_agent import ValidationSafetyAgent
from schemas.payout_result_schema import PayoutValidationRequest


@pytest.fixture
def agent():
    return ValidationSafetyAgent()


@pytest.fixture
def valid_request():
    """A valid payout proposal that should pass all checks."""
    return PayoutValidationRequest(
        claim_id="test-claim-001",
        policy_type="Comprehensive",
        claim_type="Vehicle Damage",
        approved_claim_amount=15000.00,
        coverage_limit=50000.00,
        deductible=500.00,
        proposed_payout=14500.00,  # min(15000, 50000) - 500 = 14500
    )


# ── Valid proposal tests ─────────────────────────────────────────────


def test_valid_proposal_passes(agent, valid_request):
    """A correctly calculated proposal should pass validation."""
    result = agent.validate_payout_proposal(valid_request)
    assert result.valid is True
    assert len(result.violations) == 0
    assert result.requires_human_approval is True
    assert result.agent_id == "validation-safety-agent"


def test_valid_proposal_always_requires_human_approval(agent, valid_request):
    """All payouts must require human approval regardless of validity."""
    result = agent.validate_payout_proposal(valid_request)
    assert result.requires_human_approval is True


def test_valid_proposal_has_auditable_summary(agent, valid_request):
    """Result should include an auditable summary."""
    result = agent.validate_payout_proposal(valid_request)
    assert "PASSED" in result.summary
    assert "test-claim-001" in result.summary


# ── Coverage violation tests ─────────────────────────────────────────


def test_coverage_violation_detected(agent):
    """Proposed payout exceeding coverage limit should fail."""
    request = PayoutValidationRequest(
        claim_id="test-claim-002",
        policy_type="Basic",
        claim_type="Fire Damage",
        approved_claim_amount=100000.00,
        coverage_limit=50000.00,
        deductible=1000.00,
        proposed_payout=99000.00,  # Wrong: should be min(100000, 50000) - 1000 = 49000
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is False
    assert any("COVERAGE_VIOLATION" in v for v in result.violations)


# ── Deductible correctness tests ─────────────────────────────────────


def test_incorrect_deductible_detected(agent):
    """Incorrect deductible application should fail."""
    request = PayoutValidationRequest(
        claim_id="test-claim-003",
        policy_type="Comprehensive",
        claim_type="Theft",
        approved_claim_amount=10000.00,
        coverage_limit=50000.00,
        deductible=500.00,
        proposed_payout=10000.00,  # Wrong: should be 10000 - 500 = 9500
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is False
    assert any("DEDUCTIBLE_ERROR" in v for v in result.violations)


# ── Invalid schema tests ─────────────────────────────────────────────


def test_invalid_schema_rejected(agent):
    """Malformed input should be safely rejected via validate_from_dict."""
    result = agent.validate_from_dict({})
    assert result.valid is False
    assert any("SCHEMA_ERROR" in v for v in result.violations)


def test_partial_schema_rejected(agent):
    """Partial input with missing fields should be rejected."""
    result = agent.validate_from_dict({
        "claim_id": "test",
        "policy_type": "Basic",
    })
    assert result.valid is False
    assert len(result.violations) > 0


def test_negative_amount_in_schema_rejected(agent):
    """Negative amounts should be caught by schema validation."""
    result = agent.validate_from_dict({
        "claim_id": "test",
        "policy_type": "Basic",
        "claim_type": "Fire",
        "approved_claim_amount": -1000,
        "coverage_limit": 50000,
        "deductible": 500,
        "proposed_payout": -1500,
    })
    assert result.valid is False


# ── Missing approval tests ───────────────────────────────────────────


def test_missing_claim_type_flagged(agent):
    """Empty claim type should be flagged."""
    request = PayoutValidationRequest(
        claim_id="test-claim-004",
        policy_type="Comprehensive",
        claim_type="",
        approved_claim_amount=5000.00,
        coverage_limit=50000.00,
        deductible=250.00,
        proposed_payout=4750.00,
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is False
    assert any("MISSING_CLAIM_TYPE" in v for v in result.violations)


def test_missing_policy_type_flagged(agent):
    """Empty policy type should be flagged."""
    request = PayoutValidationRequest(
        claim_id="test-claim-005",
        policy_type="",
        claim_type="Vehicle Damage",
        approved_claim_amount=5000.00,
        coverage_limit=50000.00,
        deductible=250.00,
        proposed_payout=4750.00,
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is False
    assert any("MISSING_POLICY_TYPE" in v for v in result.violations)


# ── Safe failure tests ───────────────────────────────────────────────


def test_safe_failure_on_malformed_input(agent):
    """Agent should not crash on completely malformed input."""
    result = agent.validate_from_dict({"garbage": True, "random": 42})
    assert result.valid is False
    assert result.requires_human_approval is True
    assert result.agent_id == "validation-safety-agent"


def test_safe_failure_returns_structured_result(agent):
    """Even failures should return properly structured results."""
    result = agent.validate_from_dict(None)  # type: ignore
    # Should not raise — should return a structured failure
    assert result.valid is False
    assert isinstance(result.violations, list)
    assert result.requires_human_approval is True


# ── Zero/edge case tests ─────────────────────────────────────────────


def test_zero_payout_passes(agent):
    """Zero payout (deductible >= eligible) should pass validation."""
    request = PayoutValidationRequest(
        claim_id="test-claim-006",
        policy_type="Basic",
        claim_type="Minor Damage",
        approved_claim_amount=500.00,
        coverage_limit=50000.00,
        deductible=1000.00,
        proposed_payout=0.00,  # max(0, 500 - 1000) = 0
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is True


def test_exact_coverage_limit_passes(agent):
    """Payout exactly at coverage limit should pass."""
    request = PayoutValidationRequest(
        claim_id="test-claim-007",
        policy_type="Premium",
        claim_type="Total Loss",
        approved_claim_amount=100000.00,
        coverage_limit=50000.00,
        deductible=0.00,
        proposed_payout=50000.00,  # min(100000, 50000) - 0 = 50000
    )
    result = agent.validate_payout_proposal(request)
    assert result.valid is True
