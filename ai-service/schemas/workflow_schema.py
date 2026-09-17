"""Schema definitions for the claim processing workflow."""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Optional
from pydantic import BaseModel, Field


class StepType(str, Enum):
    """Types of workflow steps in the claim processing pipeline."""
    DOCUMENT_VERIFICATION = "DOCUMENT_VERIFICATION"
    FRAUD_RISK_ASSESSMENT = "FRAUD_RISK_ASSESSMENT"
    PAYOUT_PREPARATION = "PAYOUT_PREPARATION"
    VALIDATION = "VALIDATION"
    HUMAN_APPROVAL = "HUMAN_APPROVAL"


class StepStatus(str, Enum):
    """Execution status of a workflow step."""
    PENDING = "PENDING"
    IN_PROGRESS = "IN_PROGRESS"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    SKIPPED = "SKIPPED"


class WorkflowStatus(str, Enum):
    """Overall status of a workflow."""
    CREATED = "CREATED"
    IN_PROGRESS = "IN_PROGRESS"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"


class WorkflowObjective(BaseModel):
    """Input objective for the coordinator to plan a workflow."""
    claim_id: str = Field(..., description="The ID of the claim to process (e.g., CLM-123)")
    claim_type: Optional[str] = Field(None, description="Type of claim (e.g., auto, health, property)")
    claim_amount: Optional[float] = Field(None, description="Claimed amount", ge=0)
    priority: Optional[str] = Field("normal", description="Priority level: low, normal, high, urgent")
    notes: Optional[str] = Field(None, description="Additional notes or context")


class WorkflowStep(BaseModel):
    """A single step in the workflow plan."""
    step_index: int = Field(..., description="Order of the step (1-based)")
    step_type: StepType = Field(..., description="Type of step to execute")
    description: str = Field(..., description="Human-readable description of what this step does")
    assigned_agent: str = Field(..., description="Agent responsible for this step")
    status: StepStatus = Field(default=StepStatus.PENDING, description="Current status")
    depends_on: list[int] = Field(default_factory=list, description="Step indices this step depends on")
    result: Optional[dict] = Field(None, description="Structured result from the agent")
    error: Optional[str] = Field(None, description="Error message if step failed")
    started_at: Optional[datetime] = Field(None, description="When execution started")
    completed_at: Optional[datetime] = Field(None, description="When execution completed")


class WorkflowPlan(BaseModel):
    """A structured workflow plan created by the coordinator."""
    workflow_id: str = Field(..., description="Unique identifier for this workflow")
    claim_id: str = Field(..., description="The claim being processed")
    steps: list[WorkflowStep] = Field(..., description="Ordered list of steps to execute")
    status: WorkflowStatus = Field(default=WorkflowStatus.CREATED, description="Overall status")
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)
    notes: Optional[str] = Field(None, description="Planning notes from the coordinator")


class StepResult(BaseModel):
    """Result from executing a single workflow step."""
    step_index: int
    status: StepStatus
    result: Optional[dict] = None
    error: Optional[str] = None
    completed_at: datetime = Field(default_factory=datetime.utcnow)


class WorkflowResult(BaseModel):
    """Final result of the complete workflow."""
    workflow_id: str
    claim_id: str
    status: WorkflowStatus
    steps: list[WorkflowStep]
    summary: str = Field(..., description="Human-readable summary of the workflow outcome")
    completed_at: datetime = Field(default_factory=datetime.utcnow)
