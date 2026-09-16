"""Tools for detecting duplicate claims.

These are controlled/allow-listed tool abstractions.
The AI agent uses these instead of direct database access.
"""

from typing import List
import logging

logger = logging.getLogger(__name__)


def detect_duplicate_claims(
    claim_data: dict, historical_claims: List[dict]
) -> dict:
    """
    Check for potential duplicate claims by comparing against historical data.

    Matching criteria:
    - Same description and incident date
    - Similar claim amount (within 10% tolerance)
    - Same incident location

    Args:
        claim_data: The current claim being assessed.
        historical_claims: List of past claims for the same policyholder.

    Returns:
        Dictionary with duplicate detection results.
    """
    if not claim_data or not historical_claims:
        return {"is_duplicate": False, "duplicates": [], "duplicate_count": 0}

    current_desc = claim_data.get("description", "").lower().strip()
    current_date = claim_data.get("incident_date", "")
    current_amount = claim_data.get("claim_amount", 0)
    current_location = claim_data.get("incident_location", "").lower().strip()
    current_id = str(claim_data.get("claim_id", ""))

    duplicates = []

    for past_claim in historical_claims:
        past_id = str(past_claim.get("id", past_claim.get("claim_id", "")))

        # Skip self
        if past_id == current_id:
            continue

        score = 0.0
        reasons = []

        # Check description match
        past_desc = past_claim.get("description", "").lower().strip()
        if current_desc and past_desc and current_desc == past_desc:
            score += 0.4
            reasons.append("Identical description")

        # Check incident date match
        past_date = past_claim.get("incidentDate", past_claim.get("incident_date", ""))
        if current_date and past_date:
            # Compare date strings (ISO format)
            current_date_str = str(current_date)[:10]
            past_date_str = str(past_date)[:10]
            if current_date_str == past_date_str:
                score += 0.3
                reasons.append("Same incident date")

        # Check amount similarity (within 10%)
        past_amount = past_claim.get("claimAmount", past_claim.get("claim_amount", 0))
        if current_amount > 0 and past_amount > 0:
            amount_ratio = min(current_amount, past_amount) / max(current_amount, past_amount)
            if amount_ratio >= 0.9:
                score += 0.2
                reasons.append(f"Similar amount (ratio: {amount_ratio:.2f})")

        # Check location match
        past_location = past_claim.get("incidentLocation", past_claim.get("incident_location", "")).lower().strip()
        if current_location and past_location and current_location == past_location:
            score += 0.1
            reasons.append("Same location")

        if score >= 0.5:
            duplicates.append(
                {
                    "past_claim_id": past_id,
                    "similarity_score": round(score, 2),
                    "reasons": reasons,
                }
            )

    return {
        "is_duplicate": len(duplicates) > 0,
        "duplicates": duplicates,
        "duplicate_count": len(duplicates),
    }
