"""Tools for querying claim history for fraud/risk analysis.

These are controlled/allow-listed tool abstractions.
The AI agent uses these instead of direct database access.
"""

from typing import List, Optional
import httpx
import os
import logging

logger = logging.getLogger(__name__)

_BACKEND_BASE_URL = os.getenv("BACKEND_API_URL", "http://localhost:5000/api")
_TIMEOUT = 10.0


async def lookup_claim_history(policyholder_id: str) -> List[dict]:
    """
    Look up historical claims for a policyholder via the backend API.

    Args:
        policyholder_id: UUID string of the policyholder.

    Returns:
        List of historical claim summaries. Empty list on failure.
    """
    if not policyholder_id or not isinstance(policyholder_id, str):
        logger.warning("Invalid policyholder_id provided to lookup_claim_history")
        return []

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT) as client:
            response = await client.get(
                f"{_BACKEND_BASE_URL}/claims",
                params={"policyholderId": policyholder_id},
            )
            if response.status_code == 200:
                data = response.json()
                return data if isinstance(data, list) else []
            logger.warning(
                "Backend returned %d for policyholder %s history",
                response.status_code,
                policyholder_id,
            )
            return []
    except (httpx.RequestError, httpx.TimeoutException) as exc:
        logger.warning(
            "Failed to fetch claim history for %s: %s", policyholder_id, exc
        )
        return []


def analyze_claim_frequency(claims: List[dict], months: int = 12) -> dict:
    """
    Analyze the frequency of claims within a time window.

    Args:
        claims: List of historical claim dictionaries.
        months: Lookback period in months.

    Returns:
        Analysis result with claim count and frequency assessment.
    """
    if not claims:
        return {"total_claims": 0, "is_frequent": False, "claim_count": 0}

    claim_count = len(claims)
    return {
        "total_claims": claim_count,
        "is_frequent": claim_count > 3,
        "claim_count": claim_count,
        "months_analyzed": months,
    }


def analyze_historical_patterns(claims: List[dict]) -> dict:
    """
    Look for suspicious patterns in a policyholder's claim history.

    Args:
        claims: List of historical claim dictionaries.

    Returns:
        Pattern analysis result.
    """
    if not claims:
        return {"patterns_found": False, "patterns": []}

    patterns = []
    amounts = [c.get("claimAmount", 0) for c in claims if "claimAmount" in c]

    # Check for escalating amounts
    if len(amounts) >= 3:
        increasing = all(amounts[i] <= amounts[i + 1] for i in range(len(amounts) - 1))
        if increasing:
            patterns.append("Consistently increasing claim amounts")

    # Check for similar descriptions
    descriptions = [c.get("description", "").lower() for c in claims if c.get("description")]
    if len(descriptions) >= 2:
        unique_ratio = len(set(descriptions)) / len(descriptions)
        if unique_ratio < 0.5:
            patterns.append("Repetitive claim descriptions")

    return {
        "patterns_found": len(patterns) > 0,
        "patterns": patterns,
        "total_claims_analyzed": len(claims),
    }
