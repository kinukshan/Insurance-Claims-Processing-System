"""Schema definitions for document verification results."""

from __future__ import annotations

from typing import Optional
from pydantic import BaseModel, Field


class DocumentInconsistency(BaseModel):
    """Represents a single inconsistency found during document verification."""

    field: str = Field(..., description="The field or area with the inconsistency")
    description: str = Field(..., description="Description of the inconsistency")
    severity: str = Field("warning", description="Severity level: info, warning, error")


class DocumentVerificationResult(BaseModel):
    """Structured output from the Document Verification Agent."""

    complete: bool = Field(..., description="Whether all required documents are present")
    missing_items: list[str] = Field(default_factory=list, description="List of missing required document types")
    inconsistencies: list[DocumentInconsistency] = Field(
        default_factory=list, description="List of detected inconsistencies"
    )
    warnings: list[str] = Field(default_factory=list, description="General warnings or notes")
    ai_used: bool = Field(default=False, description="Whether Gemini LLM reasoning was utilized")
    ai_provider: Optional[str] = Field(default=None, description="LLM provider name")
    ai_model: Optional[str] = Field(default=None, description="LLM model name used")
    reasoning_summary: Optional[str] = Field(default=None, description="Gemini contextual reasoning and reviewer explanation")
    fallback_used: bool = Field(default=False, description="Whether deterministic fallback was used")
