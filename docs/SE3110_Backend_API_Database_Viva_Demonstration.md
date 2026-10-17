# SE3110 — Backend/API & Database Testing: Complete University Viva Demonstration Guide

> **SE3110 – Quality Management in Software Engineering**
> **Insurance Claims Management System — Individual Testing Evidence**
> **Individual scope:** Kinukshan — Backend/API and Database Testing
> **Working branch:** `test/backend-api-database-kinukshan`
> **Platform:** macOS Terminal | ASP.NET Core / .NET 10 | PostgreSQL 17 | xUnit | Postman | Newman

---

## Table of Contents

- [1. Project and Individual Contribution](#1-project-and-individual-contribution)
- [2. Preparation Before the Viva](#2-preparation-before-the-viva)
- [3. GitHub Individual Contribution Demonstration](#3-github-individual-contribution-demonstration)
- [4. xUnit Unit Testing Demonstration](#4-xunit-unit-testing-demonstration)
- [5. Integration Testing Demonstration](#5-integration-testing-demonstration)
- [6. Backend API Demonstration](#6-backend-api-demonstration)
- [7. Newman Automated API Testing](#7-newman-automated-api-testing)
- [8. Intentionally Fail Newman Tests Using Only Terminal](#8-intentionally-fail-newman-tests-using-only-terminal)
- [9. PostgreSQL Database Integrity Demonstration](#9-postgresql-database-integrity-demonstration)
- [10. Defect Investigation and Retesting](#10-defect-investigation-and-retesting)
- [11. Common Viva Questions and Answers](#11-common-viva-questions-and-answers)
- [12. Troubleshooting](#12-troubleshooting)
- [13. Final Ready-to-Say Viva Script](#13-final-ready-to-say-viva-script)

---

## Recorded Report Results vs. Actual Database Inspection

| Category | Recorded Report Results | Actual Database Inspection |
|---|---|---|
| **xUnit Unit Tests** | 594 passed, 0 failed | — |
| **Integration Tests** | 1 passed, 0 failed | — |
| **Newman API Tests** | 10 requests, 22 assertions, 0 failures | — |
| **Database Tables** | — | 19 tables |
| **EF Core Migrations** | — | 9 migration records |
| **Foreign-Key Constraints** | — | 16 foreign keys |
| **Policy Records** | — | 5 policy records (at time of inspection) |

> **NOTE:** Recorded counts are historical evidence, not a promise about a future run. If the branch or test data changes, demonstrate and explain the live results.

---

## 1. Project and Individual Contribution

### 1.1 Insurance Claims Management System Overview

The Insurance Claims Management System is a full-stack web application for managing insurance policies, claims processing, payouts, and document verification. It features:

- **ASP.NET Core (.NET 10)** backend Web API with Entity Framework Core and PostgreSQL 17
- Role-based access control (RBAC) with JWT authentication
- Policy management, claims adjudication, payout processing, and notification workflows
- A Flutter mobile client, React web frontend, and AI-powered risk assessment service

### 1.2 My Individual Testing Responsibilities

My individual scope is **Backend/API and Database Testing**, which covers:

1. **Backend Unit Testing** — Executing and verifying the xUnit automated test suite against backend business logic
2. **Backend Integration Testing** — Running integration tests that verify connected component interactions
3. **API Testing** — Demonstrating HTTP response codes (200, 201, 400, 401, 403) using `curl` and Postman
4. **Automated API Testing** — Designing and running Postman/Newman collections for repeatable automated API validation
5. **Database Integrity Testing** — Verifying PostgreSQL schema (tables, migrations, foreign keys) and data persistence

### 1.3 Tools Used

| Tool | Purpose |
|---|---|
| **.NET 10** | Backend framework and test runner (`dotnet test`) |
| **xUnit** | .NET unit and integration testing framework |
| **Postman** | Interactive API test design and execution |
| **Newman** | CLI runner for Postman collections (headless, CI-compatible) |
| **EF Core** | ORM and database migration management |
| **PostgreSQL 17** | Relational database for persistent storage |
| **psql** | PostgreSQL interactive terminal client |
| **curl** | Command-line HTTP client for API requests |

### 1.4 Test Case IDs

| Test Case ID | Category | Purpose |
|---|---|---|
| **K-TC-01** | Unit Testing | Backend business logic verification via xUnit |
| **K-TC-02** | Integration Testing | Connected component interaction verification |
| **K-TC-03** | API Authentication | JWT authentication and 401/403 response validation |
| **K-TC-04** | API Authorization (RBAC) | Role-based access control boundary testing |
| **K-TC-05** | API Validation | Negative input testing and 400 Bad Request verification |
| **K-TC-06** | Database Integrity | Schema, migrations, foreign keys, and persistence |

### 1.5 One-Minute Introduction

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Good morning/afternoon. My name is Kinukshan and my individual contribution to the Insurance Claims Management System project is Backend/API and Database Testing.*
>
> *I used xUnit for backend automated unit and integration tests, Postman and Newman for automated API testing, and PostgreSQL with Entity Framework Core for database verification.*
>
> *My report documents 594 passing unit tests, one passing integration test, and a Newman API collection with 10 requests and 22 passing assertions. I tested authentication, role-based authorization, invalid input handling, database migrations, foreign-key constraints, and API-to-database persistence.*
>
> *I also investigated a Newman authentication failure caused by an incorrect test credential, corrected the configuration securely, and reran the tests successfully.*
>
> *I will now demonstrate each of these testing areas from the Terminal."*

---

## 2. Preparation Before the Viva

### 2.1 Terminal Layout

Organize **three Terminal windows** before the viva begins:

| Terminal | Purpose | Status |
|---|---|---|
| **Terminal A** | Backend API server (`dotnet run`) | Keeps running throughout |
| **Terminal B** | Test execution: `dotnet test`, `curl`, `newman` | Active command terminal |
| **Terminal C** | PostgreSQL interactive console (`psql`) | Database queries |

### 2.2 Preflight Checks (Terminal B)

Run these commands **before** the viva to confirm everything is ready.

**Step 1 — Navigate to the project directory:**

```bash
# Terminal B (macOS Terminal)
cd ~/Desktop/Insurance-Claims-Final-Integration
```

**Step 2 — Confirm the Git branch:**

```bash
# Terminal B (macOS Terminal)
git branch --show-current
```

Expected output:

```
test/backend-api-database-kinukshan
```

**Step 3 — Check Git status:**

```bash
# Terminal B (macOS Terminal)
git status --short
```

Expected output: empty (clean working tree).

**Step 4 — Verify .NET version:**

```bash
# Terminal B (macOS Terminal)
dotnet --version
```

Expected output: `10.x.x` (any .NET 10 version).

**Step 5 — Verify PostgreSQL:**

```bash
# Terminal B (macOS Terminal)
psql --version
```

Expected output: `psql (PostgreSQL) 17.x` (any PostgreSQL 17 version).

**Step 6 — Verify Newman (local project installation):**

```bash
# Terminal B (macOS Terminal)
testing/postman/node_modules/.bin/newman --version
```

Expected output: `6.x.x` (Newman version 6+).

> If Newman is not found locally, check if it is installed globally: `newman --version`

**Step 7 — Locate test project files:**

```bash
# Terminal B (macOS Terminal)
find backend/tests -maxdepth 3 -name '*.csproj'
```

Expected output:

```
backend/tests/InsuranceClaims.IntegrationTests/InsuranceClaims.IntegrationTests.csproj
backend/tests/InsuranceClaims.UnitTests/InsuranceClaims.UnitTests.csproj
```

**Step 8 — Confirm Newman collection and runner:**

```bash
# Terminal B (macOS Terminal)
ls -la testing/postman/
```

Expected files:

```
InsuranceClaims_API_Tests.postman_collection.json
InsuranceClaims_Local.postman_environment.json
run-newman-tests.sh
package.json
README.md
```

**Step 9 — Confirm the Newman runner is executable:**

```bash
# Terminal B (macOS Terminal)
test -f testing/postman/run-newman-tests.sh \
  && echo 'Newman runner found' \
  || echo 'Newman runner MISSING'
```

**Step 10 — Verify backend API responds (requires API running in Terminal A; skip if not yet started):**

```bash
# Terminal B (macOS Terminal)
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5080/api/PolicyTypes
```

Expected: `200` if the API is running.

**Step 11 — Verify the test database is accessible (requires PostgreSQL running):**

```bash
# Terminal B (macOS Terminal)
psql -h localhost -p 5432 -U postgres \
  -d insurance_claims_assignment_test \
  -c "SELECT COUNT(*) FROM \"Policies\";"
```

Enter the `postgres` password when prompted.

---

## 3. GitHub Individual Contribution Demonstration

### 3.1 Show Current Branch

```bash
# Terminal B (macOS Terminal)
git branch --show-current
```

Expected output:

```
test/backend-api-database-kinukshan
```

### 3.2 Show Git Commit History

```bash
# Terminal B (macOS Terminal)
git log -5 --oneline
```

Expected output (top commit):

```
c323ad6 (HEAD -> test/backend-api-database-kinukshan) test add Postman and Newman backend API test suite
```

### 3.3 Show My Individual Contribution Commit

```bash
# Terminal B (macOS Terminal)
git show --stat c323ad6
```

This displays the files I contributed in commit `c323ad6`:

| File | Description |
|---|---|
| `testing/postman/InsuranceClaims_API_Tests.postman_collection.json` | Postman API test collection (10 requests, 22 assertions) |
| `testing/postman/InsuranceClaims_Local.postman_environment.json` | Secure environment template with empty password fields |
| `testing/postman/README.md` | Comprehensive testing documentation |
| `testing/postman/package.json` | Node.js project with Newman and htmlextra dependencies |
| `testing/postman/package-lock.json` | Locked dependency tree |
| `testing/postman/run-newman-tests.sh` | Automated Newman runner with secure credential injection |
| `.gitignore` | Updated to exclude test result artifacts |

### 3.4 List Individual Testing Files

```bash
# Terminal B (macOS Terminal)
ls -la testing/postman/*.json testing/postman/*.sh testing/postman/*.md
```

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"This is my individual backend/API and database testing branch. I contributed an automated Postman collection and Newman runner with documentation and secure configuration to make the API tests reproducible. My contribution commit `c323ad6` added 7 files totalling over 3,300 lines of code including the test collection, environment template, runner script, and documentation."*

> **If asked: "What did you personally do?"**
>
> *"I executed and analyzed the xUnit backend test suites; created the automated Postman/Newman API tests; verified authentication, authorization and invalid inputs; inspected PostgreSQL schema, foreign keys and persisted policies; and documented a test credential issue followed by retesting."*

> **NOTE:** The report records 594 passing unit tests. Do not claim you personally wrote all 594 unless you actually did. Clearly distinguish your automated API contribution from the existing .NET test suite.

---

## 4. xUnit Unit Testing Demonstration

### 4.1 What Is Unit Testing?

**Unit testing** verifies individual methods, functions, and business rules in isolation. Each test checks a single piece of logic using:
- **Arrange**: Set up test data and dependencies (often using mocks or fakes)
- **Act**: Call the method being tested
- **Assert**: Verify the result matches the expected outcome

### 4.2 How xUnit Works

xUnit is a .NET testing framework. It uses:
- `[Fact]` attributes for single test cases
- `[Theory]` attributes with `[InlineData]` for parameterized tests
- Assertion methods like `Assert.Equal()`, `Assert.True()`, `Assert.Throws<>()`

The `dotnet test` command discovers and runs all test methods, then reports pass/fail counts.

### 4.3 Run the Unit Tests

```bash
# Terminal B (macOS Terminal)
dotnet test \
  backend/tests/InsuranceClaims.UnitTests/InsuranceClaims.UnitTests.csproj \
  --verbosity normal
```

> **If the project path cannot be found:**
> ```bash
> find backend/tests -name '*.csproj'
> # Use the exact UnitTests .csproj path from the result
> ```

### 4.4 What the Command Does

| Part | Meaning |
|---|---|
| `dotnet test` | .NET CLI command to discover and run tests |
| `backend/tests/InsuranceClaims.UnitTests/InsuranceClaims.UnitTests.csproj` | Path to the unit test project |
| `--verbosity normal` | Shows individual test names and results |

### 4.5 Expected vs. Actual Results

| Metric | Report Result (Historical) | Live Result |
|---|---|---|
| **Passed** | 594 | *(read from terminal output)* |
| **Failed** | 0 | *(read from terminal output)* |
| **Skipped** | 0 | *(read from terminal output)* |

> **IMPORTANT:** The live count may differ from 594 if the branch or test code has changed. Always report the actual live number, not the memorized count.

### 4.6 How to Explain the Test Output

The terminal output shows:

1. **Build phase**: The test project compiles successfully
2. **Test discovery**: xUnit discovers all `[Fact]` and `[Theory]` test methods
3. **Test execution**: Each test runs and reports ✓ passed or ✗ failed
4. **Summary line**: `Passed! - Failed: 0, Passed: XXX, Skipped: 0, Total: XXX`

### 4.7 What to Do If Tests Fail

1. Read the failure message — it shows the expected vs. actual value
2. Check if it is a data-dependent failure (e.g., database state changed)
3. Explain to the instructor: *"This test expected X but received Y, which may indicate a configuration change or a recent code update"*
4. **Do not** modify test code during the viva

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Unit testing verifies individual methods and business rules using xUnit. I ran the backend test suite and checked that the results matched the expected behavior. My submitted report records 594 passes and zero failures. The live count you see is [read the number from the terminal]."*

---

## 5. Integration Testing Demonstration

### 5.1 What Is Integration Testing?

**Integration testing** verifies that multiple connected components work together correctly. Unlike unit tests that isolate a single method, integration tests check the interaction between:
- Service layers and repositories
- Application logic and database access
- Multiple components in a realistic configuration

### 5.2 Unit Testing vs. Integration Testing

| Aspect | Unit Testing | Integration Testing |
|---|---|---|
| **Scope** | Single method or function | Multiple connected components |
| **Dependencies** | Mocked or faked | Real or configured components |
| **Speed** | Fast (milliseconds) | Slower (may access database/network) |
| **Example** | A business rule returns the right result | A service correctly communicates with another application layer or persistence component |

### 5.3 Run the Integration Tests

```bash
# Terminal B (macOS Terminal)
dotnet test \
  backend/tests/InsuranceClaims.IntegrationTests/InsuranceClaims.IntegrationTests.csproj \
  --verbosity normal
```

### 5.4 Expected vs. Actual Results

| Metric | Report Result (Historical) | Live Result |
|---|---|---|
| **Passed** | 1 | *(read from terminal output)* |
| **Failed** | 0 | *(read from terminal output)* |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Integration testing checks whether connected backend components interact as expected. My documented integration test execution completed successfully with one passing test. The integration test project is located at `backend/tests/InsuranceClaims.IntegrationTests/`."*

---

## 6. Backend API Demonstration

### 6.1 Start the Backend API

Before running HTTP tests, start the API with the isolated test database.

**Terminal A — Leave this process running throughout the viva:**

```bash
# Terminal A (macOS Terminal) — Configure test database connection
read -s "PG_TEST_PASSWORD?Enter local PostgreSQL password: "
echo
export PG_TEST_PASSWORD

export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=insurance_claims_assignment_test;Username=postgres;Password=${PG_TEST_PASSWORD};SSL Mode=Disable"
export ASPNETCORE_ENVIRONMENT=Development
```

```bash
# Terminal A (macOS Terminal) — Start the API server
dotnet run \
  --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj \
  --no-launch-profile \
  --urls http://127.0.0.1:5080
```

Wait for the startup message confirming the API is listening on port `5080`. Keep Terminal A running.

**Terminal B — Seed test data (one-time):**

```bash
# Terminal B (macOS Terminal)
curl -X POST http://127.0.0.1:5080/api/Seed/staff
```

### 6.2 HTTP 200 OK — Successful Retrieval

**Scenario:** Public endpoint returns policy type reference data.

```bash
# Terminal B (macOS Terminal)
curl -i http://127.0.0.1:5080/api/PolicyTypes
```

| Detail | Value |
|---|---|
| **Request** | `GET /api/PolicyTypes` (no authentication required) |
| **Expected Status** | `HTTP/1.1 200 OK` |
| **Expected Body** | JSON array of policy types with `id` and `name` fields |
| **Reason** | This is a public reference data endpoint; no JWT token is needed |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"This GET request to the PolicyTypes endpoint returns HTTP 200 OK with a JSON array of available policy types. This is a public endpoint that does not require authentication."*

### 6.3 HTTP 401 Unauthorized — Missing Authentication

**Scenario:** Protected endpoint rejects a request without a JWT bearer token.

```bash
# Terminal B (macOS Terminal)
curl -i http://127.0.0.1:5080/api/Policies
```

| Detail | Value |
|---|---|
| **Request** | `GET /api/Policies` (no Authorization header) |
| **Expected Status** | `HTTP/1.1 401 Unauthorized` |
| **Expected Body** | Empty or error message |
| **Reason** | The `/api/Policies` endpoint requires JWT bearer authentication |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"This request to the protected Policies endpoint returns HTTP 401 Unauthorized because I did not include a JWT bearer token. The API correctly rejects unauthenticated access."*

### 6.4 HTTP 201 Created — Successful Resource Creation

**Scenario:** Demonstrated within the Newman collection (API-08 and API-09).

- **API-08** registers a test policyholder → `POST /api/Auth/register` → **201 Created**
- **API-09** creates a valid policy → `POST /api/Policies` → **201 Created**

These are executed automatically via Newman. See [Section 7](#7-newman-automated-api-testing).

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"HTTP 201 Created indicates a resource was successfully created. In my Newman suite, request API-08 registers a test policyholder and API-09 creates a valid insurance policy, both returning 201."*

### 6.5 HTTP 400 Bad Request — Invalid Input Rejected

**Scenario:** Demonstrated within the Newman collection (API-07).

The collection request `API-07 — Invalid Policy Validation` submits deliberately invalid values:
- Coverage limit: `-100` (negative)
- Deductible: `-50` (negative)
- Deductible percentage: `150` (exceeds 100%)

Expected response: `HTTP 400 Bad Request` with validation error messages:
- `"Coverage limit must be greater than zero."`
- `"Deductible cannot be negative."`
- `"Deductible percentage must be between 0 and 100."`

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Negative testing deliberately sends invalid values to verify that the API rejects them and returns meaningful validation errors. This reduces the chance of invalid business data being accepted. My test submitted negative coverage, negative deductible, and a deductible percentage above 100, and the API returned HTTP 400 with specific validation messages."*

### 6.6 HTTP 403 Forbidden — Insufficient Permissions (RBAC)

**Scenario:** Demonstrated within the Newman collection (API-06).

The collection request `API-06 — Role-Based Authorization` uses a ClaimsAdjuster token to access the Underwriter/Admin-restricted `POST /api/Policies/validate-expiry` endpoint.

Expected response: `HTTP 403 Forbidden`.

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Role-based access control is different from authentication. A JWT can prove the user identity, but the user still needs the required role to call a restricted API. My negative test verified that the API returns 403 for an unauthorized role — a ClaimsAdjuster trying to access an Underwriter-only operation."*

---

## 7. Newman Automated API Testing

### 7.1 What Is Postman?

**Postman** is a GUI application for designing, testing, and documenting API requests. It allows you to:
- Build HTTP requests with headers, bodies, and parameters
- Write test scripts (assertions) in JavaScript
- Organize requests into collections
- Define environment variables for different configurations

### 7.2 What Is Newman?

**Newman** is the headless Command-Line Interface (CLI) runner for Postman collections. It enables:
- Automated test execution **without** the Postman desktop GUI
- Integration into CI/CD pipelines
- Deterministic, repeatable test execution with console summaries
- HTML report generation with `newman-reporter-htmlextra`

### 7.3 Postman vs. Newman

| Aspect | Postman | Newman |
|---|---|---|
| **Interface** | GUI (desktop app) | CLI (terminal) |
| **Automation** | Manual or scheduled runs | Fully automated, scriptable |
| **CI/CD Integration** | Limited | Native (exit code-based) |
| **Reports** | Built-in viewer | CLI summary + HTML export |
| **Environment** | Interactive variable editing | `--env-var` CLI injection |

### 7.4 What Is an Assertion?

An **assertion** is a programmatic check that verifies the actual result matches the expected outcome. In Postman/Newman:

```javascript
pm.test("Status code is 200 OK", function () {
    pm.response.to.have.status(200);
});
```

If the actual response status differs from 200, the assertion **fails** and Newman reports the failure.

### 7.5 My Automated API Test Suite

The collection `InsuranceClaims_API_Tests.postman_collection.json` contains **10 sequential requests** with **22 assertions** covering:

| Test ID | Request | Method & Endpoint | Assertions |
|---|---|---|---|
| **API-01** | Policy Types | `GET /api/PolicyTypes` | 200 OK, valid JSON, array not empty, captures `policyTypeId` |
| **API-02** | Authentication Protection | `GET /api/Policies` | 401 Unauthorized (no JWT) |
| **API-03** | Underwriter Login | `POST /api/Auth/login` | 200 OK, JWT exists, role is Underwriter |
| **API-04** | Authenticated User Profile | `GET /api/Auth/me` | 200 OK, email matches, role is Underwriter |
| **API-05** | ClaimsAdjuster Login | `POST /api/Auth/login` | 200 OK, JWT exists, role is ClaimsAdjuster |
| **API-06** | Role-Based Authorization | `POST /api/Policies/validate-expiry` | 403 Forbidden (adjuster → underwriter route) |
| **API-07** | Invalid Policy Validation | `POST /api/Policies` | 400 Bad Request, 3 validation error messages |
| **API-08** | Register Test Policyholder | `POST /api/Auth/register` | 201 Created, userId captured |
| **API-09** | Valid Policy Creation | `POST /api/Policies` | 201 Created, ID matches relations |
| **API-10** | Retrieve Created Policy | `GET /api/Policies/{{policyId}}` | 200 OK, ID matches |

### 7.6 JWT Authentication and RBAC Flow

The collection tests JWT and RBAC in this order:

1. **API-02** proves the protected endpoint rejects unauthenticated requests (401)
2. **API-03** authenticates `underwriter@test.com` and captures the JWT token
3. **API-04** uses the captured token to verify the user profile
4. **API-05** authenticates `adjuster@test.com` and captures a second JWT
5. **API-06** proves the adjuster's token is rejected on an Underwriter-only endpoint (403)

### 7.7 Validation and Persistence Testing

1. **API-07** sends invalid numeric values and asserts 400 with specific error messages
2. **API-08** registers a dynamically-generated policyholder and asserts 201 Created
3. **API-09** creates a policy linked to the new policyholder and asserts 201 Created
4. **API-10** retrieves the created policy by ID and verifies the data matches

### 7.8 Prepare Secure Credentials

**Step 1 — Load the staff password securely from .NET User Secrets:**

```bash
# Terminal B (macOS Terminal)
export STAFF_PASSWORD="$(
  dotnet user-secrets list \
    --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj |
  sed -n 's/^Seed:StaffPassword = //p'
)"

if [ -n "$STAFF_PASSWORD" ]; then
  echo 'Staff credential loaded'
else
  echo 'Staff credential missing - check User Secrets'
fi
```

> **NOTE:** Never type, display, or log the actual password. The above command reads it from .NET User Secrets silently.

**Step 2 (Optional) — If User Secrets is unavailable, enter the password manually:**

```bash
# Terminal B (macOS Terminal)
read -s "STAFF_PASSWORD?Enter Staff Password (Seed:StaffPassword): "
echo
export STAFF_PASSWORD
```

### 7.9 Run the Automated Collection

```bash
# Terminal B (macOS Terminal)
bash testing/postman/run-newman-tests.sh
```

> The runner script (`run-newman-tests.sh`) automatically:
> 1. Checks if the API is running at `http://127.0.0.1:5080`
> 2. Uses `STAFF_PASSWORD` from the environment (prompts if not set)
> 3. Defaults `POLICYHOLDER_PASSWORD` to `TestPass123!@#` if not set
> 4. Locates the local Newman binary at `testing/postman/node_modules/.bin/newman`
> 5. Executes the collection with CLI and HTML reporters
> 6. Saves the HTML report to `testing/postman/results/newman-report.html`

### 7.10 Expected vs. Actual Results

| Metric | Report Result (Historical) | Live Result |
|---|---|---|
| **Requests** | 10 | *(read from terminal output)* |
| **Assertions** | 22 | *(read from terminal output)* |
| **Failures** | 0 | *(read from terminal output)* |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"I designed automated API requests using Postman and ran the collection using Newman from the terminal. Newman executes the requests and checks assertions such as response status codes and expected data. My documented final run passed 10 requests and 22 assertions without failures. The suite tests authentication, role-based authorization, input validation, user registration, policy creation, and data retrieval."*

---

## 8. Intentionally Fail Newman Tests Using Only Terminal

> **⚠️ EXTREMELY IMPORTANT FOR THE VIVA**
>
> The instructor may ask: *"Show me a failure and then fix it."*
> This must be performed **entirely from the Terminal** without modifying any source code or test scripts.

### 8.1 How This Works

The `run-newman-tests.sh` script reads the `STAFF_PASSWORD` environment variable (lines 30-34). If `STAFF_PASSWORD` is already set in the environment, the script uses it directly. This means you can **override** the password by prefixing the command with a different value.

When an incorrect password is supplied:
- The staff login requests (API-03, API-05) send the wrong password to `POST /api/Auth/login`
- The API **correctly** returns HTTP 401 Unauthorized (this is expected application behavior)
- The Newman assertions **fail** because they expect HTTP 200 OK (this is a test-configuration failure)
- Dependent requests (API-04, API-06, API-07, API-09, API-10) also fail because they need valid JWT tokens

### 8.2 Step-by-Step Demonstration

#### Step A — Show a Passing Baseline

```bash
# Terminal B (macOS Terminal)
bash testing/postman/run-newman-tests.sh
```

Verify the output shows **0 failures**. This confirms the suite works with correct credentials.

#### Step B — Intentionally Cause a Failure with Wrong Password

```bash
# Terminal B (macOS Terminal)
STAFF_PASSWORD='wrong-viva-demo-password' \
  bash testing/postman/run-newman-tests.sh
```

> **Technical explanation of the command:**
> - `STAFF_PASSWORD='wrong-viva-demo-password'` sets this variable **only** for the command that follows
> - It does **not** overwrite the `STAFF_PASSWORD` that was previously exported in your shell
> - The runner script uses this wrong password, causing authentication failures

**Expected output:** Multiple assertion failures. Look for:

```
  ✗  Status code is 200 OK
     AssertionError: expected response to have status code 200 but got 401
```

#### Step C — Identify Which Assertion Failed

In the Newman output, look for the `✗` marks:

| Request | Expected | Actual | Reason |
|---|---|---|---|
| API-03 (Underwriter Login) | 200 OK | 401 Unauthorized | Wrong password → authentication rejected |
| API-05 (ClaimsAdjuster Login) | 200 OK | 401 Unauthorized | Wrong password → authentication rejected |
| API-04 (User Profile) | 200 OK | 401 Unauthorized | No valid token captured |
| API-06 (RBAC) | 403 Forbidden | 401 Unauthorized | No valid token captured |

#### Step D — Explain Expected vs. Actual

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"The test failed because I deliberately supplied an incorrect staff password. The API correctly returned HTTP 401 Unauthorized — that is correct application behavior, not a bug. The Newman assertion failure occurred because the test configuration expected a successful login with HTTP 200 OK. This is the difference between a real application defect and a test-configuration failure."*

#### Step E — Restore Correct Configuration and Retest

```bash
# Terminal B (macOS Terminal)
# The original STAFF_PASSWORD is still exported in your shell
# because the wrong password was only set for the single preceding command
bash testing/postman/run-newman-tests.sh
```

Verify the output shows **0 failures** again. No source code, test scripts, or collection files were modified.

#### Step F — Connection to K-DEF-01

This demonstration mirrors your documented defect **K-DEF-01**:

| Aspect | K-DEF-01 (Report) | Viva Demonstration |
|---|---|---|
| **Problem** | Incorrect staff password in test configuration | Deliberately wrong password via CLI |
| **Symptom** | Newman assertion failures on login requests | Same — assertions expect 200, get 401 |
| **Root Cause** | Test configuration error, not application bug | Same — configuration, not code |
| **Fix** | Corrected password via .NET User Secrets | Restored correct `STAFF_PASSWORD` in shell |
| **Retest** | All 10 requests and 22 assertions passed | Same — 0 failures after fix |

> **CRITICAL DISTINCTION:**
> - HTTP 401 from incorrect credentials is **expected application behavior** (the API correctly rejects wrong passwords)
> - The failing Newman assertion is a **test-configuration failure** (the test expected success but received a rejection due to misconfiguration)
> - These are NOT the same thing as a real application defect

> **NEVER** display or log real passwords, JWTs, or secrets during the demonstration.

### 8.3 Fallback: If the Runner Overrides `STAFF_PASSWORD`

If `run-newman-tests.sh` is modified to always read from User Secrets and ignores the environment variable, use Newman directly:

```bash
# Terminal B (macOS Terminal)
testing/postman/node_modules/.bin/newman run \
  testing/postman/InsuranceClaims_API_Tests.postman_collection.json \
  -e testing/postman/InsuranceClaims_Local.postman_environment.json \
  --env-var "baseUrl=http://127.0.0.1:5080" \
  --env-var "staffPassword=wrong-viva-demo-password" \
  --env-var "policyholderPassword=TestPass123!@#" \
  -r cli
```

This bypasses the runner script entirely and sends the wrong password directly via Newman's `--env-var`.

---

## 9. PostgreSQL Database Integrity Demonstration

> All SQL commands below are **read-only**. Do not execute `DROP`, `DELETE`, `TRUNCATE`, or any destructive SQL during the viva.

### 9.1 Connect to the Isolated Test Database

```bash
# Terminal C (macOS Terminal)
psql -h localhost -p 5432 \
  -U postgres \
  -d insurance_claims_assignment_test
```

Enter the password for the `postgres` database user when prompted.

> **NOTE:** Your earlier connection attempt with `-U "$PGUSER"` failed because `PGUSER` was unset and `psql` tried the macOS username `kinukshan`. Explicitly using `-U postgres` works correctly.

### 9.2 A — Database Table Verification

```sql
-- Inside psql (Terminal C)
\dt
```

**Previously verified result:** 19 tables, including:

| Key Tables |
|---|
| `Policies` |
| `Claims` |
| `Users` |
| `Payouts` |
| `PaymentTransactions` |
| `PaymentWebhookEvents` |
| `PolicyTypes` |
| `__EFMigrationsHistory` |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"I connected to my isolated PostgreSQL test database. Using the psql command `\dt`, I verified that the database contains 19 expected application and migration-history tables."*

### 9.3 B — EF Core Migration Verification

```sql
-- Inside psql (Terminal C)
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC;
```

**Previously verified result:** 9 recorded migrations, including:

- `InitialIntegratedSchema`
- `AddPaymentTransactions`
- `AddPayPalFieldsAndWebhookEvents`
- `CorrectCanonicalPolicyTypePricing`

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"The EF Core migration history table lists which migrations were recorded as applied to this test database. I verified nine records. This is evidence that the database schema was updated through the application migration process."*

> **NOTE:** Migration history does not, by itself, prove that every migration currently present in the source repository has been applied. It proves which migrations are recorded in the database.

### 9.4 C — Foreign-Key Constraint Verification

```sql
-- Inside psql (Terminal C)
SELECT
    tc.table_name,
    tc.constraint_name,
    kcu.column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
 AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public'
ORDER BY tc.table_name;
```

**Previously verified result:** 16 foreign-key constraint rows.

**Three key relationships to explain:**

| Child Table | Foreign Key Column | Parent Table | Meaning |
|---|---|---|---|
| `Claims` | `PolicyId` | `Policies` | Every claim must reference a valid policy |
| `Payouts` | `ClaimId` | `Claims` | Every payout must reference a valid claim |
| `PaymentTransactions` | `PayoutId` | `Payouts` | Every payment must reference a valid payout |
| `Policies` | `PolicyTypeId` | `PolicyTypes` | Every policy must reference a valid policy type |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Foreign keys maintain referential integrity. For example, Claims has a PolicyId foreign key to Policies. A payout references a claim, and a payment transaction references a payout. The query shows that those constraints exist in PostgreSQL. I verified 16 foreign-key constraints in total."*

> **NOTE:** The 16 rows verify constraint presence; they do not demonstrate a failed insertion. An invalid foreign-key insert would require a separate, carefully isolated negative test. Avoid modifying database data just to prove this during a short viva.

### 9.5 D — Policy Persistence Verification

```sql
-- Inside psql (Terminal C)
SELECT
    "Id",
    "PolicyNumber",
    "CoverageLimit",
    "Premium",
    "Deductible",
    "Status"
FROM "Policies"
WHERE "PolicyNumber" = 'POL-1A9E3D1D';
```

**Previously verified result:**

| Field | Value |
|---|---|
| `Id` | `f3093811-33e5-4908-b4ea-9eb097f6ea5c` |
| `PolicyNumber` | `POL-1A9E3D1D` |
| `CoverageLimit` | `1000000.00` |
| `Premium` | `25000.00` |
| `Deductible` | `0.00` |
| `Status` | `Draft` |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"Here I retrieve a policy that was created during API persistence testing. The record exists in PostgreSQL with its saved policy number, coverage limit, premium and status, which is evidence of data persistence."*

### 9.6 E — Policy Count Verification

```sql
-- Inside psql (Terminal C)
SELECT COUNT(*) AS total_policies
FROM "Policies";
```

**Previously verified result:** 5 policy rows.

> **NOTE:** If you have rerun Newman or created new policies since the last inspection, the count may increase. Always report the **live** number, not a memorized count.

### 9.7 F — API-to-Database Comparison

This is the **end-to-end verification**: compare the same policy retrieved from PostgreSQL (Step D above) with the API response.

**Terminal B — Authenticated API request:**

```bash
# Terminal B (macOS Terminal)
# First, obtain a valid Underwriter JWT token
# (Use Postman or the Newman login flow to get one)
# Then retrieve the policy by its ID:
curl -i -H "Authorization: Bearer <UNDERWRITER_TOKEN>" \
  http://127.0.0.1:5080/api/Policies/f3093811-33e5-4908-b4ea-9eb097f6ea5c
```

> **NOTE:** Replace `<UNDERWRITER_TOKEN>` with a valid JWT obtained from the `POST /api/Auth/login` endpoint during Newman execution. **Do not** display the actual token on the projector.

**Expected result:** HTTP 200 OK with JSON containing the same values:

| Field | Database Value | API Response Value | Match? |
|---|---|---|---|
| `id` | `f3093811-33e5-4908-b4ea-9eb097f6ea5c` | `f3093811-33e5-4908-b4ea-9eb097f6ea5c` | ✅ |
| `policyNumber` | `POL-1A9E3D1D` | `POL-1A9E3D1D` | ✅ |
| `coverageLimit` | `1000000.00` | `1000000.00` | ✅ |
| `premium` | `25000.00` | `25000.00` | ✅ |
| `deductible` | `0.00` | `0.00` | ✅ |
| `status` | `Draft` | `Draft` | ✅ |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"I compare the same policy retrieved through the backend API and queried directly from PostgreSQL. Matching identifiers and values confirm consistent storage and retrieval for this test record."*

> **NOTE:** Historical record counts may change after additional testing. The comparison of a specific policy ID is more reliable than comparing row counts.

### 9.8 Exit psql

```sql
-- Inside psql (Terminal C)
\q
```

---

## 10. Defect Investigation and Retesting

### 10.1 K-DEF-01: Newman Authentication Test-Configuration Failure

| Aspect | Detail |
|---|---|
| **Defect ID** | K-DEF-01 |
| **Problem** | Newman login requests (API-03, API-05) failed during initial test execution |
| **Symptom** | Assertions expecting HTTP 200 received HTTP 401 Unauthorized |
| **Root Cause** | The seeded staff password in the test configuration was incorrect. The runner was not using the actual password stored in .NET User Secrets (`Seed:StaffPassword`) |
| **Category** | **Test-configuration failure** — not an application defect |
| **Correction** | Loaded the correct password securely from .NET User Secrets via `dotnet user-secrets list` and exported it as `STAFF_PASSWORD` |
| **Retest Result** | All 10 requests and 22 assertions passed after correction |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"K-DEF-01 was a test-configuration failure, not a real application bug. The Newman collection initially used an incorrect staff password, so the API correctly returned 401. Once I retrieved the correct password from .NET User Secrets and exported it, all tests passed."*

### 10.2 K-OBS-01: Deductible Value Observation

| Aspect | Detail |
|---|---|
| **Observation ID** | K-OBS-01 |
| **Observation** | The deductible value stored in the database differed from the value submitted in the API creation request |
| **Investigation** | Backend business logic (domain configuration/policy type rules) **intentionally overrides** the submitted deductible with the authoritative value from the policy type configuration |
| **Database Value** | `0.00` (for policy `POL-1A9E3D1D`) |
| **API GET Response** | `0.00` (same as database) |
| **Conclusion** | **Not a defect** — this is intended business behavior. The database and API retrieval values are consistent |

> **🎤 WHAT TO SAY TO THE INSTRUCTOR:**
>
> *"K-OBS-01 was an observation, not a defect. The backend policy configuration and business rules determine the authoritative deductible, and the database and API retrieval results were consistent. The submitted deductible was intentionally overridden by domain logic."*

### 10.3 Distinguishing Defect Types

| Type | Description | Example |
|---|---|---|
| **Real application defect** | Application code produces incorrect results or crashes | API returns 500 when valid data is submitted |
| **Test-configuration failure** | Tests fail due to incorrect setup, not code | Wrong password → 401 → assertion fails (K-DEF-01) |
| **Intended business behavior** | Values differ from user input by design | Deductible override by policy type rules (K-OBS-01) |

---

## 11. Common Viva Questions and Answers

### xUnit and Unit Testing

**Q1: Why did you use xUnit?**
> It is a .NET testing framework that executes automated unit and integration checks and reports pass/fail results. It supports `[Fact]` and `[Theory]` test attributes with assertion methods.

**Q2: What is a unit test?**
> A test that verifies a single method or business rule in isolation, typically using mocks for dependencies. For example, testing that a premium calculation returns the correct value.

**Q3: What does the `[Fact]` attribute mean in xUnit?**
> It marks a method as a single test case. xUnit discovers and runs all `[Fact]` methods automatically.

**Q4: What is the difference between `[Fact]` and `[Theory]`?**
> `[Fact]` runs once with no parameters. `[Theory]` runs multiple times with different data supplied via `[InlineData]`, allowing parameterized testing.

### Postman and Newman

**Q5: Why use Newman instead of only Postman?**
> Postman designs requests interactively; Newman reruns a whole collection from the terminal and produces repeatable automated results. Newman can be integrated into CI/CD pipelines.

**Q6: What is an assertion?**
> A check that the actual result matches the expected outcome, such as verifying that the response status is HTTP 400 for invalid policy values.

**Q7: How does Newman handle authentication tokens?**
> The collection uses pre-request scripts and test scripts to capture JWT tokens from login responses and store them as environment variables. Subsequent requests use these tokens in their Authorization headers.

### JWT and RBAC

**Q8: What is JWT?**
> JSON Web Token — a compact, signed token used for stateless authentication. The backend issues a JWT after successful login, and the client includes it in the `Authorization: Bearer` header.

**Q9: What is the difference between HTTP 401 and 403?**
> 401 means missing or invalid authentication (who are you?). 403 means authenticated but lacking permission for the requested resource (you are who you say, but you're not allowed here).

**Q10: What is RBAC?**
> Role-Based Access Control. Users are assigned roles (e.g., Underwriter, ClaimsAdjuster, Admin), and API endpoints restrict access based on the user's role. A ClaimsAdjuster cannot perform Underwriter-only operations.

### HTTP Responses

**Q11: What is negative testing?**
> Intentionally supplying invalid or unauthorized requests and verifying that the system rejects them appropriately. For example, sending a negative coverage limit and expecting HTTP 400.

**Q12: What does HTTP 400 Bad Request mean?**
> The server cannot process the request because the client sent invalid data. The response body typically contains validation error messages.

### EF Core and Migrations

**Q13: What is a migration?**
> A versioned database schema change managed by Entity Framework Core. Migrations track table creation, column changes, and constraint modifications in code.

**Q14: What is EF Core?**
> Entity Framework Core is an Object-Relational Mapper (ORM) for .NET. It maps C# classes to database tables and generates SQL for data access.

### PostgreSQL and Database

**Q15: What is a foreign key?**
> A constraint connecting a child record to a valid parent record, enforcing referential integrity. For example, a `Claim` must reference a valid `Policy`.

**Q16: What is persistence testing?**
> Checking that an API-created record is actually stored in the database and can later be retrieved with the expected values.

### Testing Concepts

**Q17: What is integration testing?**
> Testing the interaction between multiple connected components (e.g., service layer + repository + database) to verify they work together correctly.

**Q18: What is regression testing?**
> Re-running previously passing tests after a code change to ensure nothing has been broken. Newman provides automated regression testing.

**Q19: What is test isolation?**
> Ensuring each test runs independently without being affected by other tests. The Newman collection uses dynamic timestamped emails and the test database is isolated from production.

### Defects

**Q20: Why did the Newman test initially fail?**
> The seeded staff password in the test configuration was incorrect. Correcting it via .NET User Secrets and re-exporting the `STAFF_PASSWORD` environment variable fixed the failures.

**Q21: Is the deductible difference a bug?**
> Not in the investigated case. Business rules calculated the authoritative deductible, and the database and GET results were consistent.

**Q22: Do the passing tests prove the whole system is flawless?**
> No. They show the tested conditions passed. Further coverage, environments, data, and production checks would be needed for full confidence.

---

## 12. Troubleshooting

### 12.1 Quick Reference Table

| Problem | Safe Response |
|---|---|
| **PostgreSQL password authentication failed as `kinukshan`** | `PGUSER` was unset. Use `-U postgres` explicitly, then enter the `postgres` database password. |
| **Incorrect PostgreSQL username** | Always specify `-U postgres` in the `psql` command. Do not rely on the default macOS username. |
| **API server not running / Connection refused on :5080** | Start the backend in Terminal A: `dotnet run --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj --urls http://127.0.0.1:5080`. Verify the startup message. |
| **Wrong API port** | Confirm you are using `http://127.0.0.1:5080`. Check Terminal A output for the actual listening port. |
| **HTTP 401 from protected endpoint** | Expected without JWT. For a successful GET, use a valid authorized test token. |
| **HTTP 403 with valid token** | Token is valid but the role is not permitted. Use the correctly authorized test account (e.g., Underwriter for policy operations). |
| **Newman credentials failing** | Check: (1) Staff user is seeded (`POST /api/Seed/staff`), (2) `STAFF_PASSWORD` is exported, (3) .NET User Secrets match. |
| **Missing testing script** | Run `test -f testing/postman/run-newman-tests.sh && echo 'Found'`. Verify you are on the correct branch. |
| **xUnit test failure** | Read the failure message. Report the actual result to the instructor. Do not modify test code during the viva. |
| **Database record not found** | Verify you connected to `insurance_claims_assignment_test` (not the production database). Check table and column name casing (PostgreSQL is case-sensitive with quotes). |
| **Changing test counts** | Explain that the suite or branch may have changed. Trust the live output and report it accurately. |
| **Missing environment variables** | Re-export the required variables. Use `echo $STAFF_PASSWORD | wc -c` (never `echo $STAFF_PASSWORD`) to check if the variable is set without revealing the value. |
| **Connection refused on :5432** | Check PostgreSQL service status: `brew services list` or `pg_isready`. Do not reset the database. |
| **Table not found in psql** | Verify you connected to `insurance_claims_assignment_test` and that schema/case is correct. Use `\dt` to list all tables. |
| **Policy count changed** | Other test runs may create data. Compare a specific policy ID instead of relying on row counts. |

### 12.2 What NOT to Do During the Viva

- ❌ Do not display a real JWT or copy actual secrets into documentation or screenshots
- ❌ Do not run `DROP TABLE`, `DELETE`, `TRUNCATE`, reset-database commands, or production migrations
- ❌ Do not commit or push a deliberately broken test collection; keep failure demonstrations temporary
- ❌ Do not claim a 401 or 400 result is an application bug when the request was deliberately invalid
- ❌ Do not claim that a table/foreign-key list proves every runtime data-integrity case — describe precisely what was checked
- ❌ Do not modify application source code or test scripts during the viva

---

## 13. Final Ready-to-Say Viva Script

### 13.1 One-Minute Introduction

> **🎤 SAY THIS AT THE BEGINNING:**
>
> *"My individual part is Backend/API and Database Testing of the Insurance Claims Management System. I used xUnit for backend automated tests, Postman and Newman for API testing, and PostgreSQL with Entity Framework Core for database verification.*
>
> *My report records 594 passing unit tests, one passing integration test, and a Newman collection with 10 requests and 22 passing assertions. I verified authentication, role-based authorization, invalid input handling, database migrations, foreign keys and API persistence.*
>
> *In my local database inspection, I confirmed 19 tables, nine applied migration records, 16 foreign-key constraints and saved policy records. I also investigated a Newman login failure caused by an incorrect test credential, corrected the configuration securely, and reran the tests successfully. The tested areas behaved as expected under these conditions."*

### 13.2 Short Explanation for Each Demonstration

| Demo | What to Say |
|---|---|
| **Git Branch** | *"This is my individual testing branch with my contribution commit c323ad6."* |
| **xUnit** | *"Unit tests verify isolated business logic. My report records 594 passes."* |
| **Integration** | *"Integration tests verify connected components. One test passed."* |
| **curl 200** | *"Public endpoint returns policy types with HTTP 200 OK."* |
| **curl 401** | *"Protected endpoint correctly rejects unauthenticated access with 401."* |
| **Newman** | *"Automated API suite: 10 requests, 22 assertions, 0 failures."* |
| **Newman Fail** | *"Deliberate wrong password caused 401. Restored credentials and retested."* |
| **psql Tables** | *"19 database tables including Policies, Claims, and migrations."* |
| **psql Migrations** | *"9 EF Core migrations recorded in the migration history."* |
| **psql Foreign Keys** | *"16 foreign-key constraints enforce referential integrity."* |
| **psql Policy** | *"API-created policy persisted and matches the API response."* |

### 13.3 Thirty-Second Closing Statement

> **🎤 SAY THIS AT THE END:**
>
> *"In summary, I executed and verified backend unit tests, integration tests, and automated API tests using xUnit and Newman. I confirmed database integrity through PostgreSQL inspection of tables, migrations, foreign keys, and persisted data. I investigated and resolved a test-configuration issue and confirmed all tested areas behaved as expected. Thank you."*

### 13.4 Final Ordered Command Checklist

Execute these commands in order during the viva:

```bash
# ─── TERMINAL B — Repository root ─────────────────────────
# 1. Show branch and contribution
git branch --show-current
git log -5 --oneline
git show --stat c323ad6

# 2. Run xUnit unit tests
dotnet test \
  backend/tests/InsuranceClaims.UnitTests/InsuranceClaims.UnitTests.csproj \
  --verbosity normal

# 3. Run integration tests
dotnet test \
  backend/tests/InsuranceClaims.IntegrationTests/InsuranceClaims.IntegrationTests.csproj \
  --verbosity normal

# ─── TERMINAL A — Start the API ──────────────────────────
# 4. Configure test database and start API
read -s "PG_TEST_PASSWORD?Enter local PostgreSQL password: "
echo
export PG_TEST_PASSWORD
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=insurance_claims_assignment_test;Username=postgres;Password=${PG_TEST_PASSWORD};SSL Mode=Disable"
export ASPNETCORE_ENVIRONMENT=Development

dotnet run \
  --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj \
  --no-launch-profile \
  --urls http://127.0.0.1:5080

# ─── TERMINAL B — API checks and Newman ──────────────────
# 5. Public API check (200)
curl -i http://127.0.0.1:5080/api/PolicyTypes

# 6. Protected API check (401)
curl -i http://127.0.0.1:5080/api/Policies

# 7. Load credentials and run Newman
export STAFF_PASSWORD="$(
  dotnet user-secrets list \
    --project backend/src/InsuranceClaims.Api/InsuranceClaims.Api.csproj |
  sed -n 's/^Seed:StaffPassword = //p'
)"
bash testing/postman/run-newman-tests.sh

# 8. (Optional) Intentional failure and retest
STAFF_PASSWORD='wrong-viva-demo-password' \
  bash testing/postman/run-newman-tests.sh
# Explain the failure, then retest:
bash testing/postman/run-newman-tests.sh

# ─── TERMINAL C — PostgreSQL ─────────────────────────────
# 9. Connect to test database
psql -h localhost -p 5432 \
  -U postgres \
  -d insurance_claims_assignment_test
```

```sql
-- Inside psql (Terminal C):

-- 10. Show tables
\dt

-- 11. Show migrations
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC;

-- 12. Show foreign keys
SELECT
    tc.table_name,
    tc.constraint_name,
    kcu.column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
 AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public'
ORDER BY tc.table_name;

-- 13. Count policies
SELECT COUNT(*) AS total_policies
FROM "Policies";

-- 14. Inspect specific policy
SELECT
    "Id",
    "PolicyNumber",
    "CoverageLimit",
    "Premium",
    "Deductible",
    "Status"
FROM "Policies"
WHERE "PolicyNumber" = 'POL-1A9E3D1D';

-- 15. Exit
\q
```

### 13.5 Report Cross-Reference

| Report Section / Page | What It Supports |
|---|---|
| 7.1 / page 12 | Detailed test cases K-TC-01 through K-TC-06 |
| 9.1 / pages 21–22 | 594, 1, 10, 22 recorded test results |
| 10 / pages 26–27 | K-DEF-01 credential failure and K-OBS-01 deductible observation |
| 11.1 / pages 32–36 | K-EV-01 through K-EV-10 evidence and screenshots |
| 12.1 / page 45 | Individual branch and `c323ad68...` commit |

### 13.6 Evidence to Capture (Screenshots)

Take screenshots of:

1. ✅ Branch/commit output
2. ✅ xUnit unit-test summary
3. ✅ Integration-test summary
4. ✅ Public API 200 and protected API 401
5. ✅ Newman totals (10/22/0)
6. ✅ 19 database tables (`\dt`)
7. ✅ Migration history (9 records)
8. ✅ 16 foreign-key constraints
9. ✅ Matching policy data in PostgreSQL and Postman/API

> **Hide credentials and tokens in all screenshots.**

---

*Prepared 17 October 2026. Commands and results should be verified on the machine before the viva; local environments and test data may change.*
