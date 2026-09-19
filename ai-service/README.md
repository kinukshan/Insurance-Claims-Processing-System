# Insurance Claims AI Service — Hybrid Agentic AI with Google Gemini

Internal agentic AI microservice supporting the Insurance Claims Processing System.

## Architecture

```
React Frontend
      │
      ▼
ASP.NET Core Web API (Port 5097)
      │
      ▼
Python FastAPI AI Service (Port 8000)
      │
      ├── Deterministic Rules & Checklists (Authoritative)
      │   ├── Coverage & Deductible Rules
      │   ├── Mandatory Document Checklists
      │   ├── Fraud Risk Scoring Algorithm
      │   └── Financial Safety Validation
      │
      └── Google Gemini LLM Reasoning (Contextual Support)
          ├── Document Completeness Interpretation
          ├── Fraud Anomaly & Pattern Explanation
          ├── Workflow Coordination Summaries
          └── Reviewer-Friendly Explanations
      │
      ▼
Human-in-the-Loop Review (Claims Adjuster / Supervisor)
```

> **Important**: Gemini reasoning never replaces or overrides deterministic financial limits, fraud scores, mandatory checklist items, or human approval requirements.

---

## Safe Local Setup

### 1. Create a Gemini API Key
Obtain an API key from [Google AI Studio](https://aistudio.google.com/).

### 2. Configure Environment Variables
Set the API key in your terminal session. **Never commit API keys to Git.**

```bash
export GEMINI_API_KEY="your-gemini-api-key-here"

# Optional: override the default model (defaults to gemini-2.5-flash)
export GEMINI_MODEL="gemini-2.5-flash"
```

> **Fallback Mode**: If `GEMINI_API_KEY` is not provided, the service will start and operate in **deterministic fallback mode**. All business rules, checklists, risk scoring, and validations continue working without disruption.

### 3. Activate Python Virtual Environment

```bash
cd ai-service
source .venv/bin/activate
```

### 4. Install Dependencies

```bash
python -m pip install -r requirements.txt
```

### 5. Start the AI Service

```bash
python -m uvicorn main:app --reload --port 8000
```

### 6. Verify Health & AI Readiness

```bash
# General health check (reports LLM status without leaking secrets)
curl http://127.0.0.1:8000/health

# Diagnostic Gemini connectivity check
curl http://127.0.0.1:8000/health/gemini
```

Expected `/health` output:
```json
{
  "status": "healthy",
  "service": "ai-service",
  "llm_provider": "gemini",
  "llm_configured": true,
  "llm_model": "gemini-2.5-flash"
}
```

---

## Running Tests

### Unit Tests (All Mocked — No API Key Needed)

```bash
source .venv/bin/activate
python -m pytest
```

### Optional Live Gemini Integration Test
To execute a live ping against the Gemini API (requires `GEMINI_API_KEY` set):

```bash
source .venv/bin/activate
python -m pytest -m live_ai
```

---

## API Endpoints (Internal Only — Called via ASP.NET Core)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/health` | Health & AI provider readiness check |
| `GET` | `/health/gemini` | Diagnostic Gemini connectivity check |
| `POST` | `/api/workflows/claim-processing` | Coordinator agent pipeline execution |
| `GET` | `/api/workflows/{id}/status` | Workflow status query |
| `POST` | `/api/agents/document-verification` | Document checklist & consistency verification |
| `POST` | `/api/fraud-risk/assess` | Fraud risk scoring & anomaly assessment |
| `POST` | `/api/validate/payout` | Financial safety validation (`requires_human_approval=True`) |
