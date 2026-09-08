"""
Insurance Claims Processing System — Agentic AI Service

This is an INTERNAL service. It is never called directly by React or Flutter.
All requests are routed through ASP.NET Core Web API.

Architecture:
    React -> ASP.NET Core -> AI Service
    Flutter -> ASP.NET Core -> AI Service
"""

from fastapi import FastAPI

app = FastAPI(
    title="Insurance Claims AI Service",
    description="Internal agentic AI service for claim processing workflows",
    version="0.1.0",
)


@app.get("/health")
async def health_check():
    """Health check endpoint for container orchestration."""
    return {"status": "healthy", "service": "ai-service"}


# TODO: Add workflow endpoints once agents are implemented
# POST /api/workflows/claim-processing — Start a claim processing workflow
# GET  /api/workflows/{workflow_id}/status — Get workflow status
