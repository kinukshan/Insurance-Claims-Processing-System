"""Tools for accessing and validating claim data.

These are controlled/allow-listed tool abstractions.
The AI agent uses these instead of direct database access.
"""

from typing import Optional
import httpx
import os
import logging

logger = logging.getLogger(__name__)

# Internal backend API base URL — never exposed externally
_BACKEND_BASE_URL = os.getenv("BACKEND_API_URL", "http://localhost:5000/api")
_TIMEOUT = 10.0


async def get_claim_details(claim_id: str) -> Optional[dict]:
    """
    Fetch claim details from the backend API.

    Args:
        claim_id: UUID string of the claim.

    Returns:
        Claim data dictionary or None if unavailable.
    """
    if not claim_id or not isinstance(claim_id, str):
        logger.warning("Invalid claim_id provided to get_claim_details")
        return None

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT) as client:
            response = await client.get(f"{_BACKEND_BASE_URL}/claims/{claim_id}")
            if response.status_code == 200:
                return response.json()
            logger.warning(
                "Backend returned %d for claim %s", response.status_code, claim_id
            )
            return None
    except (httpx.RequestError, httpx.TimeoutException) as exc:
        logger.warning("Failed to fetch claim %s: %s", claim_id, exc)
        return None


def compare_claim_amount(claim_amount: float, threshold: float = 50000.0) -> dict:
    """
    Compare a claim amount against a threshold.

    Args:
        claim_amount: The amount being claimed.
        threshold: The amount threshold for flagging.

    Returns:
        Dictionary with comparison result.
    """
    if not isinstance(claim_amount, (int, float)) or claim_amount < 0:
        return {
            "exceeds_threshold": False,
            "amount": 0,
            "threshold": threshold,
            "ratio": 0.0,
        }

    ratio = claim_amount / threshold if threshold > 0 else 0.0
    return {
        "exceeds_threshold": claim_amount > threshold,
        "amount": claim_amount,
        "threshold": threshold,
        "ratio": round(ratio, 2),
    }
