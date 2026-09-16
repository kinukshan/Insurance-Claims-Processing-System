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
from schemas.workflow_schema import WorkflowObjective, WorkflowPlan, WorkflowResult
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
        raise HTTPException(status_code=500, detail=f"Workflow execution failed: {str(e)}")


@app.get("/api/workflows/{workflow_id}/status")
async def get_workflow_status(workflow_id: str):
    """Get the current status of a workflow."""
    status = coordinator.get_workflow_status(workflow_id)
    if status is None:
        raise HTTPException(status_code=404, detail=f"Workflow '{workflow_id}' not found.")
    return status
