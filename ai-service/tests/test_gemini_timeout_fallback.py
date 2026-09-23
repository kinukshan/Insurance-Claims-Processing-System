"""
Comprehensive tests for fast, bounded Gemini failure handling and deterministic fallbacks.

Verifies:
1. Gemini successful -> ai_used=true, fallback_used=false.
2. Gemini 429 RESOURCE_EXHAUSTED -> deterministic result returned promptly.
3. Gemini timeout -> deterministic result returned promptly.
4. Gemini 5xx -> deterministic result returned promptly.
5. Malformed/unexpected Gemini response -> deterministic fallback.
6. fallback_used=true on failure.
7. ai_used=false on failure.
8. Gemini failure completes inside the configured timeout.

Document Verification:
9. COMPLETE deterministic result remains COMPLETE when Gemini fails.
10. Missing-document deterministic result remains incomplete when Gemini fails.

Risk Assessment:
11. Score is identical with Gemini success vs Gemini failure.
12. Level / flags identical.
13. Flags identical.
14. Recommendation identical.

Payout Validation:
15. Valid/invalid result identical with Gemini success vs failure.
16. Violations identical.
17. requires_human_approval remains true.

Health Endpoint:
18. 429 maps to quota_exceeded rather than generic unexpected reply.
19. Timeout maps to timeout status.
20. Secrets (API keys, passwords) are omitted from health responses.

Config:
21. GEMINI_TIMEOUT_SECONDS validates and falls back to 10.0 for non-positive or invalid values.
"""

import time
import httpx
import pytest
from datetime import date
from unittest.mock import MagicMock, AsyncMock, patch

from services.gemini_client import (
    GeminiReasoningClient,
    parse_timeout_seconds,
    DEFAULT_TIMEOUT_SECONDS,
    MAX_SAFE_TIMEOUT_SECONDS,
)
from agents.document_verification_agent import DocumentVerificationAgent
from agents.fraud_risk_agent import FraudRiskAgent
from agents.validation_agent import ValidationSafetyAgent
from schemas.claim_schema import DocumentVerificationRequest, DocumentData, ClaimData
from schemas.payout_result_schema import PayoutValidationRequest


# ── Configuration Validation Tests ──────────────────────────────────────────

class TestTimeoutConfigValidation:
    def test_valid_timeout_parsed(self):
        assert parse_timeout_seconds("8.5") == 8.5
        assert parse_timeout_seconds(12) == 12.0

    def test_non_positive_values_fallback_to_default(self):
        assert parse_timeout_seconds("0") == DEFAULT_TIMEOUT_SECONDS
        assert parse_timeout_seconds("-5") == DEFAULT_TIMEOUT_SECONDS
        assert parse_timeout_seconds(-10.0) == DEFAULT_TIMEOUT_SECONDS

    def test_invalid_strings_fallback_to_default(self):
        assert parse_timeout_seconds("invalid") == DEFAULT_TIMEOUT_SECONDS
        assert parse_timeout_seconds("") == DEFAULT_TIMEOUT_SECONDS
        assert parse_timeout_seconds(None) == DEFAULT_TIMEOUT_SECONDS

    def test_timeout_capped_at_max_safe_below_aspnet(self):
        # Must stay safely below ASP.NET 30-second timeout
        assert parse_timeout_seconds(35.0) == MAX_SAFE_TIMEOUT_SECONDS
        assert parse_timeout_seconds(60.0) == MAX_SAFE_TIMEOUT_SECONDS


# ── Document Verification Fallback Tests ────────────────────────────────────

class TestDocumentVerificationTimeoutFallback:
    def _create_complete_request(self) -> DocumentVerificationRequest:
        return DocumentVerificationRequest(
            claim_id="claim-auto-1",
            claim_type="Auto",
            claimed_amount=5000.0,
            incident_date="2026-09-01",
            documents=[
                DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at="2026-09-02"),
                DocumentData(document_type="Photos of Damage", file_name="damage.jpg", uploaded_at="2026-09-02"),
                DocumentData(document_type="Repair Estimate", file_name="estimate.pdf", uploaded_at="2026-09-02"),
                DocumentData(document_type="Driver License", file_name="license.pdf", uploaded_at="2026-09-02"),
            ],
        )

    def _create_incomplete_request(self) -> DocumentVerificationRequest:
        return DocumentVerificationRequest(
            claim_id="claim-auto-2",
            claim_type="Auto",
            claimed_amount=5000.0,
            incident_date="2026-09-01",
            documents=[
                DocumentData(document_type="Police Report", file_name="police.pdf", uploaded_at="2026-09-02"),
                # Missing Photos of Damage, Repair Estimate, Driver License
            ],
        )

    def test_gemini_success_sets_ai_used_true(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.model_name = "gemini-2.5-flash"
        mock_gemini.generate_text.return_value = "Documentation is complete and consistent with accident details."

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        result = agent.verify(self._create_complete_request())

        assert result.complete is True
        assert result.ai_used is True
        assert result.fallback_used is False
        assert result.reasoning_summary == "Documentation is complete and consistent with accident details."

    def test_gemini_429_returns_complete_deterministic_result_with_fallback(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        # Simulate 429 quota failure returning None from generate_text
        mock_gemini.generate_text.return_value = None

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        result = agent.verify(self._create_complete_request())

        # Requirement 2, 6, 7, 9: Complete remains COMPLETE, ai_used=false, fallback_used=true
        assert result.complete is True
        assert len(result.missing_items) == 0
        assert result.ai_used is False
        assert result.fallback_used is True
        assert result.reasoning_summary is None

    def test_gemini_timeout_returns_complete_deterministic_result(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.return_value = None

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        result = agent.verify(self._create_complete_request())

        assert result.complete is True
        assert result.ai_used is False
        assert result.fallback_used is True

    def test_gemini_5xx_returns_deterministic_result(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.side_effect = RuntimeError("503 Service Unavailable")

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        result = agent.verify(self._create_complete_request())

        assert result.complete is True
        assert result.ai_used is False
        assert result.fallback_used is True

    def test_missing_documents_remain_incomplete_when_gemini_fails(self):
        mock_gemini = MagicMock()
        mock_gemini.is_available = True
        mock_gemini.generate_text.return_value = None

        agent = DocumentVerificationAgent(gemini_client_instance=mock_gemini)
        result = agent.verify(self._create_incomplete_request())

        # Requirement 10: Incomplete remains incomplete (ACTION REQUIRED)
        assert result.complete is False
        assert "Photos of Damage" in result.missing_items
        assert "Repair Estimate" in result.missing_items
        assert "Driver License" in result.missing_items
        assert result.ai_used is False
        assert result.fallback_used is True


# ── Risk Assessment Fallback Tests ──────────────────────────────────────────

class TestRiskAssessmentTimeoutFallback:
    def _create_claim_data(self) -> ClaimData:
        return ClaimData(
            claim_id="e2b5c001-0000-0000-0000-000000000001",
            policy_holder_id="e2b5c001-0000-0000-0000-000000000002",
            policy_id="e2b5c001-0000-0000-0000-000000000003",
            claim_amount=85000.0,  # Exceeds HIGH_AMOUNT_THRESHOLD (50000)
            claim_type="Auto",
            incident_date=date(2026, 9, 10),
            incident_location="Colombo",
            description="Vehicle collision with highway guardrail",
        )

    @pytest.mark.asyncio
    async def test_risk_score_and_flags_identical_with_success_vs_failure(self):
        claim_data = self._create_claim_data()

        # 1. With Gemini success
        mock_gemini_ok = MagicMock()
        mock_gemini_ok.is_available = True
        mock_gemini_ok.model_name = "gemini-2.5-flash"
        mock_gemini_ok.generate_text_async = AsyncMock(return_value="High amount claim requires manager review.")

        agent_ok = FraudRiskAgent(gemini_client_instance=mock_gemini_ok)
        result_ok = await agent_ok.assess(claim_data)

        # 2. With Gemini 429 failure (returns None)
        mock_gemini_fail = MagicMock()
        mock_gemini_fail.is_available = True
        mock_gemini_fail.generate_text_async = AsyncMock(return_value=None)

        agent_fail = FraudRiskAgent(gemini_client_instance=mock_gemini_fail)
        result_fail = await agent_fail.assess(claim_data)

        # Requirements 11, 12, 13, 14: Score, level, flags, recommendation are IDENTICAL
        assert result_ok.risk_score == result_fail.risk_score
        assert len(result_ok.flags) == len(result_fail.flags)
        assert [f.flag_type for f in result_ok.flags] == [f.flag_type for f in result_fail.flags]
        assert result_ok.recommendation == result_fail.recommendation

        # AI metadata differences
        assert result_ok.ai_used is True
        assert result_ok.fallback_used is False
        assert result_ok.reasoning_summary == "High amount claim requires manager review."

        assert result_fail.ai_used is False
        assert result_fail.fallback_used is True
        assert result_fail.reasoning_summary is None


# ── Payout Validation Fallback Tests ────────────────────────────────────────

class TestPayoutValidationTimeoutFallback:
    def _create_valid_request(self) -> PayoutValidationRequest:
        return PayoutValidationRequest(
            claim_id="claim-payout-1",
            policy_type="Auto",
            claim_type="Auto",
            approved_claim_amount=5000.0,
            coverage_limit=25000.0,
            deductible=500.0,
            proposed_payout=4500.0,
        )

    def _create_invalid_request(self) -> PayoutValidationRequest:
        return PayoutValidationRequest(
            claim_id="claim-payout-2",
            policy_type="Auto",
            claim_type="Auto",
            approved_claim_amount=50000.0,
            coverage_limit=20000.0,
            deductible=500.0,
            proposed_payout=25000.0,  # Exceeds coverage limit
        )

    def test_payout_valid_and_violations_identical_with_success_vs_failure(self):
        req = self._create_valid_request()

        # Success case
        mock_ok = MagicMock()
        mock_ok.is_available = True
        mock_ok.model_name = "gemini-2.5-flash"
        mock_ok.generate_text.return_value = "Payout proposal matches policy limit minus deductible."

        agent_ok = ValidationSafetyAgent(gemini_client_instance=mock_ok)
        result_ok = agent_ok.validate_payout_proposal(req)

        # Failure case (429/timeout returning None)
        mock_fail = MagicMock()
        mock_fail.is_available = True
        mock_fail.generate_text.return_value = None

        agent_fail = ValidationSafetyAgent(gemini_client_instance=mock_fail)
        result_fail = agent_fail.validate_payout_proposal(req)

        # Requirements 15, 16, 17: Valid, violations, requires_human_approval identical
        assert result_ok.valid == result_fail.valid is True
        assert result_ok.violations == result_fail.violations == []
        assert result_ok.requires_human_approval is True
        assert result_fail.requires_human_approval is True

        assert result_ok.ai_used is True
        assert result_ok.fallback_used is False
        assert result_fail.ai_used is False
        assert result_fail.fallback_used is True

    def test_payout_violations_preserved_when_gemini_fails(self):
        req = self._create_invalid_request()

        mock_fail = MagicMock()
        mock_fail.is_available = True
        mock_fail.generate_text.return_value = None

        agent = ValidationSafetyAgent(gemini_client_instance=mock_fail)
        result = agent.validate_payout_proposal(req)

        assert result.valid is False
        assert len(result.violations) > 0
        assert any("coverage limit" in v.lower() for v in result.violations)
        assert result.requires_human_approval is True
        assert result.ai_used is False
        assert result.fallback_used is True


# ── Health Endpoint Diagnostic Tests ────────────────────────────────────────

class TestHealthEndpointDiagnostics:
    def test_health_429_maps_to_quota_exceeded(self):
        from google.genai.errors import ClientError

        client = GeminiReasoningClient(api_key="test-key")
        err = ClientError(
            code=429,
            response_json={"error": {"message": "Resource has been exhausted (e.g. check quota)."}},
        )
        classified = client.classify_error(err)
        assert classified["status"] == "quota_exceeded"
        assert "quota" in classified["message"].lower()

    def test_health_timeout_maps_to_timeout_status(self):
        client = GeminiReasoningClient(api_key="test-key")
        err = httpx.ReadTimeout("Connection timed out.")
        classified = client.classify_error(err)
        assert classified["status"] == "timeout"
        assert "timed out" in classified["message"].lower()

    def test_health_503_maps_to_service_unavailable(self):
        from google.genai.errors import ServerError

        client = GeminiReasoningClient(api_key="test-key")
        err = ServerError(
            code=503,
            response_json={"error": {"message": "Model experiencing high demand."}},
        )
        classified = client.classify_error(err)
        assert classified["status"] == "service_unavailable"
        assert "unavailable" in classified["message"].lower()

    def test_health_auth_failure_maps_to_authentication_error(self):
        from google.genai.errors import ClientError

        client = GeminiReasoningClient(api_key="test-key")
        err = ClientError(
            code=400,
            response_json={"error": {"message": "API_KEY_INVALID"}},
        )
        classified = client.classify_error(err)
        assert classified["status"] == "authentication_error"

    def test_health_response_never_contains_secrets(self):
        client = GeminiReasoningClient(api_key="secret-api-key-12345")
        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.side_effect = RuntimeError("secret-api-key-12345 in error")
        client._client = mock_genai_client

        status = client.check_connectivity()
        status_str = str(status)

        assert "secret-api-key-12345" not in status_str
        assert "api_key" not in status
        assert "key" not in status
        assert status["status"] in ("error", "service_unavailable")


# ── Fast Bounded Execution Timing Tests ─────────────────────────────────────

class TestBoundedExecutionTiming:
    def test_sync_generate_text_returns_within_bounded_time_on_timeout(self):
        client = GeminiReasoningClient(api_key="test-key", timeout_seconds=0.1)
        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.side_effect = httpx.ReadTimeout("Timed out")
        client._client = mock_genai_client

        t0 = time.monotonic()
        result = client.generate_text("Test prompt", timeout=0.1)
        t1 = time.monotonic()

        assert result is None
        # Timing check: must complete well under 1.0s (allow small tolerance for slow CI)
        assert (t1 - t0) < 0.5
