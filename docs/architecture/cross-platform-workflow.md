# Cross-Platform Workflow

## Status
Draft — to be completed during implementation.

## End-to-End Flow

```
Flutter (Policyholder submits claim)
    → ASP.NET Core (validates & persists)
    → PostgreSQL (saves claim)
    → ASP.NET Core (starts AI workflow)
    → Coordinator Agent
    → Document Verification Agent
    → Fraud / Risk Assessment Agent
    → Validation / Safety Agent
    → Pending Human Approval
    → React (Adjuster: Approve / Reject / Request Revision)
    → ASP.NET Core (records decision)
    → Payout Processing
    → PostgreSQL (updated)
    → Flutter (displays updated status)
```

## Platform Responsibilities

### Flutter (Policyholder)
- Submit claims
- Upload evidence
- View claim status
- View payout status

### React (Staff)
- Review claims
- Assess risk
- Approve/reject/revise payouts
- Monitor AI workflows

### ASP.NET Core (Backend)
- API gateway
- Business logic
- AI workflow orchestration
- Data persistence

## Data Flow Details

<!-- TODO: Document detailed API contracts and data flows -->
