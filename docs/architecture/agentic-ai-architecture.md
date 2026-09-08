# Agentic AI Architecture

## Status
Draft — to be completed during implementation.

## Overview

The system employs four distinct AI agents orchestrated in a pipeline for claim processing.

## Agents

### Agent 1: Coordinator / Planning Agent
- Receives claim-processing objective
- Creates structured multi-step plan
- Delegates tasks to specialist agents

### Agent 2: Document Verification Agent
- Checks required claim documents
- Validates completeness
- Identifies missing items and inconsistencies

### Agent 3: Fraud / Risk Assessment Agent
- Queries claim history
- Detects duplicates and suspicious patterns
- Produces structured risk score and flags

### Agent 4: Validation / Safety Agent
- Performs deterministic validation
- Ensures policy coverage compliance
- Enforces human approval for high-impact actions

## Workflow Pipeline

```
Claim Submitted
    → Coordinator Agent (creates plan)
    → Document Verification Agent
    → Fraud / Risk Assessment Agent
    → Validation / Safety Agent
    → Pending Human Approval
    → Decision Recorded
```

## Service Boundary

The AI service is INTERNAL ONLY:
- React → ASP.NET Core → AI Service ✓
- Flutter → ASP.NET Core → AI Service ✓
- React → AI Service ✗
- Flutter → AI Service ✗

## State Management

<!-- TODO: Document workflow state persistence strategy -->

## Tools & Schemas

<!-- TODO: Document available tools and their permissions -->
