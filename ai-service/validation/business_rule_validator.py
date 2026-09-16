"""
Business rule validation for insurance payout processing.

These are deterministic validators used as controlled tools by the
Validation / Safety Agent. Each function validates a specific business rule
and returns a violation message if the rule is violated, or None if it passes.
"""

from typing import Optional


def validate_coverage_limit(
    approved_claim_amount: float,
    coverage_limit: float,
    proposed_payout: float,
) -> Optional[str]:
    """
    Coverage-limit rule: proposed payout must not exceed coverage limit.
    """
    if proposed_payout > coverage_limit:
        return (
            f"COVERAGE_VIOLATION: Proposed payout ({proposed_payout:.2f}) "
            f"exceeds coverage limit ({coverage_limit:.2f})."
        )
    return None


def validate_deductible_correctness(
    approved_claim_amount: float,
    coverage_limit: float,
    deductible: float,
    proposed_payout: float,
) -> Optional[str]:
    """
    Deductible rule: proposed payout must equal max(0, min(claim, coverage) - deductible).
    """
    eligible = min(approved_claim_amount, coverage_limit)
    expected = max(0.0, eligible - deductible)

    if abs(proposed_payout - expected) > 0.01:
        return (
            f"DEDUCTIBLE_ERROR: Proposed payout ({proposed_payout:.2f}) does not match "
            f"expected calculation ({expected:.2f}). "
            f"Expected: max(0, min({approved_claim_amount:.2f}, {coverage_limit:.2f}) "
            f"- {deductible:.2f}) = {expected:.2f}."
        )
    return None


def validate_non_negative_payout(proposed_payout: float) -> Optional[str]:
    """
    Non-negative rule: payout must never be negative.
    """
    if proposed_payout < 0:
        return f"NEGATIVE_PAYOUT: Proposed payout ({proposed_payout:.2f}) is negative."
    return None


def validate_non_negative_amounts(
    approved_claim_amount: float,
    coverage_limit: float,
    deductible: float,
) -> Optional[str]:
    """
    All input amounts must be non-negative.
    """
    errors = []
    if approved_claim_amount < 0:
        errors.append(f"approved_claim_amount ({approved_claim_amount:.2f}) is negative")
    if coverage_limit < 0:
        errors.append(f"coverage_limit ({coverage_limit:.2f}) is negative")
    if deductible < 0:
        errors.append(f"deductible ({deductible:.2f}) is negative")

    if errors:
        return f"NEGATIVE_INPUT: {'; '.join(errors)}."
    return None


def validate_claim_policy_compatibility(
    claim_type: str,
    policy_type: str,
) -> Optional[str]:
    """
    Claim type must be compatible with policy type.
    Currently a placeholder that validates required fields are present.
    Full compatibility matrix will be added during integration with
    Kaushikesh's Policy module.
    """
    if not claim_type or not claim_type.strip():
        return "MISSING_CLAIM_TYPE: Claim type is empty or missing."
    if not policy_type or not policy_type.strip():
        return "MISSING_POLICY_TYPE: Policy type is empty or missing."
    return None


def validate_required_data(
    claim_id: str,
    approved_claim_amount: Optional[float],
    coverage_limit: Optional[float],
    deductible: Optional[float],
) -> Optional[str]:
    """
    All required workflow data must be present.
    """
    missing = []
    if not claim_id:
        missing.append("claim_id")
    if approved_claim_amount is None:
        missing.append("approved_claim_amount")
    if coverage_limit is None:
        missing.append("coverage_limit")
    if deductible is None:
        missing.append("deductible")

    if missing:
        return f"MISSING_REQUIRED_DATA: {', '.join(missing)}."
    return None
