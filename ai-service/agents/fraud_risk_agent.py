"""
Fraud / Risk Assessment Agent

Responsibility:
    - Query claim history using controlled tools
    - Detect duplicates
    - Compare claim amounts
    - Identify suspicious patterns
    - Produce a structured risk score and flags
    - Recommend proceed or escalate

Requirements:
    - Validated tool inputs
    - Structured outputs (via Pydantic)
    - Least privilege (allow-listed tools only)
    - Error handling with safe failure
    - Auditable results
    - No chain-of-thought storage
    - Never has unrestricted database access
"""

import logging
from typing import List, Optional

from schemas.risk_result_schema import RiskAssessmentResult, RiskFlag, RecommendationType
from schemas.claim_schema import ClaimData
from tools.claim_tools import compare_claim_amount
from tools.claim_history_tools import (
    lookup_claim_history,
    analyze_claim_frequency,
    analyze_historical_patterns,
)
from tools.duplicate_claim_tools import detect_duplicate_claims

logger = logging.getLogger(__name__)

# ── Thresholds ────────────────────────────────────────────────
HIGH_AMOUNT_THRESHOLD = 50000.0
ESCALATION_SCORE_THRESHOLD = 70.0
DUPLICATE_SCORE_CONTRIBUTION = 35.0
HIGH_AMOUNT_SCORE_CONTRIBUTION = 25.0
FREQUENCY_SCORE_CONTRIBUTION = 15.0
PATTERN_SCORE_CONTRIBUTION = 15.0
DOCUMENT_MISMATCH_SCORE_CONTRIBUTION = 40.0
DUPLICATE_DOC_SCORE_CONTRIBUTION = 25.0
UNREADABLE_DOC_SCORE_CONTRIBUTION = 20.0


class FraudRiskAgent:
    """
    Agentic AI fraud/risk assessment agent.

    Uses controlled, allow-listed tool abstractions to analyze claims
    and produce structured risk assessment results.

    The agent NEVER has direct database access.
    All data is retrieved through tool abstractions that call the backend API.
    """

    def __init__(self, gemini_client_instance=None) -> None:
        self._allowed_tools = [
            "compare_claim_amount",
            "lookup_claim_history",
            "analyze_claim_frequency",
            "analyze_historical_patterns",
            "detect_duplicate_claims",
        ]
        if gemini_client_instance is not None:
            self._gemini = gemini_client_instance
        else:
            from services.gemini_client import gemini_client
            self._gemini = gemini_client

    async def assess(self, claim_data: ClaimData) -> RiskAssessmentResult:
        """
        Perform a comprehensive risk assessment on a claim.

        Steps:
        1. Compare claim amount against threshold
        2. Look up claim history for the policyholder
        3. Check for duplicate claims
        4. Analyze claim frequency
        5. Detect historical patterns
        6. Check deterministic document integrity signals
        7. Compute aggregate risk score
        8. Determine recommendation
        9. Optional Gemini Contextual Reasoning Layer
        """
        try:
            flags: List[RiskFlag] = []
            score_contributions: List[float] = []

            # ── Step 1: Amount comparison ────────────────────────
            amount_result = compare_claim_amount(
                claim_data.claim_amount, HIGH_AMOUNT_THRESHOLD
            )
            if amount_result["exceeds_threshold"]:
                severity = "Critical" if amount_result["ratio"] >= 2.0 else "High"
                flags.append(
                    RiskFlag(
                        flag_type="HighAmount",
                        description=(
                            f"Claim amount ${claim_data.claim_amount:,.2f} "
                            f"exceeds threshold of ${HIGH_AMOUNT_THRESHOLD:,.2f} "
                            f"(ratio: {amount_result['ratio']}x)"
                        ),
                        severity=severity,
                    )
                )
                contribution = HIGH_AMOUNT_SCORE_CONTRIBUTION * min(
                    amount_result["ratio"], 3.0
                )
                score_contributions.append(contribution)

            # ── Step 2: Claim history lookup ─────────────────────
            history = await lookup_claim_history(str(claim_data.policy_holder_id))

            # ── Step 3: Duplicate detection ──────────────────────
            if history:
                claim_dict = {
                    "claim_id": str(claim_data.claim_id),
                    "description": claim_data.description,
                    "incident_date": claim_data.incident_date.isoformat(),
                    "claim_amount": claim_data.claim_amount,
                    "incident_location": claim_data.incident_location,
                }
                dup_result = detect_duplicate_claims(claim_dict, history)
                if dup_result["is_duplicate"]:
                    for dup in dup_result["duplicates"][:3]:  # Limit to top 3
                        flags.append(
                            RiskFlag(
                                flag_type="DuplicateClaim",
                                description=(
                                    f"Potential duplicate of claim {dup['past_claim_id']} "
                                    f"(similarity: {dup['similarity_score']:.0%}). "
                                    f"Reasons: {', '.join(dup['reasons'])}"
                                ),
                                severity="Critical",
                            )
                        )
                    score_contributions.append(
                        DUPLICATE_SCORE_CONTRIBUTION * dup_result["duplicate_count"]
                    )

                # ── Step 4: Frequency analysis ───────────────────
                freq_result = analyze_claim_frequency(history)
                if freq_result["is_frequent"]:
                    flags.append(
                        RiskFlag(
                            flag_type="FrequentClaims",
                            description=(
                                f"Policyholder has submitted {freq_result['claim_count']} "
                                f"claims in the analyzed period"
                            ),
                            severity="Medium" if freq_result["claim_count"] <= 5 else "High",
                        )
                    )
                    score_contributions.append(FREQUENCY_SCORE_CONTRIBUTION)

                # ── Step 5: Historical patterns ──────────────────
                pattern_result = analyze_historical_patterns(history)
                if pattern_result["patterns_found"]:
                    for pattern in pattern_result["patterns"]:
                        flags.append(
                            RiskFlag(
                                flag_type="SuspiciousPattern",
                                description=pattern,
                                severity="High",
                            )
                        )
                    score_contributions.append(PATTERN_SCORE_CONTRIBUTION)

            # ── Step 6: Document verification signals ────────────
            # Ingest any passed document flags from backend
            for dflag in (claim_data.document_flags or []):
                if isinstance(dflag, str):
                    if ":" in dflag:
                        ftype, fdesc = dflag.split(":", 1)
                        ftype = ftype.strip()
                        fdesc = fdesc.strip()
                    else:
                        ftype = "DocumentTypeMismatch"
                        fdesc = dflag
                    fsev = "High"
                else:
                    ftype = dflag.get("flag_type", "DocumentTypeMismatch")
                    fdesc = dflag.get("description", "Document verification anomaly detected.")
                    fsev = dflag.get("severity", "High")

                flags.append(RiskFlag(flag_type=ftype, description=fdesc, severity=fsev))
                if ftype == "DocumentTypeMismatch":
                    score_contributions.append(DOCUMENT_MISMATCH_SCORE_CONTRIBUTION)
                elif ftype == "DuplicateDocumentReused":
                    score_contributions.append(DUPLICATE_DOC_SCORE_CONTRIBUTION)
                elif ftype == "DocumentUnreadable":
                    score_contributions.append(UNREADABLE_DOC_SCORE_CONTRIBUTION)
                else:
                    score_contributions.append(20.0)

            # Check documents directly if passed without pre-computed flags
            if not claim_data.document_flags and claim_data.documents:
                for doc in claim_data.documents:
                    vstatus = (doc.verification_status or "").lower()
                    if vstatus in ("mismatch", "rejected"):
                        flags.append(
                            RiskFlag(
                                flag_type="DocumentTypeMismatch",
                                description=f"Document '{doc.file_name}' for '{doc.document_type}' failed verification (status: {doc.verification_status}).",
                                severity="High",
                            )
                        )
                        score_contributions.append(DOCUMENT_MISMATCH_SCORE_CONTRIBUTION)
                    elif vstatus == "unreadable":
                        flags.append(
                            RiskFlag(
                                flag_type="DocumentUnreadable",
                                description=f"Document '{doc.file_name}' for '{doc.document_type}' is unreadable.",
                                severity="Medium",
                            )
                        )
                        score_contributions.append(UNREADABLE_DOC_SCORE_CONTRIBUTION)

            # ── Step 7: Compute aggregate score ──────────────────
            raw_score = sum(score_contributions)
            final_score = max(0.0, min(100.0, raw_score))

            # ── Step 8: Determine recommendation ─────────────────
            has_mismatch_flags = any(f.flag_type == "DocumentTypeMismatch" for f in flags)
            has_critical_flags = any(f.severity in ("Critical", "High") for f in flags)
            recommendation = (
                RecommendationType.ESCALATE
                if final_score >= ESCALATION_SCORE_THRESHOLD or (has_mismatch_flags and final_score >= 30.0) or (has_critical_flags and final_score >= 60.0)
                else RecommendationType.PROCEED
            )

            # ── Step 8: Gemini Contextual Reasoning Layer ────────
            ai_used = False
            ai_provider = None
            ai_model = None
            reasoning_summary = None
            fallback_used = False

            if self._gemini and self._gemini.is_available:
                try:
                    flag_summary = (
                        "; ".join(f"{f.flag_type} ({f.severity}): {f.description}" for f in flags)
                        if flags
                        else "No risk flags triggered"
                    )
                    prompt = (
                        f"Claim ID: {claim_data.claim_id}\n"
                        f"Claim Amount: ${claim_data.claim_amount:,.2f}\n"
                        f"Description: {claim_data.description or 'N/A'}\n"
                        f"Deterministic Risk Score: {round(final_score, 1)}/100\n"
                        f"Deterministic Recommendation: {recommendation.value.upper()}\n"
                        f"Triggered Risk Flags: {flag_summary}\n\n"
                        "Provide a concise, 2-3 sentence fraud analyst interpretation explaining why the risk score and flags "
                        "were assigned, interpreting any anomalies or patterns, and suggesting what the human adjuster should check. "
                        "Do not change or contradict the deterministic score or recommendation."
                    )
                    system_instruction = (
                        "You are an insurance fraud risk intelligence assistant. "
                        "You provide analytical reasoning to assist human claims adjusters. "
                        "Never override deterministic risk calculations or make final approval decisions."
                    )

                    explanation = await self._gemini.generate_text_async(
                        prompt=prompt,
                        system_instruction=system_instruction,
                        operation_name="fraud_risk_assessment",
                    )

                    if explanation:
                        ai_used = True
                        ai_provider = "gemini"
                        model = getattr(self._gemini, "model_name", "gemini-2.5-flash")
                        ai_model = model if isinstance(model, str) else "gemini-2.5-flash"
                        reasoning_summary = explanation
                    else:
                        fallback_used = True
                        logger.info(
                            "Risk assessment: Gemini reasoning unavailable for claim %s. "
                            "Using authoritative deterministic score=%.1f, rec=%s.",
                            claim_data.claim_id, round(final_score, 1), recommendation.value
                        )
                except Exception as exc:
                    fallback_used = True
                    logger.warning(
                        "Risk assessment: Gemini call threw unexpected %s for claim %s. "
                        "Using authoritative deterministic score=%.1f.",
                        type(exc).__name__, claim_data.claim_id, round(final_score, 1)
                    )
            else:
                fallback_used = True

            return RiskAssessmentResult(
                risk_score=round(final_score, 1),
                flags=flags,
                recommendation=recommendation,
                ai_used=ai_used,
                ai_provider=ai_provider,
                ai_model=ai_model,
                reasoning_summary=reasoning_summary,
                fallback_used=fallback_used,
            )

        except Exception as exc:
            # Safe failure: return a conservative result recommending escalation
            logger.error(
                "Fraud risk agent failed for claim %s: %s",
                claim_data.claim_id,
                exc,
                exc_info=True,
            )
            return RiskAssessmentResult(
                risk_score=50.0,
                flags=[
                    RiskFlag(
                        flag_type="SuspiciousPattern",
                        description="Risk assessment encountered an error; manual review recommended.",
                        severity="High",
                    )
                ],
                recommendation=RecommendationType.ESCALATE,
            )
