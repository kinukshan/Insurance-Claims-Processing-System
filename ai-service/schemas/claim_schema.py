"""Schema definitions for claim data structures used by the AI agents."""

from __future__ import annotations

from pydantic import BaseModel, Field
from typing import Optional, Union
from datetime import date, datetime
from uuid import UUID


class DocumentData(BaseModel):
    """Represents a single document attached to a claim."""

    document_type: str = Field(..., description="Type of document (e.g., 'Police Report', 'Photos of Damage')")
    file_name: str = Field(..., description="Original file name")
    uploaded_at: Optional[str] = Field(None, description="Upload date (YYYY-MM-DD)")
    verification_status: Optional[str] = Field("Pending", description="Current verification status")
    file_url: Optional[str] = Field(None, description="Relative URL / storage path of the document")
    content_type: Optional[str] = Field(None, description="MIME content type")
    file_size: Optional[int] = Field(None, description="File size in bytes")
    file_hash: Optional[str] = Field(None, description="SHA-256 hash of file contents")
    extracted_text: Optional[str] = Field(None, description="Extracted textual content for verification")


class DocumentVerificationRequest(BaseModel):
    """Request payload for the Document Verification Agent."""

    claim_id: str = Field(..., description="Unique claim identifier")
    claim_type: str = Field(..., description="Type of insurance claim (Auto, Home, Health, etc.)")
    incident_date: str = Field(..., description="Date of the incident (YYYY-MM-DD)")
    claimed_amount: float = Field(..., ge=0, description="Amount being claimed")
    documents: list[DocumentData] = Field(default_factory=list, description="List of submitted documents")


class ClaimData(BaseModel):
    """Claim data received from the ASP.NET Core backend for risk analysis."""

    claim_id: UUID = Field(..., description="Unique identifier of the claim")
    policy_holder_id: UUID = Field(..., description="Policyholder who submitted the claim")
    claim_amount: float = Field(..., ge=0, description="Amount claimed")
    description: str = Field("", description="Claim description")
    incident_date: datetime = Field(..., description="Date of the incident")
    incident_location: str = Field("", description="Location where incident occurred")
    claim_type: Optional[str] = Field("Motor", description="Type of claim (Motor, Home, Health, Life, etc.)")
    documents: list[DocumentData] = Field(default_factory=list, description="Documents attached to the claim")
    document_flags: list[Union[dict, str]] = Field(default_factory=list, description="Deterministic document verification flags")
