# Automated Backend API Testing Guide (Postman & Newman)

> **SE3090 — Software Engineering Frameworks**  
> **Insurance Claims Processing System — Integration Testing Suite**

---

## 1. Purpose of Postman Testing

Postman provides interactive and automated API validation to verify contract adherence, data integrity, and cross-component integration across our ASP.NET Core Web API backend. 

Key testing objectives include:
- Verifying endpoint availability and HTTP specification compliance.
- Validating JWT authentication flows and secure claim propagation.
- Enforcing Role-Based Access Control (RBAC) boundaries (e.g., distinguishing ClaimsAdjuster, Underwriter, and Admin authorizations).
- Verifying input validation rules (e.g., negative coverage limits or invalid deductible percentages).
- Ensuring end-to-end data persistence and retrieval against the local PostgreSQL test database (`insurance_claims_assignment_test`).

---

## 2. Purpose of Newman

Newman is the headless Command-Line Interface (CLI) test runner for Postman. It enables:
- Automated test execution outside of the Postman desktop GUI.
- Direct integration into Continuous Integration / Continuous Deployment (CI/CD) pipelines.
- Deterministic, repeatable test execution with automated console summaries.
- Production of test evidence artifacts, including comprehensive HTML reports with execution graphs, timings, request payloads, and assertion breakdowns.

---

## 3. API Test Environment

The test suite runs against the dedicated local development and testing environment:

| Property | Value |
|---|---|
| **Base URL** | `http://127.0.0.1:5080` |
| **Backend Framework** | ASP.NET Core (.NET 10), EF Core, Npgsql |
| **Database** | PostgreSQL 17 (`insurance_claims_assignment_test`) |
| **Environment Mode** | `Development` |
| **Production DB Access** | **Strictly prohibited** — Cloud Neon PostgreSQL is never touched |

### Pre-seeded Staff Test Accounts
Created via the development seed endpoint (`POST /api/Seed/staff`):
- `adjuster@test.com` — Role: `ClaimsAdjuster`
- `underwriter@test.com` — Role: `Underwriter`
- `admin@test.com` — Role: `Admin`

---

## 4. How to Start the Backend API

To run the local backend server for testing without altering production configurations or connecting to Neon:

1. Open a terminal window and set the local test database connection string:
   ```bash
   read -s "PG_TEST_PASSWORD?Enter local PostgreSQL password: "
   echo
   export PG_TEST_PASSWORD

   export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=insurance_claims_assignment_test;Username=postgres;Password=${PG_TEST_PASSWORD};SSL Mode=Disable"
   export ASPNETCORE_ENVIRONMENT=Development
   ```

2. Start the ASP.NET Core Web API on port `5080`:
   ```bash
   dotnet run \
     --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj \
     --no-launch-profile \
     --urls http://127.0.0.1:5080
   ```

3. Ensure the seed data is populated:
   ```bash
   curl -X POST http://127.0.0.1:5080/api/Seed/staff
   ```

---

## 5. How to Supply Passwords Securely

**SECURITY REQUIREMENT**: Secrets, database credentials, and raw passwords must **never** be hard-coded into JSON files, collections, or git-tracked artifacts.

Use environment variables in your terminal shell:
```bash
# Staff password configured in .NET User Secrets (Seed:StaffPassword)
read -s "STAFF_PASSWORD?Enter Staff Password: "
echo
export STAFF_PASSWORD

# Temporary Policyholder password for dynamic registration
read -s "POLICYHOLDER_PASSWORD?Enter Test Policyholder Password: "
echo
export POLICYHOLDER_PASSWORD
```

These variables are injected at runtime via Newman CLI arguments (`--env-var`) or entered interactively in Postman desktop's Current Value fields.

---

## 6. How to Run the Collection in Postman Desktop

1. Open Postman.
2. Click **Import** (top left).
3. Import both files from `testing/postman/`:
   - `InsuranceClaims_API_Tests.postman_collection.json`
   - `InsuranceClaims_Local.postman_environment.json`
4. In the top right environment dropdown, select **InsuranceClaims_Local**.
5. Click the environment quick look icon (eye icon) and enter your passwords in the **Current Value** columns:
   - `staffPassword`: your test staff password
   - `policyholderPassword`: temporary policyholder password (e.g. `TestPass123!`)
6. Right-click the **Insurance Claims System - Backend API Tests** collection and select **Run collection**.
7. Click **Run Insurance Claims System - Backend API Tests**.

---

## 7. How to Run the Collection Using Newman

### Option A — Using the Local Newman Installation (Recommended)
This repository includes an isolated local Newman installation inside `testing/postman/node_modules/`:

```bash
cd testing/postman

./node_modules/.bin/newman run InsuranceClaims_API_Tests.postman_collection.json \
  -e InsuranceClaims_Local.postman_environment.json \
  --env-var "staffPassword=$STAFF_PASSWORD" \
  --env-var "policyholderPassword=$POLICYHOLDER_PASSWORD" \
  -r cli,htmlextra \
  --reporter-htmlextra-export results/newman-report.html
```

### Option B — Using Global Newman
If you have Newman installed globally (`npm install -g newman newman-reporter-htmlextra`):

```bash
newman run testing/postman/InsuranceClaims_API_Tests.postman_collection.json \
  -e testing/postman/InsuranceClaims_Local.postman_environment.json \
  --env-var "staffPassword=$STAFF_PASSWORD" \
  --env-var "policyholderPassword=$POLICYHOLDER_PASSWORD" \
  -r cli,htmlextra \
  --reporter-htmlextra-export testing/postman/results/newman-report.html
```

---

## 8. Test Scenarios Included

| Test ID | Request Name | Method & Endpoint | Description & Key Assertions |
|---|---|---|---|
| **API-01** | Policy Types | `GET /api/PolicyTypes` | Retrieves active policy types; asserts array is non-empty; captures `policyTypeId`. |
| **API-02** | Authentication Protection | `GET /api/Policies` | Unauthenticated request; verifies `401 Unauthorized`. |
| **API-03** | Underwriter Login | `POST /api/Auth/login` | Logs in with `underwriter@test.com`; verifies JWT exists; captures `underwriterToken`. |
| **API-04** | Authenticated User Profile | `GET /api/Auth/me` | Uses `underwriterToken`; asserts email matches and role is `Underwriter`. |
| **API-05** | ClaimsAdjuster Login | `POST /api/Auth/login` | Logs in with `adjuster@test.com`; captures `adjusterToken`. |
| **API-06** | Role-Based Authorization | `POST /api/Policies/validate-expiry` | ClaimsAdjuster invokes Underwriter-only route; verifies `403 Forbidden`. |
| **API-07** | Invalid Policy Validation | `POST /api/Policies` | Submits negative limit, negative deductible, and >100% percentage; asserts `400 Bad Request` with validation error messages. |
| **API-08** | Register Test Policyholder | `POST /api/Auth/register` | Dynamically generates unique timestamped email; asserts `201 Created`; captures `policyholderId` and `policyholderToken`. |
| **API-09** | Valid Policy Creation | `POST /api/Policies` | Creates valid policy under Underwriter role; asserts `201 Created`; captures `policyId`. |
| **API-10** | Retrieve Created Policy | `GET /api/Policies/{{policyId}}` | Retrieves created policy by ID; asserts `200 OK` and verifies ID & policyholder relationships match. |

---

## 9. Expected HTTP Status Codes Mapping

| HTTP Code | Meaning | Context in Suite |
|---|---|---|
| **200 OK** | Successful request | Reference data retrieval (`API-01`), staff logins (`API-03`, `API-05`), profile fetch (`API-04`), policy retrieval (`API-10`) |
| **201 Created** | Resource successfully created | Policyholder registration (`API-08`), policy creation (`API-09`) |
| **400 Bad Request** | Invalid input rejected | Input validation failure when numeric ranges are breached (`API-07`) |
| **401 Unauthorized** | Authentication required | Unauthenticated request rejected on protected route (`API-02`) |
| **403 Forbidden** | Insufficient permissions | Role violation when ClaimsAdjuster attempts Underwriter route (`API-06`) |

---

## 10. How to Generate Test Evidence

Execution generates two primary forms of evidence:
1. **Terminal Console Output**: A summary table displaying total requests, scripts, iterations, and assertion counts.
2. **HTML Interactive Report**: Generated at `testing/postman/results/newman-report.html` via `newman-reporter-htmlextra`. It visualizes:
   - Summary statistics and pass rates.
   - Detailed tab per request showing headers, query params, body, and status codes.
   - Exact assertion test logs.

---

## 11. Security & Compliance Notes

1. **No Credentials in Git**: All `.json` configuration files in this repository specify empty password strings (`""`).
2. **Authorization Headers**: Bearer tokens are dynamically captured at runtime in memory and never checked into source control.
3. **Database Isolation**: The test suite targets exclusively the local PostgreSQL instance. Production Neon connection strings are never exposed or called.
