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

from agents.coordinator_agent import CoordinatorAgent
from agents.validation_agent import validation_agent
from agents.document_verification_agent import DocumentVerificationAgent
from agents.fraud_risk_agent import FraudRiskAgent
from schemas.workflow_schema import WorkflowObjective, WorkflowPlan, WorkflowResult
from schemas.claim_schema import ClaimData, DocumentVerificationRequest
from schemas.document_result_schema import DocumentVerificationResult
from schemas.risk_result_schema import RiskAssessmentResult
from schemas.payout_result_schema import PayoutValidationRequest
from state.workflow_state import workflow_store

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Insurance Claims AI Service",
    description="Internal agentic AI service for claim processing workflows",
    version="0.1.0",
)

# Initialize agents
coordinator = CoordinatorAgent()
_document_agent = DocumentVerificationAgent()


@app.get("/health")
async def health_check():
    """Health check endpoint for container orchestration."""
    return {"status": "healthy", "service": "ai-service"}


# ── Coordinator / Planning Agent (Kaushikesh) ────────────────────────

@app.post("/api/workflows/claim-processing", response_model=WorkflowResult)
async def start_claim_processing(objective: WorkflowObjective):
    """
    Start a claim processing workflow.
    The coordinator agent creates a plan and executes the full pipeline.
    """
    try:
        result = coordinator.execute_workflow(objective)
        return result
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Workflow execution failed: {str(e)}"
        )


@app.get("/api/workflows/{workflow_id}/status")
async def get_workflow_status(workflow_id: str):
    """Get the current status of a workflow."""
    status = coordinator.get_workflow_status(workflow_id)

    if status is None:
        raise HTTPException(
            status_code=404,
            detail=f"Workflow '{workflow_id}' not found."
        )

    return status


# ── Validation / Safety Agent (Kinukshan) ────────────────────────────

@app.post("/api/validate/payout")
async def validate_payout(request: PayoutValidationRequest):
    """
    Validate a payout proposal via the Validation / Safety Agent.

    Called internally by ASP.NET Core (IPayoutValidationAgentGateway).
    Never called directly by React or Flutter.
    """
    try:
        result = validation_agent.validate_payout_proposal(request)
        return result.model_dump()
    except Exception as e:
        # Safe failure: return validation failure rather than 500
        return {
            "valid": False,
            "violations": [f"AGENT_ERROR: {str(e)}"],
            "requires_human_approval": True,
            "agent_id": "validation-safety-agent-error",
            "summary": "Validation agent encountered an error. Failing safely.",
        }


# ── Document Verification Agent (Arulkumaran) ───────────────────────

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


# ── Fraud / Risk Assessment Agent (Jathusha) ────────────────────────

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
