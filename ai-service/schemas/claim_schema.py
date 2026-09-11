"""Schema definitions for claim data structures."""

from pydantic import BaseModel, Field
from datetime import datetime
from uuid import UUID


class ClaimData(BaseModel):
    """Claim data received from the ASP.NET Core backend for risk analysis."""

    claim_id: UUID = Field(..., description="Unique identifier of the claim")
    policy_holder_id: UUID = Field(..., description="Policyholder who submitted the claim")
    claim_amount: float = Field(..., ge=0, description="Amount claimed")
    description: str = Field("", description="Claim description")
    incident_date: datetime = Field(..., description="Date of the incident")
    incident_location: str = Field("", description="Location where incident occurred")
