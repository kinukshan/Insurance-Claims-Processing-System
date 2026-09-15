"""
Insurance Claims Processing System — Agentic AI Service

This is an INTERNAL service. It is never called directly by React or Flutter.
All requests are routed through ASP.NET Core Web API.

Architecture:
    React -> ASP.NET Core -> AI Service
    Flutter -> ASP.NET Core -> AI Service
"""

from fastapi import FastAPI, HTTPException
from schemas.claim_schema import DocumentVerificationRequest
from schemas.document_result_schema import DocumentVerificationResult
from agents.document_verification_agent import DocumentVerificationAgent

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


# TODO: Add workflow endpoints once other agents are implemented
# POST /api/workflows/claim-processing — Start a claim processing workflow
# GET  /api/workflows/{workflow_id}/status — Get workflow status

