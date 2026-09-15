"""Schema definitions for claim data structures used by the AI agents."""

from pydantic import BaseModel, Field
from typing import Optional
from datetime import date


class DocumentData(BaseModel):
    """Represents a single document attached to a claim."""

    document_type: str = Field(..., description="Type of document (e.g., 'Police Report', 'Photos of Damage')")
    file_name: str = Field(..., description="Original file name")
    uploaded_at: Optional[str] = Field(None, description="Upload date (YYYY-MM-DD)")
    verification_status: Optional[str] = Field("Pending", description="Current verification status")


class DocumentVerificationRequest(BaseModel):
    """Request payload for the Document Verification Agent."""

    claim_id: str = Field(..., description="Unique claim identifier")
    claim_type: str = Field(..., description="Type of insurance claim (Auto, Home, Health, etc.)")
    incident_date: str = Field(..., description="Date of the incident (YYYY-MM-DD)")
    claimed_amount: float = Field(..., ge=0, description="Amount being claimed")
    documents: list[DocumentData] = Field(default_factory=list, description="List of submitted documents")
