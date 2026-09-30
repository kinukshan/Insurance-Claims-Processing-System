# Insurance Claims Mobile Application (`mobile/`)

**Course:** SE3090 — Software Engineering Frameworks (Assignment 1)
**Target Platform:** Flutter (Android, iOS, macOS Desktop, Web)
**Role Scope:** Policyholder Client Application
**Backend Gateway:** ASP.NET Core Web API (`http://localhost:5097/api` / `http://10.0.2.2:5097/api`)
**Git Branch:** `fix/claims-adjuster-authorization`

---

## 1. Overview & Architecture

The mobile application is a production-ready Flutter client designed specifically for **Policyholders** to manage their insurance policies, file new claims with binary evidence uploads (camera photos, gallery images, PDF reports), track claim lifecycle milestones in real-time, view authorized payout calculations, and inspect notifications.

```
┌────────────────────────────────────────────────────────┐
│               Flutter Mobile Application               │
│          (Policyholder Client — Material 3)            │
└───────────────────────────┬────────────────────────────┘
                            │ HTTPS / HTTP (JWT Bearer)
                            ▼
┌────────────────────────────────────────────────────────┐
│            ASP.NET Core Web API Gateway                │
│       (:5097 — Role Authorization & Validation)        │
└──────┬────────────────────┬────────────────────┬───────┘
       │                    │                    │
       ▼                    ▼                    ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  PostgreSQL  │     │ Python AI    │     │ Mock/Resend  │
│  (Neon Cloud)│     │ Microservice │     │ Notifications│
└──────────────┘     └──────────────┘     └──────────────┘
```

### Architectural Principles
1. **Single Public Gateway:** Flutter **never** communicates directly with PostgreSQL, Google Gemini, Resend, PayPal, or the internal Python AI service. All interactions route through ASP.NET Core.
2. **Role Gating & Access Control:** Mobile is strictly scoped to the `Policyholder` role. If a staff member (ClaimsAdjuster, Underwriter, Admin) attempts to log in via mobile, the app gracefully presents an `AccessDeniedView` instructing them to use the React Web Portal.
3. **Information Protection:** Internal fraud scores, risk assessment indicators, adjuster investigation notes, and staff approval controls are strictly protected on the backend and omitted from mobile DTOs.
4. **Authoritative Status Semantics:** The app never presents an approved payout as already paid (`Pending Approval` → `Approved (Pending Disbursement)` → `Processing` → `Paid / Disbursed`). Notification statuses distinguish between `Mock Sent` (simulated), `Resend Accepted` (provider accepted, final delivery unconfirmed), and `Failed` (error/suppressed).

---

## 2. State Management Architecture

As documented in [ADR-002](../../docs/adr/ADR-002-flutter-state-management.md), state management is implemented using **Provider (`provider: ^6.1.2`)** with `ChangeNotifier`:

* **Global Session State (`AuthProvider`):** Manages `AuthStatus` (`initial`, `loading`, `authenticated`, `unauthenticated`, `error`). It handles login, registration, logout, and token restoration on startup. Outbound HTTP requests automatically inject the stored JWT via `ApiService`.
* **Declarative Auth Gate (`_AuthGate`):** The app root in `lib/main.dart` listens to `AuthProvider` to route users without flash-of-unauthenticated-content:
  - `initial` → `AuthLoadingScreen` (session restoration from OS secure storage)
  - `unauthenticated` → `LoginScreen`
  - `authenticated` (Policyholder) → `MainNavigationShell` (5-tab navigation)
  - `authenticated` (Staff) → `AccessDeniedView`
* **Localized Screen State (`StatefulWidget`):** Form inputs, dynamic policy dropdowns, document checklists, search queries, and status filter chips are managed locally in screen state with dependency-injected services (`ClaimService`, `PolicyService`, `PayoutService`, `NotificationService`).

---

## 3. Screen Inventory & Features

| Screen | Route / Location | Description & Capabilities |
| :--- | :--- | :--- |
| **Login Screen** | `/login` | Email/password authentication, JWT storage, error banner handling, and toggle to registration. |
| **Register Screen** | `/register` | New Policyholder account creation with client-side form validation. |
| **Main Navigation Shell** | `/home`, `/policies`, `/payouts`, `/notifications` | Persistent 5-tab `BottomNavigationBar` (`Home`, `Policies`, `Claims`, `Payouts`, `Alerts`) using `IndexedStack` and nested `Navigator` instances. |
| **Home Dashboard** | Tab 0 (`HomeScreen`) | Welcome greeting, quick action shortcuts, active policy count, and recent claim summaries. |
| **Policies Screen** | Tab 1 (`PoliciesScreen`) | Real-time search by policy number/type, status filter chips (`All`, `Active`, `Pending`, `Expired`), policy card list with coverage limits. |
| **Policy Details** | `PolicyDetailsScreen` | Detailed policy metadata, coverage terms, effective dates, and deductible breakdown. |
| **Claim History** | Tab 2 (`ClaimHistoryScreen`) | Real-time text search (number, type, keyword), status filter chips (`All`, `Draft`, `Submitted`, `UnderReview`, `Approved`, `Withdrawn`), empty search state with filter reset, and pull-to-refresh. |
| **Submit Claim** | `/claims/submit` | Dynamic policy selector populated via `GET /api/policies/my`, policy-to-claim compatibility validation, incident details, camera photo capture (`image_picker`), file/PDF picker (`file_picker`), checklist document tagging, draft saving, and resilient multi-step upload. |
| **Claim Details** | `/claims/details` | Authoritative claim metadata, uploaded document list with verification badges (`Verified`, `Flagged`, `Rejected`), Payout card with quick link, evidence upload action, Claim Withdrawal action, and Draft Deletion action. |
| **Claim Status Timeline**| `/claims/status` | Authoritative timeline rendering confirmed backend timestamps (`createdAt`, `submittedAt`, `approvalTimestamp`, `updatedAt`) and terminal states without hardcoded assumptions. |
| **Payout History** | Tab 3 (`PayoutHistoryScreen`) | Search by reference/amount, status filter chips (`All`, `PendingApproval`, `Approved`, `Processing`, `Paid`, `Failed`), and list of past payouts. |
| **Payout Details** | `PayoutDetailScreen` | Policyholder-safe calculation breakdown (Claimed Amount, Coverage Limit, Deductible, Final Payout) and payment reference string. |
| **Notifications Screen** | Tab 4 (`NotificationsScreen`) | Live notification feed from `GET /api/notifications` displaying delivery semantics (`Mock Sent`, `Resend Accepted`, `Failed`), timestamps, and modal details. |

---

## 4. API & Environment Configuration

Configuration is loaded from environment variables and `.env`:

```bash
# Copy example to active config
cp .env.example .env
```

### Base URL Matrix
* **macOS Desktop / Chrome Web:** `http://localhost:5097/api`
* **Android Emulator:** `http://10.0.2.2:5097/api`
* **Physical Android Device:** `http://<your-host-lan-ip>:5097/api`

### Security Notice
* No secrets, database connection strings, or third-party API keys are stored in `mobile/`.
* JWT tokens are securely stored on device using `flutter_secure_storage` (Android Keystore / macOS Keychain).

---

## 5. Android Setup & Build Instructions

### 5.1 Android Manifest Configuration
* **Production Manifest (`android/app/src/main/AndroidManifest.xml`):**
  - Declares `<uses-permission android:name="android.permission.INTERNET" />`
  - Declares `<uses-permission android:name="android.permission.CAMERA" />`
* **Debug-Only Manifest (`android/app/src/debug/AndroidManifest.xml`):**
  - Declares `<application android:usesCleartextTraffic="true" />`
  - Strictly limits cleartext HTTP development traffic to debug builds; release builds enforce HTTPS.

### 5.2 Prerequisites for Android Compilation on macOS
To compile and run Android APKs on macOS:
1. Download and install **Android Studio** from [developer.android.com/studio](https://developer.android.com/studio).
2. During setup, install:
   - Android SDK Platform (API 34 or 35)
   - Android SDK Command-line Tools (latest)
   - Android SDK Platform-Tools
   - Android Emulator
3. Configure environment in `~/.zshrc`:
   ```bash
   export ANDROID_HOME=$HOME/Library/Android/sdk
   export PATH=$PATH:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools
   ```
4. Accept licenses:
   ```bash
   flutter doctor --android-licenses
   ```
5. Verify with `flutter doctor -v`.

### 5.3 Building the APK
```bash
cd mobile

# Build debug APK for local testing (uses cleartext HTTP to 10.0.2.2 or LAN IP)
flutter build apk --debug

# Build release APK for production (requires HTTPS endpoint in .env)
flutter build apk --release
```
* Built APK output location: `mobile/build/app/outputs/flutter-apk/app-debug.apk`

---

## 6. Automated Test Suite

The mobile application includes **174 automated tests** with 100% pass rate:

```bash
cd mobile
flutter pub get
flutter analyze
flutter test
```

### Test Distribution
* **Model Serialization (40 tests):** JSON mapping for `Claim`, `Policy`, `Payout`, `NotificationItem`, and `DocumentRequirement`.
* **Authentication & Routing (34 tests):** Login screen, registration validation, `AuthProvider` session restoration, token injection, and `AccessDeniedView`.
* **Claim Submission & Files (56 tests):** Policy dropdown population, claim type compatibility, document requirements mapping, file size validation, draft saving, and recovery.
* **Cross-Platform & Status History (40 tests):** Payout card navigation, claim withdrawal, draft deletion, document deletion, milestone timeline rendering, notification semantics, and real-time search/status filtering across Policy, Claim, and Payout screens.

---

## 7. Cross-Platform Workflow Verification

The end-to-end integration workflow was verified across the live ecosystem:
1. **Policyholder (Flutter):** Logged in as `test_live_phase3@example.com`, verified active policy `POL-DB18FC7A`, created draft `CLM-20260926-0005`, attached 4 binary checklist documents (`police_report.pdf`, `damage_photo.jpg`, `repair_estimate.pdf`, `driver_license.pdf`), and submitted claim.
2. **Staff Adjuster (React / ASP.NET Core):** Document Verification Agent verified all 4 checklist items (`complete: true`, `inconsistencies: 0`). Ran risk assessment (Score: 55, Medium). Prepared payout proposal for `$28,500.00`.
3. **Staff Underwriter / Admin (React / ASP.NET Core):** Authorized underwriter approved payout proposal. Admin executed Mock payment disbursement (`MOCK-PAY-e3e9754db001420`).
4. **Policyholder (Flutter):** Claim reflected `Submitted` status with `Paid` payout card, payment reference `MOCK-PAY-e3e9754db001420`, and notification audit history.
5. **Policyholder Operation:** Claim withdrawal was verified on submitted claim `CLM-20260926-0006`, successfully transitioning status to `Withdrawn`.

---

## 8. Remaining Limitations & Recommendations

1. **Local Android SDK:** The host development Mac currently lacks Android Studio and Android SDK command-line tools. Running `flutter doctor -v` shows `[✗] Android toolchain`. Android Studio must be installed following Section 5.2 to produce physical device binaries.
2. **Production API Endpoint:** The release APK requires a publicly accessible HTTPS endpoint (or reverse proxy) since cleartext HTTP is intentionally disabled in release builds.
3. **Email Delivery Provider:** Development notifications are currently logged with `Failed` delivery status because test email addresses are outside the `RESEND_RECIPIENT_ALLOWLIST` security filter, or configured with `Mock` provider in development mode.
