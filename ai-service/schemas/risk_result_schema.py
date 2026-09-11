"""Schema definitions for risk assessment results."""

from pydantic import BaseModel, Field, field_validator
from enum import Enum
from typing import List


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
        allowed = {
            "DuplicateClaim",
            "HighAmount",
            "InconsistentData",
            "SuspiciousPattern",
            "FrequentClaims",
        }
        if v not in allowed:
            return "SuspiciousPattern"
        return v


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

    @field_validator("risk_score")
    @classmethod
    def clamp_score(cls, v: float) -> float:
        return max(0.0, min(100.0, v))
