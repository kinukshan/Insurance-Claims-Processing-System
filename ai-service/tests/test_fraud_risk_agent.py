"""
Tests for the Fraud / Risk Assessment Agent — Component C (Member 3).

Covers:
- Low-risk claim assessment
- High-amount claim flag creation
- Duplicate claim detection
- Tool failure safe fallback
- Score bounds validation (0-100)
- Pydantic schema validation
- Safe escalation recommendation
"""

import pytest
import sys
import os
from unittest.mock import AsyncMock, patch, MagicMock
from datetime import datetime
from uuid import uuid4

# Add project root to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from agents.fraud_risk_agent import FraudRiskAgent, HIGH_AMOUNT_THRESHOLD
from schemas.claim_schema import ClaimData
from schemas.risk_result_schema import RiskAssessmentResult, RiskFlag, RecommendationType
from tools.claim_tools import compare_claim_amount
from tools.duplicate_claim_tools import detect_duplicate_claims
from tools.claim_history_tools import analyze_claim_frequency, analyze_historical_patterns


# ── Fixtures ──────────────────────────────────────────────────

@pytest.fixture
def agent():
    return FraudRiskAgent()


@pytest.fixture
def low_risk_claim():
    return ClaimData(
        claim_id=uuid4(),
        policy_holder_id=uuid4(),
        claim_amount=5000.0,
        description="Minor car scratch on bumper",
        incident_date=datetime(2026, 8, 15),
        incident_location="Colombo",
    )


@pytest.fixture
def high_amount_claim():
    return ClaimData(
        claim_id=uuid4(),
        policy_holder_id=uuid4(),
        claim_amount=120000.0,
        description="Total loss vehicle fire",
        incident_date=datetime(2026, 8, 20),
        incident_location="Kandy",
    )


# ══════════════════════════════════════════════════════════════
# Test 1 — Low-risk claim produces low score and proceed
# ══════════════════════════════════════════════════════════════

@pytest.mark.asyncio
async def test_low_risk_claim_returns_proceed(agent, low_risk_claim):
    """A normal small-amount claim with no history should score low and recommend proceed."""
    with patch(
        "agents.fraud_risk_agent.lookup_claim_history",
        new_callable=AsyncMock,
        return_value=[],
    ):
        result = await agent.assess(low_risk_claim)

    assert isinstance(result, RiskAssessmentResult)
    assert result.risk_score < 30.0
    assert result.recommendation == RecommendationType.PROCEED
    assert len(result.flags) == 0


# ══════════════════════════════════════════════════════════════
# Test 2 — High amount claim creates HighAmount flag
# ══════════════════════════════════════════════════════════════

@pytest.mark.asyncio
async def test_high_amount_creates_flag(agent, high_amount_claim):
    """A claim above the threshold should produce a HighAmount flag."""
    with patch(
        "agents.fraud_risk_agent.lookup_claim_history",
        new_callable=AsyncMock,
        return_value=[],
    ):
        result = await agent.assess(high_amount_claim)

    assert result.risk_score > 0
    high_flags = [f for f in result.flags if f.flag_type == "HighAmount"]
    assert len(high_flags) >= 1
    assert high_flags[0].severity in ("High", "Critical")


# ══════════════════════════════════════════════════════════════
# Test 3 — Duplicate claim detection
# ══════════════════════════════════════════════════════════════

@pytest.mark.asyncio
async def test_duplicate_claim_detected(agent, low_risk_claim):
    """When history contains an identical claim, a DuplicateClaim flag should be raised."""
    past_claims = [
        {
            "id": str(uuid4()),
            "description": low_risk_claim.description,
            "incidentDate": low_risk_claim.incident_date.isoformat(),
            "claimAmount": low_risk_claim.claim_amount,
            "incidentLocation": low_risk_claim.incident_location,
        }
    ]

    with patch(
        "agents.fraud_risk_agent.lookup_claim_history",
        new_callable=AsyncMock,
        return_value=past_claims,
    ):
        result = await agent.assess(low_risk_claim)

    dup_flags = [f for f in result.flags if f.flag_type == "DuplicateClaim"]
    assert len(dup_flags) >= 1


# ══════════════════════════════════════════════════════════════
# Test 4 — Tool failure triggers safe fallback
# ══════════════════════════════════════════════════════════════

@pytest.mark.asyncio
async def test_tool_failure_safe_fallback(agent, low_risk_claim):
    """If a tool raises an exception, the agent should return a safe fallback result."""
    with patch(
        "agents.fraud_risk_agent.lookup_claim_history",
        new_callable=AsyncMock,
        side_effect=RuntimeError("Connection refused"),
    ):
        result = await agent.assess(low_risk_claim)

    assert isinstance(result, RiskAssessmentResult)
    assert result.risk_score == 50.0
    assert result.recommendation == RecommendationType.ESCALATE
    assert len(result.flags) >= 1


# ══════════════════════════════════════════════════════════════
# Test 5 — Score is always clamped to 0-100
# ══════════════════════════════════════════════════════════════

def test_score_clamping_via_schema():
    """Pydantic schema should clamp risk_score to [0, 100]."""
    result_low = RiskAssessmentResult(risk_score=-10.0, flags=[], recommendation="proceed")
    assert result_low.risk_score == 0.0

    result_high = RiskAssessmentResult(risk_score=150.0, flags=[], recommendation="proceed")
    assert result_high.risk_score == 100.0


# ══════════════════════════════════════════════════════════════
# Test 6 — Invalid flag_type is sanitized
# ══════════════════════════════════════════════════════════════

def test_invalid_flag_type_sanitized():
    """Invalid flag types should be normalized to SuspiciousPattern."""
    flag = RiskFlag(flag_type="InvalidType", description="test", severity="Medium")
    assert flag.flag_type == "SuspiciousPattern"


# ══════════════════════════════════════════════════════════════
# Test 7 — Invalid severity is sanitized
# ══════════════════════════════════════════════════════════════

def test_invalid_severity_sanitized():
    """Invalid severity values should default to Medium."""
    flag = RiskFlag(flag_type="HighAmount", description="test", severity="SuperHigh")
    assert flag.severity == "Medium"


# ══════════════════════════════════════════════════════════════
# Test 8 — Recommendation enum accepts only valid values
# ══════════════════════════════════════════════════════════════

def test_recommendation_enum_values():
    """Only 'proceed' and 'escalate' are valid recommendations."""
    assert RecommendationType.PROCEED.value == "proceed"
    assert RecommendationType.ESCALATE.value == "escalate"

    with pytest.raises(ValueError):
        RecommendationType("deny")


# ══════════════════════════════════════════════════════════════
# Test 9 — compare_claim_amount tool with valid inputs
# ══════════════════════════════════════════════════════════════

def test_compare_claim_amount_above_threshold():
    """Amount above threshold should return exceeds_threshold=True with correct ratio."""
    result = compare_claim_amount(100000.0, 50000.0)
    assert result["exceeds_threshold"] is True
    assert result["ratio"] == 2.0


def test_compare_claim_amount_below_threshold():
    """Amount below threshold should return exceeds_threshold=False."""
    result = compare_claim_amount(10000.0, 50000.0)
    assert result["exceeds_threshold"] is False


def test_compare_claim_amount_invalid_input():
    """Negative amount should be handled gracefully."""
    result = compare_claim_amount(-100.0, 50000.0)
    assert result["exceeds_threshold"] is False


# ══════════════════════════════════════════════════════════════
# Test 10 — detect_duplicate_claims tool
# ══════════════════════════════════════════════════════════════

def test_detect_duplicate_claims_finds_match():
    """Identical claims should be detected as duplicates."""
    current = {
        "claim_id": str(uuid4()),
        "description": "Water damage in kitchen",
        "incident_date": "2026-06-15",
        "claim_amount": 10000.0,
        "incident_location": "Jaffna",
    }
    history = [
        {
            "id": str(uuid4()),
            "description": "Water damage in kitchen",
            "incidentDate": "2026-06-15",
            "claimAmount": 10000.0,
            "incidentLocation": "Jaffna",
        }
    ]
    result = detect_duplicate_claims(current, history)
    assert result["is_duplicate"] is True
    assert result["duplicate_count"] >= 1


def test_detect_duplicate_claims_no_match():
    """Different claims should not be flagged as duplicates."""
    current = {
        "claim_id": str(uuid4()),
        "description": "Car accident on highway",
        "incident_date": "2026-06-15",
        "claim_amount": 10000.0,
        "incident_location": "Colombo",
    }
    history = [
        {
            "id": str(uuid4()),
            "description": "House flood in basement",
            "incidentDate": "2026-03-01",
            "claimAmount": 50000.0,
            "incidentLocation": "Kandy",
        }
    ]
    result = detect_duplicate_claims(current, history)
    assert result["is_duplicate"] is False


# ══════════════════════════════════════════════════════════════
# Test 11 — analyze_claim_frequency tool
# ══════════════════════════════════════════════════════════════

def test_analyze_claim_frequency_frequent():
    """More than 3 claims should be flagged as frequent."""
    claims = [{"id": str(uuid4())} for _ in range(5)]
    result = analyze_claim_frequency(claims)
    assert result["is_frequent"] is True
    assert result["claim_count"] == 5


def test_analyze_claim_frequency_normal():
    """3 or fewer claims should not be flagged."""
    claims = [{"id": str(uuid4())} for _ in range(2)]
    result = analyze_claim_frequency(claims)
    assert result["is_frequent"] is False


def test_analyze_claim_frequency_empty():
    """Empty history should return safe defaults."""
    result = analyze_claim_frequency([])
    assert result["is_frequent"] is False
    assert result["total_claims"] == 0


# ══════════════════════════════════════════════════════════════
# Test 12 — analyze_historical_patterns tool
# ══════════════════════════════════════════════════════════════

def test_analyze_historical_patterns_escalating_amounts():
    """Consistently increasing amounts should be detected."""
    claims = [
        {"claimAmount": 1000, "description": "Claim A"},
        {"claimAmount": 2000, "description": "Claim B"},
        {"claimAmount": 3000, "description": "Claim C"},
    ]
    result = analyze_historical_patterns(claims)
    assert result["patterns_found"] is True
    assert any("increasing" in p.lower() for p in result["patterns"])


def test_analyze_historical_patterns_repetitive_descriptions():
    """Repetitive descriptions should be detected."""
    claims = [
        {"claimAmount": 1000, "description": "water damage"},
        {"claimAmount": 2000, "description": "water damage"},
        {"claimAmount": 3000, "description": "water damage"},
    ]
    result = analyze_historical_patterns(claims)
    assert result["patterns_found"] is True


def test_analyze_historical_patterns_empty():
    """Empty claims list should find no patterns."""
    result = analyze_historical_patterns([])
    assert result["patterns_found"] is False


# ══════════════════════════════════════════════════════════════
# Test 13 — Agent allowed tools list
# ══════════════════════════════════════════════════════════════

def test_agent_allowed_tools():
    """Agent should only have the expected tool set (least privilege)."""
    agent = FraudRiskAgent()
    expected = {
        "compare_claim_amount",
        "lookup_claim_history",
        "analyze_claim_frequency",
        "analyze_historical_patterns",
        "detect_duplicate_claims",
    }
    assert set(agent._allowed_tools) == expected


# ══════════════════════════════════════════════════════════════
# Test 14 — High score triggers escalate recommendation
# ══════════════════════════════════════════════════════════════

@pytest.mark.asyncio
async def test_high_score_triggers_escalation(agent, high_amount_claim):
    """A high-risk claim with duplicates should recommend escalation."""
    past_claims = [
        {
            "id": str(uuid4()),
            "description": high_amount_claim.description,
            "incidentDate": high_amount_claim.incident_date.isoformat(),
            "claimAmount": high_amount_claim.claim_amount,
            "incidentLocation": high_amount_claim.incident_location,
        }
    ]

    with patch(
        "agents.fraud_risk_agent.lookup_claim_history",
        new_callable=AsyncMock,
        return_value=past_claims,
    ):
        result = await agent.assess(high_amount_claim)

    # High amount (120k) + duplicate = should exceed escalation threshold
    assert result.risk_score >= 50.0
    assert result.recommendation == RecommendationType.ESCALATE
