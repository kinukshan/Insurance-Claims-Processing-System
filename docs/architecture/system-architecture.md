# System Architecture

## Status
Draft — to be completed during implementation.

## Overview

<!-- TODO: Add high-level system architecture diagram -->

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
       │   PostgreSQL    │       │  AI Service  │
       │   (EF Core)    │       │  (Internal)  │
       └─────────────────┘       └──────────────┘
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
- Entity Framework Core (PostgreSQL)
- JWT authentication
- External service integrations
- AI service integration client

### AI Service Layer (Internal)
- FastAPI
- Agent orchestration
- Tool execution

## Key Design Decisions

<!-- TODO: Document key architecture decisions -->
