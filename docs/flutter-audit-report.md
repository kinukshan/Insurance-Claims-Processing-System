# Comprehensive Audit Report: Flutter Mobile Application (`mobile/`)

**Course:** SE3090 — Software Engineering Frameworks (Assignment 1)
**Repository:** `Insurance-Claims-Final-Integration`
**Current Branch:** `fix/claims-adjuster-authorization`
**Audit Date:** September 26, 2026
**Auditor:** Antigravity Engineering Agent

---

## Executive Summary

The existing Flutter codebase in `mobile/` provides an initial foundation—specifically around **Claims Management (Component B)** and model serialization. However, the application currently operates as an **isolated, dev-mode prototype** rather than a secure, connected, role-aware client.

### Key Audit Findings
1. **Security & Authentication Bypass:** The mobile app completely skips authentication. It boots directly into `ClaimHistoryScreen` via a hardcoded dummy UUID (`00000000-0000-0000-0000-000000000001`) in `api_service.dart`. `login_screen.dart`, `register_screen.dart`, and `auth_service.dart` are unimplemented stubs. There is no JWT storage, session lifecycle, or route protection.
2. **Disconnected Screens:** Out of 13 UI screens in the project, **8 screens are completely disconnected** from the app's routing table in `main.dart`. Policy Management, Payout Tracking, Risk Status, and the Home Dashboard cannot be accessed through normal app navigation.
3. **Backend Authorization & Endpoint Mismatches:**
   - **Risk Assessment (403 Forbidden):** `ClaimRiskStatusScreen` calls `/api/riskassessments/{claimId}/status`. However, `RiskAssessmentsController.cs` is decorated with `[Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]` at the class level, blocking all Policyholders with HTTP 403.
   - **Payout History (403 Forbidden):** `PayoutService.getPayoutHistory()` calls `/api/payouts/history`, which is restricted to staff roles. The policyholder-accessible endpoint is `/api/payouts/my`.
   - **Notifications:** Flutter has **no service, models, or UI** for notifications, despite the backend `NotificationsController.cs` providing policyholder notification feeds.
4. **Document & File Handling Gaps:**
   - `submit_claim_screen.dart` only supports image selection via `image_picker`. Document types like PDF (police reports, repair estimates, medical bills) cannot be selected because `file_picker` is not installed.
   - Document type assignment is hardcoded to a single static string per claim type (e.g. all auto photos are tagged `Photos of Damage`), completely ignoring backend requirement checklists (`Police Report`, `Repair Estimate`, `Driver License`).
   - The user must manually copy-paste a raw Policy GUID string instead of selecting from their active policies.
5. **Architectural Separation Maintained:** Flutter **strictly complies** with the backend gateway architecture. There are **zero direct connections** to Neon PostgreSQL, Google Gemini, Resend, PayPal, or the internal Python AI service. All requests target the ASP.NET Core API.
6. **Android Build Blocker:** Android SDK is not configured on the local development environment, and `AndroidManifest.xml` lacks the essential `INTERNET` permission and cleartext HTTP traffic flag needed to communicate with `http://10.0.2.2:5000`.

---

## 1. Environment & Verification Results

Commands executed within `mobile/`:

### 1.1 `flutter doctor -v`
```text
[✓] Flutter (Channel stable, 3.47.2, on macOS 26.6.2 25G83 darwin-arm64, locale en-LK)
    • Flutter version 3.47.2 at /Users/kinukshan/development/flutter
    • Dart version 3.13.2 • DevTools version 2.60.0
[✗] Android toolchain - develop for Android devices
    ✗ Unable to locate Android SDK.
[!] Xcode - develop for iOS and macOS
    ✗ Xcode installation is incomplete; CocoaPods not installed.
[✓] Chrome - develop for the web
    • Chrome at /Applications/Google Chrome.app/Contents/MacOS/Google Chrome
[✓] Connected device (2 available)
    • macOS (desktop) • macos  • darwin-arm64   • macOS 26.6.2
    • Chrome (web)    • chrome • web-javascript • Google Chrome 153.0.8010.53
[✓] Network resources
    • All expected network resources are available.
```
*Note: While Android and iOS native toolchains require local configuration, the application builds and runs immediately on macOS Desktop and Chrome Web.*

### 1.2 `flutter pub get`
* Succeeded in `2.4s`. Dependencies resolved cleanly.
* 7 indirect packages have newer compatible versions available.

### 1.3 `flutter analyze`
```text
Analyzing mobile...
No issues found! (ran in 0.9s)
```
* Zero linter warnings or syntax errors. Existing Dart code conforms to `analysis_options.yaml`.

### 1.4 `flutter test`
```text
00:00 +46: All tests passed!
```
* 46 tests passed. However, **coverage is severely skewed**:
  - 39 test cases cover pure JSON serialization of `Claim`, `Policy`, `Payout`, and `RiskAssessment` models.
  - 6 test cases cover form field labels in `SubmitClaimScreen`.
  - 1 smoke test in `widget_test.dart`.
  - **Zero tests** exist for `ApiService`, `AuthService`, any other screen, state management, or network error conditions.

---

## 2. Codebase Structure & File Inventory

```
mobile/
├── android/                     # Android native project files
│   ├── app/
│   │   ├── build.gradle.kts     # Namespace: com.insuranceclaims.insurance_claims_mobile
│   │   └── src/main/AndroidManifest.xml  # MISSING INTERNET PERMISSION
│   └── local.properties         # Missing sdk.dir
├── lib/
│   ├── main.dart                # Entry point; sets initialRoute to /claims/history
│   ├── models/
│   │   ├── claim.dart           # Complete DTO (ClaimResponseDto mapping)
│   │   ├── claim_document.dart  # Complete DTO (ClaimDocumentDto mapping)
│   │   ├── payout.dart          # Complete DTO (PayoutDto mapping)
│   │   ├── payout_approval.dart # Complete DTO (Policyholder-safe view)
│   │   ├── policy.dart          # Complete DTO (PolicyDto mapping)
│   │   ├── risk_assessment.dart # Complete DTO (Safe status fields)
│   │   ├── user.dart            # STUB (Missing fromJson/toJson)
│   │   └── workflow_status.dart # STUB (Missing fromJson/toJson)
│   ├── providers/
│   │   └── .gitkeep             # EMPTY (No state management providers)
│   ├── routes/
│   │   └── .gitkeep             # EMPTY (Routes inline in main.dart)
│   ├── screens/
│   │   ├── auth/
│   │   │   ├── login_screen.dart       # PLACEHOLDER (Text("Login — TODO"))
│   │   │   └── register_screen.dart    # PLACEHOLDER (Text("Register — TODO"))
│   │   ├── claims/
│   │   │   ├── claim_details_screen.dart # PARTIALLY COMPLETE (Connected)
│   │   │   ├── claim_history_screen.dart # PARTIALLY COMPLETE (Connected)
│   │   │   ├── claim_status_screen.dart  # PARTIALLY COMPLETE (Connected)
│   │   │   └── submit_claim_screen.dart  # PARTIALLY COMPLETE (Connected)
│   │   ├── home/
│   │   │   └── home_screen.dart        # PLACEHOLDER (Text("Home — TODO"))
│   │   ├── payout/
│   │   │   ├── payout_history_screen.dart # IMPLEMENTED BUT DISCONNECTED (403 API)
│   │   │   └── payout_status_screen.dart  # IMPLEMENTED BUT DISCONNECTED
│   │   ├── policy/
│   │   │   ├── policies_screen.dart       # IMPLEMENTED BUT DISCONNECTED
│   │   │   └── policy_details_screen.dart # IMPLEMENTED BUT DISCONNECTED
│   │   └── risk/
│   │       ├── claim_risk_status_screen.dart  # IMPLEMENTED BUT DISCONNECTED (403 API)
│   │       └── fraud_review_status_screen.dart# IMPLEMENTED BUT DISCONNECTED (Empty API)
│   ├── services/
│   │   ├── api_service.dart     # Base HTTP client; hardcodes dev X-User-Id
│   │   ├── auth_service.dart    # STUB (Empty class with TODO comments)
│   │   ├── claim_service.dart   # Complete CRUD operations for Claims
│   │   ├── payout_service.dart  # Read operations; wrong history endpoint
│   │   ├── policy_service.dart  # CRUD operations for Policies
│   │   └── risk_service.dart    # Read operations for safe risk statuses
│   ├── utils/
│   │   └── .gitkeep             # EMPTY
│   └── widgets/
│       └── .gitkeep             # EMPTY (Zero shared reusable widgets)
├── pubspec.yaml                 # Dependencies: http, image_picker, intl, cupertino_icons
└── test/                        # 46 passing tests (Model serialization & 1 form test)
```

---

## 3. Screen-by-Screen Implementation Status

| Screen | File Path | Component / Owner | Implementation Status | API Connection | Primary Defects & Gaps |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Login** | `lib/screens/auth/login_screen.dart` | Shared | **Placeholder** | Disconnected | Displays static text "Login — TODO". No text controllers, form validation, or login submission. |
| **Register** | `lib/screens/auth/register_screen.dart` | Shared | **Placeholder** | Disconnected | Displays static text "Register — TODO". No input fields for name, email, or password. |
| **Home Dashboard** | `lib/screens/home/home_screen.dart` | Shared | **Placeholder** | Disconnected | Displays static text "Home Dashboard — TODO". No policyholder metrics, quick actions, or recent claims. |
| **Submit Claim** | `lib/screens/claims/submit_claim_screen.dart` | Component B (Member 2) | **Partially Implemented** | Connected (`POST /api/claims`, `/documents`, `/submit`) | **Critical:** Policy ID is manual raw text instead of a dropdown; cannot select PDF documents; all images given hardcoded static type; no upload recovery if multi-part step fails. |
| **Claim History** | `lib/screens/claims/claim_history_screen.dart` | Component B (Member 2) | **Partially Implemented** | Connected (`GET /api/claims/my-claims`) | No search bar, no filtering by status or date range. Serves as default `initialRoute`. |
| **Claim Details** | `lib/screens/claims/claim_details_screen.dart` | Component B (Member 2) | **Partially Implemented** | Connected (`GET /api/claims/{id}`) | Displays metadata and documents with verification badges. Allows adding photo evidence. Cannot withdraw or delete claim; no link to payout screen. |
| **Claim Status Timeline** | `lib/screens/claims/claim_status_screen.dart` | Component B (Member 2) | **Partially Implemented** | Pseudo-connected | Renders a 9-step static timeline driven by route argument string. Does not fetch live workflow step logs or timestamps. |
| **Policies List** | `lib/screens/policy/policies_screen.dart` | Component A (Member 1) | **Complete UI** | **Disconnected from App** | Not registered in `main.dart`. Constructor requires `policyholderId` rather than taking it from authenticated session. |
| **Policy Details** | `lib/screens/policy/policy_details_screen.dart` | Component A (Member 1) | **Complete UI** | **Disconnected from App** | Not registered in `main.dart`. Calls `getPolicyById` and `getCoverage`. Has no UI for premium calculation or renewal. |
| **Claim Risk Status** | `lib/screens/risk/claim_risk_status_screen.dart` | Component C (Member 3) | **Complete UI** | **Broken API (403 Forbidden)** | Not registered in `main.dart`. Correctly hides fraud scores, but backend controller blocks Policyholder role. |
| **Fraud Review Status** | `lib/screens/risk/fraud_review_status_screen.dart` | Component C (Member 3) | **Complete UI** | **Disconnected / Empty** | Not registered in `main.dart`. Calls `_riskService.getReviewHistory()`, which returns hardcoded empty list `[]`. |
| **Payout Status** | `lib/screens/payout/payout_status_screen.dart` | Component D (Member 4) | **Complete UI** | **Disconnected from App** | Not registered in `main.dart`. Properly hides internal adjuster notes and displays payment reference and status. |
| **Payout History** | `lib/screens/payout/payout_history_screen.dart` | Component D (Member 4) | **Complete UI** | **Broken API (403 Forbidden)** | Not registered in `main.dart`. Calls `/api/payouts/history` (staff only) instead of `/api/payouts/my`. |

---

## 4. Deep Dive: `submit_claim_screen.dart`

`SubmitClaimScreen` is the primary interactive business screen currently reachable by policyholders. A line-by-line inspection reveals several functional flaws:

1. **Manual Policy ID Input (Lines 193–204):**
   - The user must manually type or paste a 36-character GUID string into a `TextFormField` (`_policyIdController`).
   - If the user mistypes the GUID or enters an ID of an expired or mismatched policy, the backend throws an error upon submit.
   - **Resolution Needed:** Replace the raw text input with a `DropdownButtonFormField` that asynchronously fetches the user's active policies using `GET /api/policies/my`.
2. **Missing Document Types & PDF Support (Lines 45–54, 77–110):**
   - The screen defines a static map:
     ```dart
     static const _documentTypeMap = {
       'Auto': 'Photos of Damage',
       'Home': 'Photos of Damage',
       'Health': 'Medical Report',
       'Life': 'Supporting Document',
       'Travel': 'Receipts',
       'Property': 'Photos of Damage',
       'Liability': 'Incident Report',
       'Other': 'Supporting Document',
     };
     ```
   - When a user submits an Auto claim with 3 files, **all 3 files receive the document type `'Photos of Damage'`**.
   - However, the backend's `DocumentChecklistValidator.cs` expects: `Police Report`, `Photos of Damage`, `Repair Estimate`, and `Driver License`.
   - Furthermore, `_picker.pickImage` and `_picker.pickMultiImage` only support photographic image files (`.jpg`, `.png`). Users cannot attach PDF medical reports, police reports, or receipts.
   - **Resolution Needed:** Integrate `file_picker`, allow selecting a document type tag per file from the required checklist returned by `GET /api/claims/{id}/document-requirements`.
3. **Sequential, Non-Transactional Submission Flow (Lines 125–156):**
   ```dart
   // Step 1: Create Draft Claim
   final claim = await _claimService.createClaim(...);
   // Step 2: Upload Documents sequentially
   for (final file in _evidenceFiles) {
     await _claimService.uploadDocument(...);
   }
   // Step 3: Transition from Draft to Submitted
   await _claimService.submitClaim(claim.id);
   ```
   - If `createClaim` succeeds but the network drops during document upload, the claim is left abandoned in `Draft` state in PostgreSQL.
   - The user receives an error banner, but the form does not retain the created draft ID to resume uploading, resulting in duplicate draft creations upon re-submit.

---

## 5. Security & Authentication Audit

### 5.1 Registration & Login Implementation
* **Backend:** `AuthController.cs` exposes public endpoints:
  - `POST /api/auth/register` (accepts `FirstName`, `LastName`, `Email`, `Password`, `ConfirmPassword`)
  - `POST /api/auth/login` (accepts `Email`, `Password`)
  - `GET /api/auth/me` (requires Bearer token; returns user profile)
* **Flutter:** Completely absent.
  - `auth_service.dart` is an empty stub with no HTTP calls.
  - `login_screen.dart` and `register_screen.dart` do not contain form fields.
  - No logout action exists anywhere in the UI.

### 5.2 Token Storage & API Header Injection
* `pubspec.yaml` **does not include** `flutter_secure_storage` or `shared_preferences`.
* In `api_service.dart` (lines 23–26):
  ```dart
  Map<String, String> get _headers => {
    'Content-Type': 'application/json',
    'X-User-Id': devUserId,
  };
  ```
* The mobile app relies entirely on a development backdoor (`X-User-Id` header). It **never generates, stores, or transmits** an `Authorization: Bearer <JWT>` header.
* In production or non-development environments, the ASP.NET Core `JwtBearerHandler` will immediately reject all mobile requests with **401 Unauthorized**.

### 5.3 Route Protection & Role Navigation
* There are no route guards. Entering the app opens `/claims/history` directly.
* `user.dart` defines an incomplete model without serialization methods.
* There is no mechanism to verify whether the logged-in user is a `Policyholder` versus staff, nor is there a navigation drawer or bottom navigation bar to switch between user features.

---

## 6. Backend Integration & Gateway Architecture Audit

Audit requirement: *Flutter must not connect directly to PostgreSQL, Gemini, Resend, PayPal or the internal Python AI service.*

### 6.1 Architectural Verification
* **Neon PostgreSQL:** No PostgreSQL drivers (`postgres` package) exist in `pubspec.yaml`. No direct DB connections are made.
* **Google Gemini / AI Service:** No Gemini SDKs or direct HTTP calls to `http://localhost:8000` exist.
* **PayPal / Resend:** No payment or email SDKs are present.
* **Conclusion:** The Flutter application **strictly adheres** to the mandate that **ASP.NET Core Web API is the single public gateway**.

### 6.2 Endpoint Inventory & Gap Analysis

| Business Domain | Mobile Service Call | Backend Endpoint | Status / Issue |
| :--- | :--- | :--- | :--- |
| **Authentication** | None | `POST /api/auth/login`<br>`POST /api/auth/register` | **Missing in Flutter** |
| **Policies** | `PolicyService.getPolicies(id)` | `GET /api/policies/policyholder/{id}` | Works, but `GET /api/policies/my` is cleaner and avoids passing IDs manually. |
| **Policies** | `PolicyService.getCoverage(id)` | `GET /api/policies/{id}/coverage` | Works. |
| **Policies** | None | `POST /api/policies/{id}/renew` | **Missing in Flutter.** |
| **Claims** | `ClaimService.createClaim()` | `POST /api/claims` | Works. |
| **Claims** | `ClaimService.getMyClaims()` | `GET /api/claims/my-claims` | Works. |
| **Claims** | `ClaimService.submitClaim(id)` | `POST /api/claims/{id}/submit` | Works. |
| **Claims** | `ClaimService.uploadDocument()` | `POST /api/claims/{id}/documents` | Works for images only. |
| **Claims** | None | `POST /api/claims/{id}/withdraw` | **Missing in Flutter.** |
| **Claims** | None | `DELETE /api/claims/{id}` | **Missing in Flutter.** |
| **Claims** | None | `GET /api/claims/{id}/document-requirements` | **Missing in Flutter.** |
| **Risk Assessment** | `RiskService.getReviewStatus(id)` | `GET /api/riskassessments/{id}/status` | **Fails with 403 Forbidden** due to controller authorization. |
| **Risk Assessment** | `RiskService.getReviewHistory()` | None | Unimplemented; returns `[]`. |
| **Payouts** | `PayoutService.getPayoutByClaimId(id)`| `GET /api/payouts/claim/{id}` | Works. |
| **Payouts** | `PayoutService.getPayoutHistory()` | `GET /api/payouts/history` | **Fails with 403 Forbidden** (calls staff endpoint instead of `/api/payouts/my`). |
| **Notifications** | None | `GET /api/notifications` | **Missing in Flutter.** |

---

## 7. Compliance Against SE3090 Section 8 Requirements

| Requirement Area | Current Implementation Status | Evaluation & Identified Gaps |
| :--- | :--- | :--- |
| **1. Reusable Widgets** | **Absent** (0%) | `lib/widgets/` is completely empty. Status badges, loading spinners, error cards, and detail rows are copy-pasted across 6+ screens. Needs reusable widgets: `StatusBadge`, `ErrorRetryCard`, `EmptyStateView`, `AppTextField`. |
| **2. Navigation** | **Deficient** (25%) | Only 4 routes registered in `main.dart`. No bottom navigation shell (`NavigationBar`). 8 screens are unroutable. No auth guard. Uses raw string casting for route arguments. |
| **3. State Management** | **Deficient** (10%) | `lib/providers/` is empty. No state management package in `pubspec.yaml`. All state is localized in `StatefulWidget` instances. `ADR-002` is an unfilled template. |
| **4. Form Validation** | **Partial** (40%) | Basic validation in `SubmitClaimScreen` (non-empty strings, positive amounts). No real-time feedback, no policy limits check, no auth form validations, manual typing required for policy GUIDs. |
| **5. Search & Filtering** | **Absent** (0%) | No search bars or filter chips exist on `ClaimHistoryScreen`, `PoliciesScreen`, or `PayoutHistoryScreen`. Backend query parameters (`?status=...&search=...`) are completely ignored. |
| **6. Business Transactions**| **Partial** (45%) | Claim creation and submission works in dev mode. Policy renewal, claim withdrawal, draft deletion, and document deletion are missing. |
| **7. Status History** | **Partial** (35%) | Visual timeline in `ClaimStatusScreen` is static and hardcoded. No timestamps or actual audit log records. Notification history feed is completely unbuilt. |
| **8. Responsive Design & UI** | **Basic** (30%) | Material 3 enabled with seed color indigo. However, layout is generic, without adaptive breakpoints, animations, skeleton loaders, or custom branding. |
| **9. Error Handling** | **Basic** (40%) | `ApiException` parses HTTP error codes, but 401s do not log out users, multi-step claim submissions have no retry mechanism, and network timeouts are basic. |
| **10. Agentic AI Integration**| **Partial** (30%) | Document verification status badges (`Verified`, `Flagged`, `Rejected`) are displayed on `ClaimDetailsScreen`. Policyholder-safe status is respected in models, but live AI workflow tracking is unintegrated and risk status returns 403. |

---

## 8. Cross-Platform End-to-End Workflow Verification

Cross-platform workflow evaluation from submission to payout:

```
[Flutter App] ──(1. Submit Claim + Evidence)──► [ASP.NET Core API] ──► [Neon PostgreSQL]
                                                        │
                                                        ▼
                                             [Agentic AI Service]
                                             (Doc Verif + Risk + Safety)
                                                        │
[React Dashboard] ◄──(3. Adjuster Reviews & Approves)───┘
       │
       ▼
[ASP.NET Core API] ──(4. Payout Calculation & Approval)──► [Neon DB]
       │
       ▼
[Flutter App] ◄──(5. View Live Status & Payout)─── (BLOCKED BY GAPS)
```

### Detailed Evaluation of the Flow:
1. **Stage 1 (Flutter: Claim Submission):** **Partially Functional.** A policyholder can submit a claim in development mode using the dev header backdoor, but only with images and a manually typed Policy ID.
2. **Stage 2 (ASP.NET Core: Storage & AI Ingestion):** **Functional.** The backend persists the claim and documents, dispatches notifications, and triggers the Document Verification Agent.
3. **Stage 3 (React: Staff Review & Approval):** **Functional.** Adjusters and Underwriters can view the submitted claim, review documents, run risk assessments, and approve/reject claims.
4. **Stage 4 (ASP.NET Core: Payout Calculation):** **Functional.** Adjuster calculates payout proposal, which is persisted in Neon PostgreSQL.
5. **Stage 5 (Flutter: Status & Payout Reflection):** **Broken / Blocked.**
   - In `ClaimDetailsScreen`, the policyholder can see the status badge update to `Approved`.
   - However, the policyholder **cannot navigate to the payout details** from the claim screen.
   - If the policyholder attempts to view `PayoutHistoryScreen`, the backend responds with **403 Forbidden**.
   - If the policyholder checks `ClaimRiskStatusScreen`, the backend responds with **403 Forbidden**.
   - The policyholder never receives an in-app notification because notifications are not implemented.

---

## 9. Android Build Configuration & APK Readiness

Inspecting `mobile/android/`:

### 9.1 Build Configuration Status
* **Gradle & Kotlin Versions:**
  - Gradle Wrapper: `9.3.1-all` (`gradle-wrapper.properties`)
  - Android Gradle Plugin: `com.android.application` version `9.1.0` (`settings.gradle.kts`)
  - Kotlin Android: `2.4.0` (`settings.gradle.kts`)
  - Java Target: Java 17 Compatibility configured in `app/build.gradle.kts`
  - Namespace & App ID: `com.insuranceclaims.insurance_claims_mobile`

### 9.2 Critical Deficiencies Preventing Runnable APK
1. **Missing Internet Permission in Manifest:**
   - In `mobile/android/app/src/main/AndroidManifest.xml`, there is **no `<uses-permission android:name="android.permission.INTERNET"/>`**.
   - Any APK built will crash or throw `SocketException: Permission denied` on all network requests.
2. **Missing Cleartext HTTP Traffic Support:**
   - Android 9+ (API 28+) blocks unencrypted HTTP traffic by default.
   - For local development with `http://10.0.2.2:5000/api`, `<application>` must specify `android:usesCleartextTraffic="true"` or declare a `network_security_config.xml`.
3. **Camera & Storage Hardware Permissions:**
   - Missing `<uses-permission android:name="android.permission.CAMERA"/>` for taking photos on older/specific Android OEMs.
4. **Missing Local Android SDK:**
   - `flutter doctor` confirms that no Android SDK is installed at `/Users/kinukshan/Library/Android/sdk`, and `local.properties` lacks `sdk.dir`.
   - Android Command Line Tools or Android Studio must be installed, or the app tested on macOS/Chrome platforms until the Android SDK is configured.

---

## 10. Prioritized Implementation Plan

Building on top of the existing codebase without rewriting, here is the structured, prioritized implementation roadmap to achieve full SE3090 compliance:

```
Phase 1: Foundations & Security ──► Phase 2: Navigation & Shared Widgets
                │                                    │
                ▼                                    ▼
Phase 3: Claim Submission & Files ─► Phase 4: Full Cross-Platform Integration
                │                                    │
                ▼                                    ▼
Phase 5: Search, Filtering & Polishing ──► Phase 6: Testing & Build Verification
```

### Phase 1: Security, Auth & State Management Foundations
* **Dependencies:** Add `provider` (or Riverpod as per ADR-002), `flutter_secure_storage`, and `flutter_dotenv` to `pubspec.yaml`.
* **Auth Service & Models:**
  - Complete `User` serialization.
  - Implement login, register, and token refresh in `auth_service.dart`.
  - Update `api_service.dart` to automatically read JWT from secure storage and inject `Authorization: Bearer <token>`.
* **Auth Screens:**
  - Build out `login_screen.dart` and `register_screen.dart` with responsive forms, validation, and loading indicators.
  - Add session management (`AuthProvider`) to notify listeners on login/logout.

### Phase 2: Shell Navigation & Reusable Widget Library
* **Main Navigation Shell:**
  - Implement a persistent `ScaffoldWithNavBar` or bottom navigation bar connecting:
    1. **Home** (Metrics, quick actions, active alerts)
    2. **My Policies** (Connecting `policies_screen.dart`)
    3. **Claims** (Connecting `claim_history_screen.dart`)
    4. **Payouts** (Connecting `payout_history_screen.dart`)
    5. **Notifications** (New notification history feed)
* **Design System & Reusable Widgets in `lib/widgets/`:**
  - `StatusBadge`: Consolidated status badge supporting Claims, Policies, Payouts, and Document Verification.
  - `ErrorRetryCard`: Standardized error view with retry callback.
  - `EmptyStateView`: Illustrated empty states.
  - `DetailRow`: Standardized key-value display row for details screens.

### Phase 3: Enhanced Claim Submission & Document Selection
* **Add `file_picker` Dependency:** Support selecting PDF and image evidence.
* **Refactor `SubmitClaimScreen`:**
  - Replace manual Policy ID field with an async dropdown populated from `GET /api/policies/my`.
  - Provide a document checklist selector matching the claim type (`Police Report`, `Repair Estimate`, `Photos of Damage`, etc.).
  - Allow labeling individual attached files with their respective document types.
  - Add draft preservation if a network error interrupts file upload.

### Phase 4: Cross-Platform & Backend Coordination Fixes
* **Fix Payout History Endpoint:**
  - In `payout_service.dart` (line 42), update `getPayoutHistory()` to call `/payouts/my` instead of `/payouts/history`.
* **Fix Risk Assessment Authorization:**
  - In backend `RiskAssessmentsController.cs` (line 191), ensure `GET {claimId}/status` is accessible to the `Policyholder` who owns the claim.
* **Add Notification Screen & Service:**
  - Implement `NotificationService` calling `GET /api/notifications`.
  - Build `NotificationHistoryScreen` allowing policyholders to view approval/status notifications.
* **Connect Claim Details to Payouts:**
  - On `ClaimDetailsScreen`, add a "View Payout" action card when status is `Approved` or `PayoutProcessing`.

### Phase 5: Search, Filter, Validation & Responsive Polish
* Add real-time text search and status filter chips to `ClaimHistoryScreen`, `PoliciesScreen`, and `PayoutHistoryScreen`.
* Implement claim withdrawal (`POST /api/claims/{id}/withdraw`) on `ClaimDetailsScreen`.
* Implement policy renewal on `PolicyDetailsScreen`.
* Enhance error handling with user-friendly SnackBars and connection retry logic.

### Phase 6: Android Build Setup & Comprehensive Test Suite
* Update `mobile/android/app/src/main/AndroidManifest.xml` with:
  ```xml
  <uses-permission android:name="android.permission.INTERNET"/>
  <uses-permission android:name="android.permission.CAMERA"/>
  ```
* Set `android:usesCleartextTraffic="true"` in `<application>`.
* Add unit and mock HTTP tests for all services (`AuthService`, `ClaimService`, `PayoutService`, `PolicyService`).
* Add widget tests for `LoginScreen`, `ClaimHistoryScreen`, and navigation flows.

---

## 11. Preservation Confirmation

The current git branch `fix/claims-adjuster-authorization` and all existing code across `backend/`, `frontend-web/`, `ai-service/`, and `mobile/` remain **fully preserved**. No files were deleted, stashed, reset, committed, or rewritten during this audit.
