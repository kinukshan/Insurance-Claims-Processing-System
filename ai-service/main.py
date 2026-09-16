"""
Insurance Claims Processing System — Agentic AI Service

This is an INTERNAL service. It is never called directly by React or Flutter.
All requests are routed through ASP.NET Core Web API.

Architecture:
    React -> ASP.NET Core -> AI Service
    Flutter -> ASP.NET Core -> AI Service
"""

from fastapi import FastAPI, HTTPException

from agents.validation_agent import validation_agent
from schemas.payout_result_schema import PayoutValidationRequest

app = FastAPI(
    title="Insurance Claims AI Service",
    description="Internal agentic AI service for claim processing workflows",
    version="0.1.0",
)


@app.get("/health")
async def health_check():
    """Health check endpoint for container orchestration."""
    return {"status": "healthy", "service": "ai-service"}


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


# TODO: Add workflow endpoints once agents are implemented
# POST /api/workflows/claim-processing — Start a claim processing workflow
# GET  /api/workflows/{workflow_id}/status — Get workflow status
