# System Architecture

## Status
Draft — to be completed during implementation.

## Overview

The Insurance Claims Processing System uses a layered architecture with Neon PostgreSQL as the cloud-hosted primary database.

## Architecture Diagram

```
┌─────────────┐     ┌─────────────┐
│   React     │     │   Flutter   │
│  (Staff)    │     │(Policyholder)│
└──────┬──────┘     └──────┬──────┘
       │                   │
       └────────┬──────────┘
                │
       ┌────────▼────────┐
       │  ASP.NET Core   │
       │   Web API       │
       └────────┬────────┘
                │
       ┌────────▼────────┐       ┌──────────────┐
       │ Neon PostgreSQL │       │  AI Service  │
       │  (EF Core +     │       │  (Internal)  │
       │   Npgsql)       │       └──────────────┘
       └─────────────────┘
```

### Database Connection Detail

```
ASP.NET Core Web API
        ↓
  Entity Framework Core
        ↓
  Npgsql (EF Core PostgreSQL Provider)
        ↓
  Neon PostgreSQL (cloud-hosted PostgreSQL)
```

## Layers

### Presentation Layer
- React (staff-facing web dashboard)
- Flutter (policyholder-facing mobile app)

### API Layer
- ASP.NET Core Web API — the ONLY public backend

### Application Layer
- Business logic services
- Validation
- DTOs

### Domain Layer
- Domain entities
- Value objects
- Enums

### Infrastructure Layer
- Entity Framework Core with Npgsql (Neon PostgreSQL)
- JWT authentication
- External service integrations
- AI service integration client

### AI Service Layer (Internal)
- FastAPI
- Agent orchestration
- Tool execution

## Key Design Decisions

- **Neon PostgreSQL** is the primary database — a cloud-hosted PostgreSQL service
- **Entity Framework Core + Npgsql** is the ORM/provider — no change from standard PostgreSQL usage
- **Connection strings** are managed securely via User Secrets or environment variables, never committed
- **Docker Compose PostgreSQL** is retained as an optional local fallback only
- React and Flutter **never** connect directly to the database or AI service

<!-- TODO: Document additional key architecture decisions -->
