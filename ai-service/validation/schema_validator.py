"""
Schema validation for agent inputs and outputs.

Validates that payout proposal data conforms to the expected schema
before processing by the Validation / Safety Agent.
"""

from typing import List, Tuple

from schemas.payout_result_schema import PayoutValidationRequest
from pydantic import ValidationError


def validate_payout_request_schema(data: dict) -> Tuple[bool, List[str]]:
    """
    Validate that the incoming request data conforms to the
    PayoutValidationRequest Pydantic schema.

    Returns:
        (is_valid, list_of_errors)
    """
    try:
        PayoutValidationRequest(**data)
        return True, []
    except (ValidationError, TypeError) as e:
        errors = []
        if isinstance(e, ValidationError):
            for err in e.errors():
                field = " -> ".join(str(loc) for loc in err["loc"])
                errors.append(f"SCHEMA_ERROR[{field}]: {err['msg']}")
        else:
            errors.append(f"SCHEMA_ERROR: {str(e)}")
        return False, errors


def validate_output_schema(result: dict) -> Tuple[bool, List[str]]:
    """
    Validate that the output result conforms to the expected response schema.
    Ensures 'valid', 'violations', and 'requires_human_approval' are present.
    """
    errors = []

    if "valid" not in result:
        errors.append("SCHEMA_ERROR: Missing 'valid' field in output.")
    elif not isinstance(result["valid"], bool):
        errors.append("SCHEMA_ERROR: 'valid' must be a boolean.")

    if "violations" not in result:
        errors.append("SCHEMA_ERROR: Missing 'violations' field in output.")
    elif not isinstance(result["violations"], list):
        errors.append("SCHEMA_ERROR: 'violations' must be a list.")

    if "requires_human_approval" not in result:
        errors.append("SCHEMA_ERROR: Missing 'requires_human_approval' field in output.")

    return len(errors) == 0, errors
