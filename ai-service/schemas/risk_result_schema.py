"""Schema definitions for risk assessment results."""

from pydantic import BaseModel, Field, field_validator
from enum import Enum
from typing import List, Optional


class RecommendationType(str, Enum):
    """Valid recommendation values from the fraud/risk agent."""
    PROCEED = "proceed"
    ESCALATE = "escalate"


class RiskFlag(BaseModel):
    """A single flag raised during risk assessment."""

    flag_type: str = Field(
        ...,
        description="Category of the flag: DuplicateClaim, HighAmount, InconsistentData, SuspiciousPattern, FrequentClaims",
    )
    description: str = Field(..., description="Human-readable description of why the flag was raised")
    severity: str = Field(
        "Medium",
        description="Severity level: Low, Medium, High, Critical",
    )

    @field_validator("severity")
    @classmethod
    def validate_severity(cls, v: str) -> str:
        allowed = {"Low", "Medium", "High", "Critical"}
        if v not in allowed:
            return "Medium"
        return v

    @field_validator("flag_type")
    @classmethod
    def validate_flag_type(cls, v: str) -> str:
        canonical_map = {
            "duplicateclaim": "DuplicateClaim",
            "highamount": "HighAmount",
            "inconsistentdata": "InconsistentData",
            "suspiciouspattern": "SuspiciousPattern",
            "frequentclaims": "FrequentClaims",
            "documenttypemismatch": "DocumentTypeMismatch",
            "document_type_mismatch": "DocumentTypeMismatch",
            "documentunreadable": "DocumentUnreadable",
            "document_unreadable": "DocumentUnreadable",
            "duplicatedocumentreused": "DuplicateDocumentReused",
            "duplicate_document_reused": "DuplicateDocumentReused",
            "documentcontentinconsistent": "DocumentContentInconsistent",
            "document_content_inconsistent": "DocumentContentInconsistent",
            "documentverificationfailed": "DocumentVerificationFailed",
            "required_document_verification_failed": "DocumentVerificationFailed",
            "requireddocumentverificationfailed": "DocumentVerificationFailed",
        }
        clean = v.strip().lower()
        if clean in canonical_map:
            return canonical_map[clean]
        return "SuspiciousPattern"


class RiskAssessmentResult(BaseModel):
    """Structured output from the fraud/risk assessment agent."""

    risk_score: float = Field(
        ...,
        ge=0,
        le=100,
        description="Risk score between 0 (safe) and 100 (maximum risk)",
    )
    flags: List[RiskFlag] = Field(
        default_factory=list,
        description="List of fraud flags raised during analysis",
    )
    recommendation: RecommendationType = Field(
        RecommendationType.PROCEED,
        description="Final recommendation: proceed or escalate",
    )
    ai_used: bool = Field(default=False, description="Whether Gemini LLM reasoning was utilized")
    ai_provider: Optional[str] = Field(default=None, description="LLM provider name")
    ai_model: Optional[str] = Field(default=None, description="LLM model name used")
    reasoning_summary: Optional[str] = Field(default=None, description="Gemini contextual risk analysis and explanation")
    fallback_used: bool = Field(default=False, description="Whether deterministic fallback was used")

    @field_validator("risk_score", mode="before")
    @classmethod
    def clamp_score(cls, v: float) -> float:
        try:
            return max(0.0, min(100.0, float(v)))
        except (ValueError, TypeError):
            return 50.0
