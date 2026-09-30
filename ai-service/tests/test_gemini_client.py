"""
Unit and integration tests for the central GeminiReasoningClient.

Tests:
1. Missing API key behavior
2. Successful text generation (mocked)
3. Successful structured generation (mocked)
4. Timeout handling
5. Provider exception handling
6. Malformed JSON recovery
7. Connectivity check
8. Opt-in live connectivity test (gated on GEMINI_API_KEY)
"""

import os
import pytest
from unittest.mock import MagicMock, AsyncMock, patch
from pydantic import BaseModel
from services.gemini_client import GeminiReasoningClient, DEFAULT_GEMINI_MODEL


class SampleSchema(BaseModel):
    summary: str
    risk_level: str


class TestGeminiClientUnit:
    """Unit tests for GeminiReasoningClient with mocked SDK."""

    def test_missing_api_key_disables_client(self):
        client = GeminiReasoningClient(api_key="", model_name="gemini-2.5-flash")
        assert not client.is_available
        assert client.model_name == "gemini-2.5-flash"
        assert client.generate_text("Hello") is None
        assert client.generate_structured("Hello", SampleSchema) is None

    def test_sync_generate_text_success(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = "Analysis complete: documentation is valid."

        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.return_value = mock_response
        client._client = mock_genai_client

        result = client.generate_text("Prompt", system_instruction="Instruction")
        assert result == "Analysis complete: documentation is valid."

    @pytest.mark.asyncio
    async def test_async_generate_text_success(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = "Async reasoning output."

        mock_genai_client = MagicMock()
        mock_genai_client.aio.models.generate_content = AsyncMock(return_value=mock_response)
        client._client = mock_genai_client

        result = await client.generate_text_async("Prompt")
        assert result == "Async reasoning output."

    def test_sync_generate_structured_success(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = '{"summary": "Claim verified", "risk_level": "low"}'
        mock_response.parsed = None

        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.return_value = mock_response
        client._client = mock_genai_client

        result = client.generate_structured("Prompt", SampleSchema)
        assert isinstance(result, SampleSchema)
        assert result.summary == "Claim verified"
        assert result.risk_level == "low"

    @pytest.mark.asyncio
    async def test_async_generate_structured_success(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = '{"summary": "Async structured", "risk_level": "medium"}'
        mock_response.parsed = None

        mock_genai_client = MagicMock()
        mock_genai_client.aio.models.generate_content = AsyncMock(return_value=mock_response)
        client._client = mock_genai_client

        result = await client.generate_structured_async("Prompt", SampleSchema)
        assert isinstance(result, SampleSchema)
        assert result.summary == "Async structured"

    def test_sync_timeout_falls_back_gracefully(self):
        import httpx
        client = GeminiReasoningClient(api_key="test-key")
        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.side_effect = httpx.ReadTimeout("Request timed out")
        client._client = mock_genai_client

        result = client.generate_text("Prompt", timeout=0.05)
        assert result is None

    @pytest.mark.asyncio
    async def test_async_timeout_falls_back_gracefully(self):
        client = GeminiReasoningClient(api_key="test-key")

        async def slow_call(*args, **kwargs):
            import asyncio
            await asyncio.sleep(1.0)
            return MagicMock(text="Too late")

        mock_genai_client = MagicMock()
        mock_genai_client.aio.models.generate_content = slow_call
        client._client = mock_genai_client

        result = await client.generate_text_async("Prompt", timeout=0.05)
        assert result is None

    def test_provider_error_falls_back_gracefully(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.side_effect = RuntimeError("503 Service Unavailable")
        client._client = mock_genai_client

        result = client.generate_text("Prompt")
        assert result is None

    def test_malformed_json_returns_none(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = '{"malformed": true'  # invalid JSON
        mock_response.parsed = None

        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.return_value = mock_response
        client._client = mock_genai_client

        result = client.generate_structured("Prompt", SampleSchema)
        assert result is None

    def test_connectivity_check_success(self):
        client = GeminiReasoningClient(api_key="test-key")
        mock_response = MagicMock()
        mock_response.text = "READY"

        mock_genai_client = MagicMock()
        mock_genai_client.models.generate_content.return_value = mock_response
        client._client = mock_genai_client

        status = client.check_connectivity()
        assert status["configured"] is True
        assert status["connected"] is True

    def test_connectivity_check_when_not_configured(self):
        client = GeminiReasoningClient(api_key="")
        status = client.check_connectivity()
        assert status["configured"] is False
        assert status["connected"] is False

    def test_health_endpoint_reports_ai_readiness(self):
        from fastapi.testclient import TestClient
        from main import app

        client = TestClient(app)
        res = client.get("/health")
        assert res.status_code == 200
        data = res.json()
        assert data["status"] == "healthy"
        assert data["service"] == "ai-service"
        assert data["llm_provider"] == "gemini"
        assert "llm_configured" in data
        assert data["llm_model"] in ("gemini-3.5-flash", "gemini-3.6-flash", "gemini-2.5-flash")
        # Must never leak credentials
        assert "key" not in str(data).lower()
        assert "api_key" not in data

    def test_health_gemini_diagnostic_endpoint(self):
        from fastapi.testclient import TestClient
        from main import app

        client = TestClient(app)
        res = client.get("/health/gemini")
        assert res.status_code == 200
        data = res.json()
        assert "configured" in data
        assert "connected" in data
        assert data["model"] in ("gemini-3.5-flash", "gemini-3.6-flash", "gemini-2.5-flash")


class TestGeminiLiveIntegration:
    """Live connectivity integration test — opt-in only when GEMINI_API_KEY is in env."""

    @pytest.mark.live_ai
    @pytest.mark.skipif(
        not os.environ.get("GEMINI_API_KEY"),
        reason="GEMINI_API_KEY not set in environment (live test skipped)",
    )
    def test_live_gemini_ping(self):
        client = GeminiReasoningClient()
        assert client.is_available
        status = client.check_connectivity()
        assert status["configured"] is True
        assert status["connected"] is True, f"Live ping failed: {status}"
