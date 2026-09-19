"""
Schema definitions for payout proposal validation.

These Pydantic models define the structured input/output contracts
for the Validation / Safety Agent.
"""

from datetime import datetime
from typing import List, Optional
from pydantic import BaseModel, Field


class PayoutValidationRequest(BaseModel):
    """
    Input contract for the Validation / Safety Agent.
    All values come from trusted backend data via IPayoutContextProvider.
    """
    claim_id: str = Field(..., description="Claim identifier")
    policy_type: str = Field(..., description="Policy type name")
    claim_type: str = Field(..., description="Claim type description")
    approved_claim_amount: float = Field(..., ge=0, description="Approved claim amount")
    coverage_limit: float = Field(..., ge=0, description="Maximum coverage limit")
    deductible: float = Field(..., ge=0, description="Deductible amount")
    proposed_payout: float = Field(..., ge=0, description="Proposed payout amount")


class PayoutValidationResult(BaseModel):
    """
    Structured output contract from the Validation / Safety Agent.
    Only structured validation results — no chain-of-thought stored.
    """
    valid: bool = Field(..., description="Whether the proposal passes all checks")
    violations: List[str] = Field(default_factory=list, description="List of rule violations")
    requires_human_approval: bool = Field(
        default=True,
        description="Whether human approval is required before execution"
    )
    agent_id: str = Field(default="validation-safety-agent")
    timestamp: datetime = Field(default_factory=datetime.utcnow)
    summary: str = Field(
        default="",
        description="Auditable summary of the validation result"
    )
    ai_used: bool = Field(default=False, description="Whether Gemini LLM reasoning was utilized")
    ai_provider: Optional[str] = Field(default=None, description="LLM provider name")
    ai_model: Optional[str] = Field(default=None, description="LLM model name used")
    reasoning_summary: Optional[str] = Field(default=None, description="Gemini contextual explanation of payout validity")
    fallback_used: bool = Field(default=False, description="Whether deterministic fallback was used")


class PayoutProposal(BaseModel):
    """
    Validated payout proposal record.
    """
    claim_id: str
    approved_claim_amount: float
    coverage_limit: float
    deductible: float
    eligible_amount: float
    proposed_payout: float
    validation_result: Optional[PayoutValidationResult] = None
