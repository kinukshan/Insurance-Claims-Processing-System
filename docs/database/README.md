# Database Design

## Primary Database

| Property       | Value                                    |
|----------------|------------------------------------------|
| **Provider**   | Neon PostgreSQL (cloud-hosted PostgreSQL) |
| **DB Engine**  | PostgreSQL                               |
| **ORM**        | Entity Framework Core 10.x               |
| **EF Provider**| Npgsql.EntityFrameworkCore.PostgreSQL     |
| **Schema Mgmt**| EF Core Migrations                       |

## Connection Architecture

```
ASP.NET Core Web API
        ↓
  Entity Framework Core
        ↓
  Npgsql (EF Core PostgreSQL provider)
        ↓
  Neon PostgreSQL (cloud)
```

> **Important:** Neon is a cloud PostgreSQL hosting provider — it runs standard PostgreSQL.
> There is no change to the database engine, SQL dialect, or ORM layer.

## Connection Configuration

The connection string is loaded via the standard ASP.NET Core configuration key:

```
ConnectionStrings:DefaultConnection
```

It is read in code via:

```csharp
configuration.GetConnectionString("DefaultConnection")
```

### Supplying the Connection String

| Method                  | Usage                                        |
|-------------------------|----------------------------------------------|
| **.NET User Secrets**   | Local development (recommended)              |
| **Environment variable**| `ConnectionStrings__DefaultConnection`       |
| **CI/CD secrets**       | GitHub Actions / deployment pipelines        |

**Never commit the real connection string to source control.**

### Typical Neon Connection String Format

```
Host=<neon-host>;Database=<database>;Username=<username>;Password=<password>;SSL Mode=Require
```

## Migrations

EF Core migrations are stored in:

```
backend/src/InsuranceClaims.Infrastructure/Persistence/Migrations/
```

### Commands

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

## Optional Local PostgreSQL Fallback

A Docker Compose file is provided at the project root for offline/isolated development:

```bash
docker compose up -d
```

This starts a local PostgreSQL 15 instance on `localhost:5432`. This is **not** required when using Neon.

## Security Rules

- Only ASP.NET Core connects to the database
- React and Flutter **never** connect directly to Neon PostgreSQL
- The Python AI service does **not** connect directly to the database
- Connection strings with credentials must **never** be committed to Git
- `.env` files are excluded via `.gitignore`

<!-- TODO: Add ER diagram and schema documentation -->
<!-- Note: EF Core migrations are the primary schema management tool -->
<!-- This supplements with documentation only -->
