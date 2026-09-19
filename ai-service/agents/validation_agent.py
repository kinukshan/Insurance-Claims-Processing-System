"""
Validation / Safety Agent

Responsibility:
    Validate proposed payout results before high-impact payout actions.

    This is a distinct agent with:
    - Identifiable Validation/Safety Agent responsibility
    - Structured input contract (PayoutValidationRequest)
    - Structured output contract (PayoutValidationResult)
    - Controlled tool/validator access (deterministic business rule validators)
    - Visible execution result
    - Safe-failure behaviour
    - Testable participation in the future Agentic workflow

    Deterministic validators exist underneath the agent as controlled tools.
    Only structured validation results and auditable summaries are persisted.
    No chain-of-thought is stored.
"""

from datetime import datetime
from typing import List

from schemas.payout_result_schema import (
    PayoutValidationRequest,
    PayoutValidationResult,
)
from validation.business_rule_validator import (
    validate_coverage_limit,
    validate_deductible_correctness,
    validate_non_negative_payout,
    validate_non_negative_amounts,
    validate_claim_policy_compatibility,
    validate_required_data,
)
from validation.schema_validator import validate_payout_request_schema


class ValidationSafetyAgent:
    """
    Validation / Safety Agent for payout proposal validation.

    Executes a suite of deterministic validation tools against a payout proposal
    and returns a structured result indicating validity, violations, and whether
    human approval is required.

    The AI must NEVER automatically execute a real payment.
    Unsafe proposals must fail safely or return for revision.
    """

    AGENT_ID = "validation-safety-agent"

    def __init__(self, gemini_client_instance=None):
        if gemini_client_instance is not None:
            self._gemini = gemini_client_instance
        else:
            from services.gemini_client import gemini_client
            self._gemini = gemini_client

    def validate_payout_proposal(
        self, request: PayoutValidationRequest
    ) -> PayoutValidationResult:
        """
        Validate a payout proposal using all available deterministic validators.

        Args:
            request: The validated payout proposal request.

        Returns:
            Structured validation result with violations and approval requirement.
        """
        violations: List[str] = []

        # Tool 1: Validate required data completeness
        result = validate_required_data(
            claim_id=request.claim_id,
            approved_claim_amount=request.approved_claim_amount,
            coverage_limit=request.coverage_limit,
            deductible=request.deductible,
        )
        if result:
            violations.append(result)

        # Tool 2: Validate non-negative input amounts
        result = validate_non_negative_amounts(
            approved_claim_amount=request.approved_claim_amount,
            coverage_limit=request.coverage_limit,
            deductible=request.deductible,
        )
        if result:
            violations.append(result)

        # Tool 3: Validate claim type / policy type compatibility
        result = validate_claim_policy_compatibility(
            claim_type=request.claim_type,
            policy_type=request.policy_type,
        )
        if result:
            violations.append(result)

        # Tool 4: Validate coverage limit rule
        result = validate_coverage_limit(
            approved_claim_amount=request.approved_claim_amount,
            coverage_limit=request.coverage_limit,
            proposed_payout=request.proposed_payout,
        )
        if result:
            violations.append(result)

        # Tool 5: Validate deductible correctness
        result = validate_deductible_correctness(
            approved_claim_amount=request.approved_claim_amount,
            coverage_limit=request.coverage_limit,
            deductible=request.deductible,
            proposed_payout=request.proposed_payout,
        )
        if result:
            violations.append(result)

        # Tool 6: Validate non-negative payout
        result = validate_non_negative_payout(request.proposed_payout)
        if result:
            violations.append(result)

        is_valid = len(violations) == 0

        # Build auditable summary (no chain-of-thought)
        summary = (
            f"Validation completed for claim {request.claim_id}. "
            f"{'PASSED' if is_valid else 'FAILED'} with {len(violations)} violation(s)."
        )

        # Gemini contextual safety analysis
        ai_used = False
        ai_provider = None
        ai_model = None
        reasoning_summary = None
        fallback_used = False

        if self._gemini and self._gemini.is_available:
            try:
                prompt = (
                    f"Claim ID: {request.claim_id}\n"
                    f"Policy Type: {request.policy_type}\n"
                    f"Claim Type: {request.claim_type}\n"
                    f"Approved Amount: ${request.approved_claim_amount:,.2f}\n"
                    f"Coverage Limit: ${request.coverage_limit:,.2f}\n"
                    f"Deductible: ${request.deductible:,.2f}\n"
                    f"Proposed Payout: ${request.proposed_payout:,.2f}\n"
                    f"Deterministic Validation: {'PASSED' if is_valid else 'FAILED'}\n"
                    f"Violations: {'; '.join(violations) if violations else 'None'}\n\n"
                    "Provide a concise 2-sentence financial safety summary explaining why this payout proposal is valid "
                    "or why specific policy rules were violated. Reiterate that human supervisor approval is required."
                )
                system_instruction = (
                    "You are an insurance payout validation and financial safety assistant. "
                    "Never approve payouts, change payout amounts, or override policy limits. "
                    "Always respect deterministic rule outcomes."
                )
                explanation = self._gemini.generate_text(
                    prompt=prompt,
                    system_instruction=system_instruction,
                )
                if explanation:
                    ai_used = True
                    ai_provider = "gemini"
                    model = getattr(self._gemini, "model_name", "gemini-2.5-flash")
                    ai_model = model if isinstance(model, str) else "gemini-2.5-flash"
                    reasoning_summary = explanation
                else:
                    fallback_used = True
            except Exception:
                fallback_used = True
        else:
            fallback_used = True

        return PayoutValidationResult(
            valid=is_valid,
            violations=violations,
            requires_human_approval=True,  # All payouts strictly require human approval
            agent_id=self.AGENT_ID,
            timestamp=datetime.utcnow(),
            summary=summary,
            ai_used=ai_used,
            ai_provider=ai_provider,
            ai_model=ai_model,
            reasoning_summary=reasoning_summary,
            fallback_used=fallback_used,
        )

    def validate_from_dict(self, data: dict) -> PayoutValidationResult:
        """
        Safe entry point that validates schema first, then runs business rules.
        Returns safe failure result if schema validation fails.
        """
        # Schema validation
        schema_valid, schema_errors = validate_payout_request_schema(data)
        if not schema_valid:
            return PayoutValidationResult(
                valid=False,
                violations=schema_errors,
                requires_human_approval=True,
                agent_id=self.AGENT_ID,
                timestamp=datetime.utcnow(),
                summary=f"Schema validation failed with {len(schema_errors)} error(s).",
            )

        try:
            request = PayoutValidationRequest(**data)
            return self.validate_payout_proposal(request)
        except Exception as e:
            # Safe failure: return invalid result rather than crashing
            return PayoutValidationResult(
                valid=False,
                violations=[f"AGENT_ERROR: Unexpected error during validation: {str(e)}"],
                requires_human_approval=True,
                agent_id=self.AGENT_ID,
                timestamp=datetime.utcnow(),
                summary="Validation agent encountered an unexpected error. Failing safely.",
            )


# Module-level agent instance
validation_agent = ValidationSafetyAgent()
