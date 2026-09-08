# Team Contribution Matrix

## Member Ownership

### Member 1
| Area | Primary Folders |
|------|----------------|
| **Component A** — Policy Management | `backend/src/InsuranceClaims.Application/PolicyManagement/` |
| | `backend/src/InsuranceClaims.Domain/PolicyManagement/` |
| | `frontend-web/src/pages/policy/` |
| | `frontend-web/src/components/policy/` |
| | `frontend-web/src/services/policyService.js` |
| | `mobile/lib/screens/policy/` |
| | `mobile/lib/models/policy.dart` |
| | `mobile/lib/services/policy_service.dart` |
| **AI** — Coordinator / Planning Agent | `ai-service/agents/coordinator_agent.py` |
| **Tests** | `backend/tests/InsuranceClaims.UnitTests/PolicyManagement/` |
| | `frontend-web/src/__tests__/policy/` |
| | `mobile/test/policy/` |
| | `ai-service/tests/test_coordinator_agent.py` |

---

### Member 2
| Area | Primary Folders |
|------|----------------|
| **Component B** — Claims Submission & Document Verification | `backend/src/InsuranceClaims.Application/ClaimsManagement/` |
| | `backend/src/InsuranceClaims.Domain/ClaimsManagement/` |
| | `frontend-web/src/pages/claims/` |
| | `frontend-web/src/components/claims/` |
| | `frontend-web/src/services/claimService.js` |
| | `mobile/lib/screens/claims/` |
| | `mobile/lib/models/claim.dart` |
| | `mobile/lib/models/claim_document.dart` |
| | `mobile/lib/services/claim_service.dart` |
| **AI** — Document Verification Agent | `ai-service/agents/document_verification_agent.py` |
| **Tests** | `backend/tests/InsuranceClaims.UnitTests/ClaimsManagement/` |
| | `frontend-web/src/__tests__/claims/` |
| | `mobile/test/claims/` |
| | `ai-service/tests/test_document_verification_agent.py` |

---

### Member 3
| Area | Primary Folders |
|------|----------------|
| **Component C** — Risk Assessment / Fraud Flagging | `backend/src/InsuranceClaims.Application/RiskAssessment/` |
| | `backend/src/InsuranceClaims.Domain/RiskAssessment/` |
| | `frontend-web/src/pages/risk/` |
| | `frontend-web/src/components/risk/` |
| | `frontend-web/src/services/riskService.js` |
| | `mobile/lib/screens/risk/` |
| | `mobile/lib/models/risk_assessment.dart` |
| | `mobile/lib/services/risk_service.dart` |
| **AI** — Fraud / Risk Assessment Agent | `ai-service/agents/fraud_risk_agent.py` |
| **Tests** | `backend/tests/InsuranceClaims.UnitTests/RiskAssessment/` |
| | `frontend-web/src/__tests__/risk/` |
| | `mobile/test/risk/` |
| | `ai-service/tests/test_fraud_risk_agent.py` |

---

### Member 4
| Area | Primary Folders |
|------|----------------|
| **Component D** — Payout Processing | `backend/src/InsuranceClaims.Application/PayoutProcessing/` |
| | `backend/src/InsuranceClaims.Domain/PayoutProcessing/` |
| | `frontend-web/src/pages/payout/` |
| | `frontend-web/src/components/payout/` |
| | `frontend-web/src/services/payoutService.js` |
| | `mobile/lib/screens/payout/` |
| | `mobile/lib/models/payout.dart` |
| | `mobile/lib/services/payout_service.dart` |
| **AI** — Validation / Safety Agent | `ai-service/agents/validation_agent.py` |
| **Tests** | `backend/tests/InsuranceClaims.UnitTests/PayoutProcessing/` |
| | `frontend-web/src/__tests__/payout/` |
| | `mobile/test/payout/` |
| | `ai-service/tests/test_validation_agent.py` |

---

## Shared Infrastructure Files

> **⚠️ Modify with care — coordinate with the team.**

| File | Purpose |
|------|---------|
| `backend/src/InsuranceClaims.Api/Program.cs` | API startup and DI registration |
| `backend/src/InsuranceClaims.Infrastructure/Persistence/ApplicationDbContext.cs` | EF Core DbContext |
| `backend/src/InsuranceClaims.Infrastructure/DependencyInjection.cs` | Service registration |
| `backend/src/InsuranceClaims.Api/Controllers/AuthController.cs` | Shared authentication |
| `backend/src/InsuranceClaims.Api/Controllers/AgentWorkflowsController.cs` | Shared workflows |
| `backend/src/InsuranceClaims.Application/AgentWorkflows/` | Shared workflow logic |
| `backend/src/InsuranceClaims.Domain/AgentWorkflows/` | Shared workflow entities |
| `backend/src/InsuranceClaims.Domain/Users/` | Shared user entities |
| `frontend-web/src/App.jsx` | React app shell |
| `frontend-web/src/main.jsx` | React entry point |
| `mobile/lib/main.dart` | Flutter entry point |
| `docker-compose.yml` | Container orchestration |
| `README.md` | Project documentation |
| `.github/workflows/backend-ci.yml` | CI pipeline |
