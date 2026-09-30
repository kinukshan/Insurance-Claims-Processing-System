import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'providers/auth_provider.dart';
import 'services/api_service.dart';
import 'services/auth_service.dart';
import 'screens/auth/login_screen.dart';
import 'screens/auth/register_screen.dart';
import 'screens/payout/payout_status_screen.dart';
import 'screens/claims/claim_history_screen.dart';
import 'screens/claims/submit_claim_screen.dart';
import 'screens/claims/claim_details_screen.dart';
import 'screens/claims/claim_status_screen.dart';
import 'screens/home/main_navigation_shell.dart';
import 'screens/policy/create_policy_screen.dart';
import 'screens/adjuster/adjuster_navigation_shell.dart';
import 'screens/underwriter/underwriter_navigation_shell.dart';
import 'screens/admin/admin_navigation_shell.dart';
import 'utils/app_theme.dart';
import 'widgets/shared_widgets.dart';

/// Insurance Claims Mobile App
/// Policyholder-facing application.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
///
/// State management: Provider (ChangeNotifier) for authentication.
///
/// MODIFICATION (Arulkumaran): Added named routes for claims screens.
/// MODIFICATION (Phase 1): Added authentication, Provider state management,
///   and auth-aware navigation.
/// MODIFICATION (Phase 2): Added Policyholder bottom navigation shell with
///   nested navigation across Home, Policies, Claims, Payouts, and Notifications.
/// MODIFICATION (Phase 4): Added PayoutStatus route and cross-platform integrations.
void main() {
  WidgetsFlutterBinding.ensureInitialized();
  FlutterError.onError = (details) {
    FlutterError.presentError(details);
    debugPrint('FLUTTER_ERROR_LOGCAT: ${details.exceptionAsString()}\n${details.stack}');
  };

  // Create shared service instances
  final apiService = ApiService.shared;
  final authService = AuthService(api: apiService);

  runApp(InsuranceClaimsApp(
    apiService: apiService,
    authService: authService,
  ));
}

class InsuranceClaimsApp extends StatelessWidget {
  final ApiService apiService;
  final AuthService authService;

  const InsuranceClaimsApp({
    super.key,
    required this.apiService,
    required this.authService,
  });

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<AuthProvider>(
      create: (_) => AuthProvider(
        authService: authService,
        apiService: apiService,
      )..restoreSession(), // Attempt session restoration on startup
      child: MaterialApp(
        title: 'Insurance Claims',
        theme: AppTheme.lightTheme,
        darkTheme: AppTheme.darkTheme,
        debugShowCheckedModeBanner: false,
        home: const AuthGate(),
        routes: {
          '/login': (context) => const LoginScreen(),
          '/register': (context) => const RegisterScreen(),
          '/home': (context) => const MainNavigationShell(initialIndex: 0),
          '/policies': (context) => const MainNavigationShell(initialIndex: 1),
          '/policies/create': (context) => const CreatePolicyScreen(),
          '/claims/history': (context) => const ClaimHistoryScreen(),
          '/claims/submit': (context) => const SubmitClaimScreen(),
          '/claims/details': (context) => const ClaimDetailsScreen(),
          '/claims/status': (context) => const ClaimStatusScreen(),
          '/payout/status': (context) => const PayoutStatusScreen(),
          '/payouts/status': (context) => const PayoutStatusScreen(),
          '/payouts': (context) => const MainNavigationShell(initialIndex: 3),
          '/notifications': (context) => const MainNavigationShell(initialIndex: 4),
        },
      ),
    );
  }
}

/// Authentication gate — controls navigation based on auth status.
///
/// Flow:
/// 1. Show loading screen during session restoration (AuthStatus.initial).
/// 2. If unauthenticated, show the login screen.
/// 3. If authenticated as Policyholder, show the main 5-tab navigation shell.
/// 4. If authenticated as another role (Admin, ClaimsAdjuster, etc.),
///    show the access-denied view — staff features are on the web portal.
class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<AuthProvider>(
      builder: (context, auth, _) {
        // 1. Initial state — restoring session
        if (auth.isInitial || (auth.isLoading && auth.status == AuthStatus.initial)) {
          return const AuthLoadingScreen();
        }

        // 2. Not authenticated — show login
        if (auth.isUnauthenticated) {
          return const LoginScreen();
        }

        // 3. Authenticated — route by role
        if (auth.isAuthenticated) {
          final user = auth.user!;

          // Policyholder → main 5-tab customer shell
          if (user.isPolicyholder) {
            return const MainNavigationShell();
          }

          // Claims Adjuster → 4-tab adjuster investigation shell
          if (user.isClaimsAdjuster) {
            return const AdjusterNavigationShell();
          }

          // Underwriter → 4-tab underwriter policy & approval desk shell
          if (user.isUnderwriter) {
            return const UnderwriterNavigationShell();
          }

          // Administrator → 5-tab operations command center & settlement shell
          if (user.isAdmin) {
            return const AdminNavigationShell();
          }

          // Unrecognized roles → access restricted fallback
          return AccessDeniedView(
            userRole: user.role,
            onLogout: () => auth.logout(),
          );
        }

        // Fallback
        return const LoginScreen();
      },
    );
  }
}
