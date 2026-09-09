# Insurance Claims Processing System

> **SE3090 — Software Engineering Frameworks**
> Assignment 1 — Integrated Full-Stack and Agentic AI Application Development

## Overview

A cross-platform insurance claims processing system with agentic AI capabilities. Policyholders submit and track claims via a Flutter mobile app, while claims adjusters and underwriters manage reviews, risk assessments, and payouts through a React web dashboard. An agentic AI workflow automates document verification, fraud/risk assessment, and validation before human approval.

## Architecture

```
React (Staff) ────────┐
                       ├──► ASP.NET Core Web API ──► Neon PostgreSQL
Flutter (Policyholder)─┘           │
                                   ▼
                          Agentic AI Service
                          (Internal Only)
```

**ASP.NET Core is the ONLY public backend.** React and Flutter never call the AI service or database directly.

### Database Connection Architecture

```
ASP.NET Core Web API
        ↓
  Entity Framework Core
        ↓
      Npgsql
        ↓
  Neon PostgreSQL
```

## Technology Stack

| Layer           | Technology                                    |
|-----------------|-----------------------------------------------|
| Backend API     | C# / ASP.NET Core Web API (.NET 10)           |
| ORM             | Entity Framework Core 10.x                    |
| Database        | Neon PostgreSQL (cloud-hosted PostgreSQL)      |
| DB Provider     | Npgsql.EntityFrameworkCore.PostgreSQL          |
| Web Frontend    | React (Vite)                                  |
| Mobile App      | Flutter / Dart                                |
| Agentic AI      | Python / FastAPI (internal service)            |
| CI/CD           | GitHub Actions                                |
| Containerization| Docker / Docker Compose (optional local only) |

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
    → Neon PostgreSQL (saves claim)
    → ASP.NET Core (starts AI workflow)
    → Coordinator Agent → Document Verification → Fraud/Risk → Validation
    → Pending Human Approval
    → React (Adjuster: Approve / Reject / Request Revision)
    → ASP.NET Core (records decision)
    → Payout Processing
    → Neon PostgreSQL (updated)
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

- .NET 10 SDK
- Node.js 18+
- Flutter 3.x
- Python 3.11+
- Docker & Docker Compose (optional — only for local PostgreSQL fallback)

### Database Setup (Neon PostgreSQL)

1. **Create a Neon project** at [neon.tech](https://neon.tech).
2. **Obtain the connection string** from the Neon dashboard. It will look like:
   ```
   Host=<neon-host>;Database=<database>;Username=<username>;Password=<password>;SSL Mode=Require
   ```
3. **Store the connection string securely** using one of these methods:

   **Option A — Environment variable:**
   ```bash
   export ConnectionStrings__DefaultConnection="Host=<neon-host>;Database=<database>;Username=<username>;Password=<password>;SSL Mode=Require"
   ```

   **Option B — .NET User Secrets (recommended for local development):**
   ```bash
   cd backend/src/InsuranceClaims.Api
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<neon-host>;Database=<database>;Username=<username>;Password=<password>;SSL Mode=Require"
   ```

4. **Never commit the real connection string** to source control.

### Optional: Local PostgreSQL Fallback

If you need to work offline without Neon, a Docker Compose file is provided:

```bash
docker compose up -d
```

Then set the connection string to the local instance:
```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=insurance_claims;Username=postgres;Password=postgres_dev"
```

### Running EF Core Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project backend/src/InsuranceClaims.Infrastructure \
  --startup-project backend/src/InsuranceClaims.Api

# Apply migrations to the database
dotnet ef database update \
  --project backend/src/InsuranceClaims.Infrastructure \
  --startup-project backend/src/InsuranceClaims.Api
```

> **Note:** Only run `database update` when a valid Neon (or local) connection string is configured.

### Quick Start

```bash
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
