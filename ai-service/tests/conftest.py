"""
Pytest configuration and guardrails.

Enforces Instruction 16:
Normal unit tests must NEVER make live Gemini API calls.
Live calls are strictly restricted to tests marked with @pytest.mark.live_ai.
"""

import pytest


@pytest.fixture(autouse=True)
def guard_live_gemini_in_unit_tests(request, monkeypatch):
    """
    Prevent unit tests from making unexpected external network calls to Gemini.
    Only tests decorated with @pytest.mark.live_ai may interact with the live API.
    """
    if "live_ai" in request.keywords:
        return  # Permit live integration test

    # In regular unit tests, ensure the default global client runs in deterministic fallback mode
    from services.gemini_client import gemini_client

    monkeypatch.setattr(gemini_client, "_client", None)
    monkeypatch.setattr(gemini_client, "_api_key", None)
