"""
Central Gemini Client Abstraction

Provides reusable, safe, and testable Gemini LLM reasoning capabilities
for the hybrid agentic insurance claims system.

Key requirements fulfilled:
- Uses official google-genai SDK
- Reads GEMINI_API_KEY, GEMINI_MODEL, GEMINI_TIMEOUT_SECONDS from runtime environment
- Configurable default timeout (10.0s) kept well below ASP.NET Core 30s timeout
- Disables automatic retry loops (attempts=1) for interactive claim operations
- Direct SDK invocation (no ThreadPoolExecutor overhead or abandoned threads)
- Structured classification of errors (429 quota, 503 unavailable, timeout, auth)
- Safe failure mode: returns None on any failure (missing key, network, timeout, quota, malformed JSON)
- Data minimization: Never logs sensitive prompts, documents, or API keys
- Provides both sync and async methods for seamless integration with all agents
- Easy to mock in unit tests
"""

import asyncio
import logging
import os
import time
from typing import Any, Dict, Optional, Type, TypeVar
from pydantic import BaseModel
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)

# Configurable defaults
DEFAULT_GEMINI_MODEL = "gemini-2.5-flash"
DEFAULT_TIMEOUT_SECONDS = 10.0
MAX_SAFE_TIMEOUT_SECONDS = 20.0  # Must remain safely below ASP.NET 30-second timeout

T = TypeVar("T", bound=BaseModel)


def parse_timeout_seconds(raw_val: Any) -> float:
    """
    Validate timeout seconds and fall back to 10.0 for invalid/non-positive values.
    Caps at MAX_SAFE_TIMEOUT_SECONDS to ensure it stays below ASP.NET's 30s timeout.
    """
    try:
        if raw_val is None:
            return DEFAULT_TIMEOUT_SECONDS
        val = float(raw_val)
        if val <= 0:
            return DEFAULT_TIMEOUT_SECONDS
        return min(val, MAX_SAFE_TIMEOUT_SECONDS)
    except (ValueError, TypeError):
        return DEFAULT_TIMEOUT_SECONDS


class GeminiReasoningClient:
    """
    Central client abstraction for Gemini LLM reasoning.

    All agents interact through this client rather than importing or calling
    the Google GenAI SDK directly.
    """

    def __init__(
        self,
        api_key: Optional[str] = None,
        model_name: Optional[str] = None,
        timeout_seconds: Optional[float] = None,
    ):
        self._api_key = os.environ.get("GEMINI_API_KEY") if api_key is None else api_key
        self._model_name = os.environ.get("GEMINI_MODEL", DEFAULT_GEMINI_MODEL) if model_name is None else model_name

        timeout_env = os.environ.get("GEMINI_TIMEOUT_SECONDS")
        if timeout_seconds is not None:
            self._timeout_seconds = parse_timeout_seconds(timeout_seconds)
        elif timeout_env is not None:
            self._timeout_seconds = parse_timeout_seconds(timeout_env)
        else:
            self._timeout_seconds = DEFAULT_TIMEOUT_SECONDS

        self._client: Optional[Any] = None

        if self._api_key and self._api_key.strip():
            try:
                from google import genai
                from google.genai import types

                http_options = types.HttpOptions(
                    timeout=int(self._timeout_seconds * 1000),
                    retry_options=types.HttpRetryOptions(attempts=1),
                )
                self._client = genai.Client(
                    api_key=self._api_key.strip(),
                    http_options=http_options,
                )
                logger.info(
                    "Gemini provider configured: yes (model: %s, timeout: %.1fs, retries: 0)",
                    self._model_name,
                    self._timeout_seconds,
                )
            except Exception as e:
                logger.warning("Failed to initialize Google GenAI client: %s", type(e).__name__)
                self._client = None
        else:
            logger.info("Gemini provider configured: no — deterministic fallback enabled")

    @property
    def is_available(self) -> bool:
        """Return True if Gemini client is initialized and ready for calls."""
        return self._client is not None and bool(self._api_key)

    @property
    def model_name(self) -> str:
        """Return the active model name."""
        return self._model_name

    @property
    def timeout_seconds(self) -> float:
        """Return the configured timeout in seconds."""
        return self._timeout_seconds

    def classify_error(self, exc: Exception) -> Dict[str, str]:
        """
        Classify Gemini API errors into safe, structured categories.
        Never exposes API keys, tokens, or sensitive parameters.
        """
        err_str = str(exc)
        exc_type = type(exc).__name__
        status_code = getattr(exc, "code", None)
        if status_code is None and hasattr(exc, "status_code"):
            status_code = getattr(exc, "status_code")

        # 1. Timeout
        if (
            "Timeout" in exc_type
            or "timed out" in err_str.lower()
            or isinstance(exc, asyncio.TimeoutError)
        ):
            return {
                "status": "timeout",
                "message": "Gemini request timed out.",
            }

        # 2. Quota / Rate limit (429)
        if (
            status_code == 429
            or "429" in err_str
            or "RESOURCE_EXHAUSTED" in err_str.upper()
            or "QUOTA" in err_str.upper()
        ):
            return {
                "status": "quota_exceeded",
                "message": "Gemini quota is currently exhausted.",
            }

        # 3. Authentication / Permission (401, 403)
        if (
            status_code in (401, 403)
            or "UNAUTHENTICATED" in err_str.upper()
            or "PERMISSION_DENIED" in err_str.upper()
            or "API_KEY_INVALID" in err_str.upper()
        ):
            return {
                "status": "authentication_error",
                "message": "Gemini authentication or API key invalid.",
            }

        # 4. Model not found / unavailable (404)
        if (
            status_code == 404
            or "NOT_FOUND" in err_str.upper()
            or "NOT FOUND" in err_str.upper()
            or "is not found for API version" in err_str
        ):
            return {
                "status": "model_unavailable",
                "message": f"Configured Gemini model '{self._model_name}' is not available.",
            }

        # 5. Service unavailable / High demand (503, 5xx)
        if (
            status_code in (500, 502, 503, 504)
            or "UNAVAILABLE" in err_str.upper()
            or "HIGH DEMAND" in err_str.upper()
        ):
            return {
                "status": "service_unavailable",
                "message": "Gemini service is temporarily unavailable.",
            }

        # 6. General error
        return {
            "status": "error",
            "message": f"Gemini call failed: {exc_type}.",
        }

    # ── Synchronous Generation ──────────────────────────────────────

    def generate_text(
        self,
        prompt: str,
        system_instruction: Optional[str] = None,
        timeout: Optional[float] = None,
        operation_name: str = "text_generation",
    ) -> Optional[str]:
        """
        Generate text response synchronously with SDK-level timeout and safe fallback.
        Calls the Google GenAI SDK directly without thread executor.
        """
        if not self.is_available:
            return None

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                system_instruction=system_instruction,
                http_options=http_options,
            )

            response = self._client.models.generate_content(
                model=self._model_name,
                contents=prompt,
                config=config,
            )

            if response and response.text:
                return response.text.strip()
            return None

        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini %s failed [%s]: %s (elapsed=%.2fs). Falling back to safe deterministic default.",
                operation_name,
                classification["status"],
                classification["message"],
                elapsed,
            )
            return None

    def generate_structured(
        self,
        prompt: str,
        response_schema: Type[T],
        system_instruction: Optional[str] = None,
        timeout: Optional[float] = None,
        operation_name: str = "structured_generation",
    ) -> Optional[T]:
        """
        Generate structured response synchronously validated against a Pydantic schema.
        Calls the Google GenAI SDK directly without thread executor.
        """
        if not self.is_available:
            return None

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                response_mime_type="application/json",
                response_schema=response_schema,
                system_instruction=system_instruction,
                http_options=http_options,
            )

            response = self._client.models.generate_content(
                model=self._model_name,
                contents=prompt,
                config=config,
            )

            if not response or not response.text:
                return None

            if hasattr(response, "parsed") and isinstance(response.parsed, response_schema):
                return response.parsed

            return response_schema.model_validate_json(response.text)

        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini %s failed [%s]: %s (elapsed=%.2fs). Falling back to safe deterministic default.",
                operation_name,
                classification["status"],
                classification["message"],
                elapsed,
            )
            return None

    # ── Asynchronous Generation ─────────────────────────────────────

    async def generate_text_async(
        self,
        prompt: str,
        system_instruction: Optional[str] = None,
        timeout: Optional[float] = None,
        operation_name: str = "async_text_generation",
    ) -> Optional[str]:
        """
        Generate text response asynchronously with SDK-level timeout and safe fallback.
        """
        if not self.is_available:
            return None

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                system_instruction=system_instruction,
                http_options=http_options,
            )

            response = await asyncio.wait_for(
                self._client.aio.models.generate_content(
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                ),
                timeout=effective_timeout,
            )

            if response and response.text:
                return response.text.strip()
            return None

        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini %s failed [%s]: %s (elapsed=%.2fs). Falling back to safe deterministic default.",
                operation_name,
                classification["status"],
                classification["message"],
                elapsed,
            )
            return None

    async def generate_structured_async(
        self,
        prompt: str,
        response_schema: Type[T],
        system_instruction: Optional[str] = None,
        timeout: Optional[float] = None,
        operation_name: str = "async_structured_generation",
    ) -> Optional[T]:
        """
        Generate structured response asynchronously validated against a Pydantic schema.
        """
        if not self.is_available:
            return None

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                response_mime_type="application/json",
                response_schema=response_schema,
                system_instruction=system_instruction,
                http_options=http_options,
            )

            response = await asyncio.wait_for(
                self._client.aio.models.generate_content(
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                ),
                timeout=effective_timeout,
            )

            if not response or not response.text:
                return None

            if hasattr(response, "parsed") and isinstance(response.parsed, response_schema):
                return response.parsed

            return response_schema.model_validate_json(response.text)

        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini %s failed [%s]: %s (elapsed=%.2fs). Falling back to safe deterministic default.",
                operation_name,
                classification["status"],
                classification["message"],
                elapsed,
            )
            return None

    # ── Connectivity Check ──────────────────────────────────────────

    async def check_connectivity_async(self, timeout: Optional[float] = None) -> Dict[str, Any]:
        """Safe async connectivity verification using a harmless test prompt."""
        if not self.is_available:
            return {
                "configured": False,
                "connected": False,
                "model": self._model_name,
                "status": "not_configured",
                "message": "GEMINI_API_KEY is not configured.",
                "elapsed_seconds": 0.0,
            }

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                system_instruction="You are a system health check assistant. Respond with only READY.",
                http_options=http_options,
            )
            response = await asyncio.wait_for(
                self._client.aio.models.generate_content(
                    model=self._model_name,
                    contents="Return the word READY.",
                    config=config,
                ),
                timeout=effective_timeout,
            )
            elapsed = round(time.monotonic() - start_time, 3)
            reply = response.text.strip() if response and response.text else ""
            connected = "READY" in reply.upper()
            return {
                "configured": True,
                "connected": connected,
                "model": self._model_name,
                "status": "connected" if connected else "unexpected_reply",
                "message": "Connection verified" if connected else "Unexpected reply from Gemini.",
                "elapsed_seconds": elapsed,
            }
        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini connectivity check failed [%s]: %s (elapsed=%.2fs)",
                classification["status"], classification["message"], elapsed
            )
            return {
                "configured": True,
                "connected": False,
                "model": self._model_name,
                "status": classification["status"],
                "message": classification["message"],
                "elapsed_seconds": elapsed,
            }

    def check_connectivity(self, timeout: Optional[float] = None) -> Dict[str, Any]:
        """Safe sync connectivity verification using a harmless test prompt."""
        if not self.is_available:
            return {
                "configured": False,
                "connected": False,
                "model": self._model_name,
                "status": "not_configured",
                "message": "GEMINI_API_KEY is not configured.",
                "elapsed_seconds": 0.0,
            }

        effective_timeout = parse_timeout_seconds(timeout) if timeout is not None else self._timeout_seconds
        start_time = time.monotonic()
        try:
            from google.genai import types

            http_options = types.HttpOptions(
                timeout=int(effective_timeout * 1000),
                retry_options=types.HttpRetryOptions(attempts=1),
            )
            config = types.GenerateContentConfig(
                system_instruction="You are a system health check assistant. Respond with only READY.",
                http_options=http_options,
            )
            response = self._client.models.generate_content(
                model=self._model_name,
                contents="Return the word READY.",
                config=config,
            )
            elapsed = round(time.monotonic() - start_time, 3)
            reply = response.text.strip() if response and response.text else ""
            connected = "READY" in reply.upper()
            return {
                "configured": True,
                "connected": connected,
                "model": self._model_name,
                "status": "connected" if connected else "unexpected_reply",
                "message": "Connection verified" if connected else "Unexpected reply from Gemini.",
                "elapsed_seconds": elapsed,
            }
        except Exception as e:
            elapsed = round(time.monotonic() - start_time, 3)
            classification = self.classify_error(e)
            logger.warning(
                "Gemini connectivity check failed [%s]: %s (elapsed=%.2fs)",
                classification["status"], classification["message"], elapsed
            )
            return {
                "configured": True,
                "connected": False,
                "model": self._model_name,
                "status": classification["status"],
                "message": classification["message"],
                "elapsed_seconds": elapsed,
            }


# Default global instance
gemini_client = GeminiReasoningClient()
