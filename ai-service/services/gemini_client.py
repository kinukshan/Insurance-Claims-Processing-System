"""
Central Gemini Client Abstraction

Provides reusable, safe, and testable Gemini LLM reasoning capabilities
for the hybrid agentic insurance claims system.

Key requirements fulfilled:
- Uses official google-genai SDK
- Reads GEMINI_API_KEY and GEMINI_MODEL exclusively from runtime environment
- Isolated configurable default model: gemini-2.5-flash
- Safe failure mode: returns None on any failure (missing key, network, timeout, quota, malformed JSON)
- Data minimization: Never logs sensitive prompts or API keys
- Provides both sync and async methods for seamless integration with all agents
- Easy to mock in unit tests
"""

import asyncio
import concurrent.futures
import logging
import os
from typing import Any, Dict, Optional, Type, TypeVar
from pydantic import BaseModel
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)

# Configurable default model isolated in configuration
DEFAULT_GEMINI_MODEL = "gemini-3.5-flash"
DEFAULT_TIMEOUT_SECONDS = 20.0

T = TypeVar("T", bound=BaseModel)


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
    ):
        self._api_key = os.environ.get("GEMINI_API_KEY") if api_key is None else api_key
        self._model_name = os.environ.get("GEMINI_MODEL", DEFAULT_GEMINI_MODEL) if model_name is None else model_name
        self._client: Optional[Any] = None

        if self._api_key and self._api_key.strip():
            try:
                from google import genai
                self._client = genai.Client(api_key=self._api_key.strip())
                logger.info("Gemini provider configured: yes (model: %s)", self._model_name)
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

    # ── Synchronous Generation ──────────────────────────────────────

    def generate_text(
        self,
        prompt: str,
        system_instruction: Optional[str] = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> Optional[str]:
        """
        Generate text response synchronously with timeout and safe fallback.
        """
        if not self.is_available:
            return None

        try:
            from google.genai import types

            config = types.GenerateContentConfig(
                system_instruction=system_instruction,
            )

            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
                future = executor.submit(
                    self._client.models.generate_content,
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                )
                response = future.result(timeout=timeout)

            if response and response.text:
                return response.text.strip()
            return None

        except concurrent.futures.TimeoutError:
            logger.warning("Gemini generate_text timed out after %.1fs; falling back.", timeout)
            return None
        except Exception as e:
            logger.warning("Gemini generate_text error (%s); falling back.", type(e).__name__)
            return None

    def generate_structured(
        self,
        prompt: str,
        response_schema: Type[T],
        system_instruction: Optional[str] = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> Optional[T]:
        """
        Generate structured response synchronously validated against a Pydantic schema.
        """
        if not self.is_available:
            return None

        try:
            from google.genai import types

            config = types.GenerateContentConfig(
                response_mime_type="application/json",
                response_schema=response_schema,
                system_instruction=system_instruction,
            )

            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
                future = executor.submit(
                    self._client.models.generate_content,
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                )
                response = future.result(timeout=timeout)

            if not response or not response.text:
                return None

            if hasattr(response, "parsed") and isinstance(response.parsed, response_schema):
                return response.parsed

            return response_schema.model_validate_json(response.text)

        except concurrent.futures.TimeoutError:
            logger.warning("Gemini generate_structured timed out after %.1fs; falling back.", timeout)
            return None
        except Exception as e:
            logger.warning("Gemini generate_structured error (%s); falling back.", type(e).__name__)
            return None

    # ── Asynchronous Generation ─────────────────────────────────────

    async def generate_text_async(
        self,
        prompt: str,
        system_instruction: Optional[str] = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> Optional[str]:
        """
        Generate text response asynchronously with timeout and safe fallback.
        """
        if not self.is_available:
            return None

        try:
            from google.genai import types

            config = types.GenerateContentConfig(
                system_instruction=system_instruction,
            )

            response = await asyncio.wait_for(
                self._client.aio.models.generate_content(
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                ),
                timeout=timeout,
            )

            if response and response.text:
                return response.text.strip()
            return None

        except asyncio.TimeoutError:
            logger.warning("Gemini generate_text_async timed out after %.1fs; falling back.", timeout)
            return None
        except Exception as e:
            logger.warning("Gemini generate_text_async error (%s); falling back.", type(e).__name__)
            return None

    async def generate_structured_async(
        self,
        prompt: str,
        response_schema: Type[T],
        system_instruction: Optional[str] = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> Optional[T]:
        """
        Generate structured response asynchronously validated against a Pydantic schema.
        """
        if not self.is_available:
            return None

        try:
            from google.genai import types

            config = types.GenerateContentConfig(
                response_mime_type="application/json",
                response_schema=response_schema,
                system_instruction=system_instruction,
            )

            response = await asyncio.wait_for(
                self._client.aio.models.generate_content(
                    model=self._model_name,
                    contents=prompt,
                    config=config,
                ),
                timeout=timeout,
            )

            if not response or not response.text:
                return None

            if hasattr(response, "parsed") and isinstance(response.parsed, response_schema):
                return response.parsed

            return response_schema.model_validate_json(response.text)

        except asyncio.TimeoutError:
            logger.warning("Gemini generate_structured_async timed out after %.1fs; falling back.", timeout)
            return None
        except Exception as e:
            logger.warning("Gemini generate_structured_async error (%s); falling back.", type(e).__name__)
            return None

    # ── Connectivity Check ──────────────────────────────────────────

    async def check_connectivity_async(self, timeout: float = 15.0) -> Dict[str, Any]:
        """Safe async connectivity verification using a harmless test prompt."""
        if not self.is_available:
            return {
                "configured": False,
                "connected": False,
                "model": self._model_name,
                "message": "GEMINI_API_KEY is not configured.",
            }

        try:
            reply = await self.generate_text_async(
                prompt="Return the word READY.",
                system_instruction="You are a system health check assistant. Respond with only READY.",
                timeout=timeout,
            )
            connected = reply is not None and "READY" in reply.upper()
            return {
                "configured": True,
                "connected": connected,
                "model": self._model_name,
                "message": "Connection verified" if connected else "Unexpected reply from Gemini.",
            }
        except Exception as e:
            return {
                "configured": True,
                "connected": False,
                "model": self._model_name,
                "message": f"Connection check failed: {type(e).__name__}",
            }

    def check_connectivity(self, timeout: float = 15.0) -> Dict[str, Any]:
        """Safe sync connectivity verification using a harmless test prompt."""
        if not self.is_available:
            return {
                "configured": False,
                "connected": False,
                "model": self._model_name,
                "message": "GEMINI_API_KEY is not configured.",
            }

        try:
            reply = self.generate_text(
                prompt="Return the word READY.",
                system_instruction="You are a system health check assistant. Respond with only READY.",
                timeout=timeout,
            )
            connected = reply is not None and "READY" in reply.upper()
            return {
                "configured": True,
                "connected": connected,
                "model": self._model_name,
                "message": "Connection verified" if connected else "Unexpected reply from Gemini.",
            }
        except Exception as e:
            return {
                "configured": True,
                "connected": False,
                "model": self._model_name,
                "message": f"Connection check failed: {type(e).__name__}",
            }


# Default global instance
gemini_client = GeminiReasoningClient()
