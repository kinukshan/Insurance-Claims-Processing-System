"""
Insurance Claims Processing System — Agentic AI Service

This is an INTERNAL service. It is never called directly by React or Flutter.
All requests are routed through ASP.NET Core Web API.

Architecture:
    React -> ASP.NET Core -> AI Service
    Flutter -> ASP.NET Core -> AI Service
"""

import logging
from fastapi import FastAPI, HTTPException
from schemas.claim_schema import ClaimData, DocumentVerificationRequest
from schemas.document_result_schema import DocumentVerificationResult
from schemas.risk_result_schema import RiskAssessmentResult
from schemas.payout_result_schema import PayoutValidationRequest
from agents.document_verification_agent import DocumentVerificationAgent
from agents.fraud_risk_agent import FraudRiskAgent
from agents.validation_agent import validation_agent

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Insurance Claims AI Service",
    description="Internal agentic AI service for claim processing workflows",
    version="0.1.0",
)

# Initialize agents
_document_agent = DocumentVerificationAgent()


@app.get("/health")
async def health_check():
    """Health check endpoint for container orchestration."""
    return {"status": "healthy", "service": "ai-service"}


@app.post("/api/agents/document-verification", response_model=DocumentVerificationResult)
async def verify_documents(request: DocumentVerificationRequest):
    """
    Document Verification Agent endpoint.
    Checks submitted documents against required checklists and
    identifies missing items and inconsistencies.
    """
    try:
        result = _document_agent.verify(request)
        return result
    except Exception as e:
        # Safe failure — return structured error, never crash the service
        return DocumentVerificationResult(
            complete=False,
            missing_items=[],
            inconsistencies=[],
            warnings=[f"Agent error: {str(e)}"],
        )


@app.post("/api/fraud-risk/assess", response_model=RiskAssessmentResult)
async def assess_fraud_risk(claim_data: ClaimData) -> RiskAssessmentResult:
    """
    Run the fraud/risk assessment agent on a claim.

    Called by ASP.NET Core backend — never by React or Flutter directly.

    Returns a structured risk assessment with:
    - risk_score (0-100)
    - flags (list of identified risk indicators)
    - recommendation (proceed or escalate)
    """
    logger.info("Assessing fraud risk for claim %s", claim_data.claim_id)

    try:
        agent = FraudRiskAgent()
        result = await agent.assess(claim_data)

        logger.info(
            "Assessment complete for claim %s: score=%.1f, flags=%d, recommendation=%s",
            claim_data.claim_id,
            result.risk_score,
            len(result.flags),
            result.recommendation.value,
        )

        return result
    except Exception as exc:
        logger.error(
            "Unhandled error in fraud risk assessment for claim %s: %s",
            claim_data.claim_id,
            exc,
            exc_info=True,
        )
        # Return a safe fallback instead of 500
        return RiskAssessmentResult(
            risk_score=50.0,
            flags=[],
            recommendation="escalate",
        )


# TODO: Add workflow endpoints once other agents are implemented
# POST /api/workflows/claim-processing — Start a claim processing workflow
# GET  /api/workflows/{workflow_id}/status — Get workflow status
