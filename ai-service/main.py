"""
Insurance Claims Processing System — Agentic AI Service

This is an INTERNAL service. It is never called directly by React or Flutter.
All requests are routed through ASP.NET Core Web API.

Architecture:
    React -> ASP.NET Core -> AI Service
    Flutter -> ASP.NET Core -> AI Service
"""

from fastapi import FastAPI, HTTPException

from agents.coordinator_agent import CoordinatorAgent
from agents.validation_agent import validation_agent
from schemas.workflow_schema import WorkflowObjective, WorkflowPlan, WorkflowResult
from schemas.payout_result_schema import PayoutValidationRequest
from state.workflow_state import workflow_store

app = FastAPI(
    title="Insurance Claims AI Service",
    description="Internal agentic AI service for claim processing workflows",
    version="0.1.0",
)

coordinator = CoordinatorAgent()


@app.get("/health")
async def health_check():
    """Health check endpoint for container orchestration."""
    return {"status": "healthy", "service": "ai-service"}


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