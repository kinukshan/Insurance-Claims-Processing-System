# ADR-002: Flutter State Management

## Status
Accepted

## Context
The Flutter mobile application (`mobile/`) provides the Policyholder client interface for the SE3090 Insurance Claims Processing System. The application must manage:
1. **Global Authentication & Session Lifecycle:** User registration, credential login, JWT token persistence across app launches via `flutter_secure_storage`, automatic session restoration on startup, reactive logout, and role-based route gating (ensuring only Policyholders access the mobile app while staff roles are guided to the web portal).
2. **Screen-Level Workflow States:** Policy listing, claim submission with multi-step document uploads and draft recovery, live claim status timeline tracking, payout calculation breakdowns, notification feeds, and real-time search/status filtering.

The state management architecture must be robust, maintainable, straightforward to test in automated CI/CD pipelines, and adhere strictly to Flutter architectural best practices without introducing extraneous boilerplate.

## Options Considered

### Option 1: Provider (`provider: ^6.1.2`)
* **Overview:** The Google-recommended community standard based on `InheritedWidget` and `ChangeNotifier`.
* **Pros:**
  - Lightweight with negligible runtime overhead and minimal boilerplate.
  - Idiomatic Flutter integration using `ChangeNotifierProvider`, `Consumer`, and `context.watch`/`context.read`.
  - Excellent testability: `AuthProvider` can be instantiated in test harnesses with mocked `AuthService` and `ApiService` without complex test scaffolding.
  - Clean separation: Global session state is isolated in `AuthProvider`, while localized UI states (e.g., form validation, search queries, filter selections) remain encapsulated within `StatefulWidget` controllers.
* **Cons:** Requires manual notification triggers (`notifyListeners()`) which, if mismanaged, could cause redundant widget rebuilds.

### Option 2: Riverpod
* **Overview:** A complete rewrite of Provider that eliminates Flutter context dependency.
* **Pros:** Compile-time safety, easy provider overrides in tests, no `BuildContext` required.
* **Cons:** Steeper learning curve, introduces non-standard `ConsumerWidget`/`ConsumerStatefulWidget` base classes throughout the UI hierarchy, and adds unnecessary complexity for an application with well-defined single-role scoping.

### Option 3: BLoC / Cubit (`flutter_bloc`)
* **Overview:** Formal stream-based state machine architecture separating events and states.
* **Pros:** Highly predictable state transitions, strict architectural enforcement.
* **Cons:** High ceremony and boilerplate (Event classes, State classes, Bloc classes) that disproportionately slows development for straightforward CRUD and status-polling screens.

### Option 4: GetX
* **Overview:** All-in-one framework offering reactive state, micro-navigation, and dependency injection.
* **Pros:** Minimal boilerplate.
* **Cons:** Global, context-less service locator anti-pattern; impairs deterministic testing, diverges from standard Flutter patterns, and complicates debugging.

## Decision
We adopted **Option 1: Provider (`ChangeNotifierProvider`)** for application-wide session and authentication state, paired with localized widget state for UI-specific flows.

### Architecture Breakdown
1. **Global Auth Layer:**
   - `AuthProvider` extends `ChangeNotifier` to manage `AuthStatus` (`initial`, `loading`, `authenticated`, `unauthenticated`, `error`).
   - Automatically attempts session restoration on app boot via `ApiService` and `AuthService`.
   - Injected at the root of `MaterialApp` via `ChangeNotifierProvider` in `mobile/lib/main.dart`.
   - `_AuthGate` uses `Consumer<AuthProvider>` to declaratively switch between `AuthLoadingScreen`, `LoginScreen`, `MainNavigationShell`, or `AccessDeniedView` (for staff accounts attempting mobile login).
2. **Local Screen States:**
   - Search queries, status filter chips, text input controllers, and pull-to-refresh workflows in `ClaimHistoryScreen`, `PoliciesScreen`, `PayoutHistoryScreen`, and `SubmitClaimScreen` utilize standard `StatefulWidget` lifecycles.
   - Services (`ClaimService`, `PolicyService`, `PayoutService`, `NotificationService`) are injected via constructors with default fallbacks to facilitate unit and widget testing.

## Consequences

### Positive
- **Simplicity & Maintainability:** The entire authentication and navigation flow is concise, readable, and standard Dart/Flutter code.
- **High Testability:** 174 automated Flutter widget and unit tests execute reliably in less than 3 seconds without mocking complex stream pipelines or global singletons.
- **Zero Overhead:** No heavy dependency trees or code generation requirements (`build_runner` is not required for state models).
- **Security Compliance:** Bearer tokens are kept in secure OS storage via `flutter_secure_storage` and automatically injected into outbound HTTP requests via `ApiService` headers.

### Negative / Trade-offs
- Developers must explicitly call `notifyListeners()` when modifying properties in `AuthProvider`.
- Multi-provider trees would become necessary if more global stores (e.g., app-wide theme switching or offline synchronization queues) were introduced in the future.
