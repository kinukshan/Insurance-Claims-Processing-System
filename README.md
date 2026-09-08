# Insurance Claims Processing System

> **SE3090 — Software Engineering Frameworks**
> Assignment 1 — Integrated Full-Stack and Agentic AI Application Development

## Overview

A cross-platform insurance claims processing system with agentic AI capabilities. Policyholders submit and track claims via a Flutter mobile app, while claims adjusters and underwriters manage reviews, risk assessments, and payouts through a React web dashboard. An agentic AI workflow automates document verification, fraud/risk assessment, and validation before human approval.

## Architecture

```
React (Staff) ────────┐
                       ├──► ASP.NET Core Web API ──► PostgreSQL
Flutter (Policyholder)─┘           │
                                   ▼
                          Agentic AI Service
                          (Internal Only)
```

**ASP.NET Core is the ONLY public backend.** React and Flutter never call the AI service directly.

## Technology Stack

| Layer          | Technology                         |
|----------------|------------------------------------|
| Backend API    | C# / ASP.NET Core Web API          |
| ORM            | Entity Framework Core               |
| Database       | PostgreSQL                          |
| Web Frontend   | React (Vite)                        |
| Mobile App     | Flutter / Dart                      |
| Agentic AI     | Python / FastAPI (internal service)  |
| CI/CD          | GitHub Actions                      |
| Containerization| Docker / Docker Compose            |

## Business Components

| Component | Owner    | Description                              |
|-----------|----------|------------------------------------------|
| A         | Member 1 | Policy Management                        |
| B         | Member 2 | Claims Submission & Document Verification|
| C         | Member 3 | Risk Assessment / Fraud Flagging         |
| D         | Member 4 | Payout Processing                        |

## Agentic AI Agents

| Agent | Owner    | Responsibility                  |
|-------|----------|---------------------------------|
| 1     | Member 1 | Coordinator / Planning Agent    |
| 2     | Member 2 | Document Verification Agent     |
| 3     | Member 3 | Fraud / Risk Assessment Agent   |
| 4     | Member 4 | Validation / Safety Agent       |

## Cross-Platform Workflow

```
Flutter (Policyholder submits claim)
    → ASP.NET Core (validates & persists)
    → PostgreSQL (saves claim)
    → ASP.NET Core (starts AI workflow)
    → Coordinator Agent → Document Verification → Fraud/Risk → Validation
    → Pending Human Approval
    → React (Adjuster: Approve / Reject / Request Revision)
    → ASP.NET Core (records decision)
    → Payout Processing
    → PostgreSQL (updated)
    → Flutter (displays updated status)
```

## Project Structure

```
├── backend/          # ASP.NET Core Web API + EF Core
├── frontend-web/     # React (Vite) staff dashboard
├── mobile/           # Flutter policyholder app
├── ai-service/       # Python/FastAPI AI agents (internal)
├── database/         # Diagrams, scripts, seed docs
├── docs/             # Architecture, ADRs, API docs
└── .github/          # CI workflows
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 18+
- Flutter 3.x
- Python 3.11+
- PostgreSQL 15+
- Docker & Docker Compose

### Quick Start

```bash
# Start PostgreSQL
docker-compose up -d

# Backend
cd backend
dotnet restore
dotnet build

# React Frontend
cd frontend-web
npm install
npm run dev

# Flutter Mobile
cd mobile
flutter pub get
flutter run

# AI Service
cd ai-service
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn main:app --reload
```

## Documentation

- [System Architecture](docs/architecture/system-architecture.md)
- [Agentic AI Architecture](docs/architecture/agentic-ai-architecture.md)
- [Cross-Platform Workflow](docs/architecture/cross-platform-workflow.md)
- [API Documentation](docs/api/README.md)
- [Database Design](docs/database/README.md)
- [Testing Strategy](docs/testing/README.md)
- [Team Contribution Matrix](docs/team/contribution-matrix.md)

## License

This project is developed for academic purposes as part of the SE3090 module.
