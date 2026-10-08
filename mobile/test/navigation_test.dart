import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:insurance_claims_mobile/main.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/auth/login_screen.dart';
import 'package:insurance_claims_mobile/screens/auth/register_screen.dart';
import 'package:insurance_claims_mobile/screens/home/main_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/adjuster/adjuster_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/underwriter/underwriter_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/admin/admin_navigation_shell.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/widgets/shared_widgets.dart';

// ─────────────────────── Test Helpers ───────────────────────

/// Sets up a mock platform channel for FlutterSecureStorage in tests.
void setupMockSecureStorage() {
  final storage = <String, String>{};

  TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
      .setMockMethodCallHandler(
    const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
    (MethodCall methodCall) async {
      switch (methodCall.method) {
        case 'write':
          final args = methodCall.arguments as Map;
          storage[args['key'] as String] = args['value'] as String;
          return null;
        case 'read':
          final args = methodCall.arguments as Map;
          return storage[args['key'] as String];
        case 'delete':
          final args = methodCall.arguments as Map;
          storage.remove(args['key'] as String);
          return null;
        case 'readAll':
          return storage;
        case 'deleteAll':
          storage.clear();
          return null;
        case 'containsKey':
          final args = methodCall.arguments as Map;
          return storage.containsKey(args['key'] as String);
        default:
          return null;
      }
    },
  );
}

/// Builds the full app with InsuranceClaimsApp and AuthGate for testing
/// named-route navigation as it really works in the app.
Widget _buildFullApp({http.Client? client}) {
  final mockClient = client ?? MockClient((r) async => http.Response('', 404));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = apiService;
  final authService = AuthService(api: apiService);

  return InsuranceClaimsApp(
    apiService: apiService,
    authService: authService,
  );
}

/// Builds an auth-gated test app with a pre-authenticated user.
Widget _buildAuthTestApp({required User user, http.Client? client}) {
  final mockClient = client ?? MockClient((r) async => http.Response('[]', 200));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = apiService;
  final authService = AuthService(api: apiService);
  final authProvider = AuthProvider(authService: authService, apiService: apiService);
  authProvider.setAuthenticatedUserForTesting(user);

  return MultiProvider(
    providers: [
      ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
      Provider<ApiService>.value(value: apiService),
    ],
    child: const MaterialApp(home: AuthGate()),
  );
}

/// Builds a test app wrapping the MainNavigationShell for authenticated Policyholders.
Widget _buildNavigationShellTestApp({
  int initialIndex = 0,
  http.Client? client,
}) {
  final mockClient = client ?? MockClient((r) async => http.Response('[]', 200));
  final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = api;
  final authService = AuthService(api: api);
  final authProvider = AuthProvider(authService: authService, apiService: api);
  authProvider.setAuthenticatedUserForTesting(
    const User(
      id: '11111111-1111-1111-1111-111111111111',
      email: 'kasun@test.com',
      firstName: 'Kasun',
      lastName: 'Perera',
      role: 'Policyholder',
    ),
  );

  return MaterialApp(
    home: ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: MainNavigationShell(initialIndex: initialIndex),
    ),
  );
}

Finder navItem(String label) => find.descendant(
      of: find.byType(BottomNavigationBar),
      matching: find.text(label),
    );

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    setupMockSecureStorage();
  });

  // ═════════════════════════════════════════════════════════════════════
  // AuthGate Role-Based Navigation
  // ═════════════════════════════════════════════════════════════════════

  group('AuthGate Role-Based Navigation', () {
    testWidgets('unauthenticated user sees LoginScreen', (tester) async {
      await tester.pumpWidget(_buildFullApp());
      // Pump multiple times to let session restoration complete (returns 404)
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsOneWidget);
    });

    testWidgets('Policyholder is routed to MainNavigationShell',
        (tester) async {
      final user = const User(
        id: 'user-ph',
        email: 'holder@test.com',
        firstName: 'Jane',
        lastName: 'Doe',
        role: 'Policyholder',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();

      expect(find.byType(MainNavigationShell), findsOneWidget);
    });

    testWidgets('ClaimsAdjuster is routed to AdjusterNavigationShell',
        (tester) async {
      final user = const User(
        id: 'user-adj',
        email: 'adjuster@test.com',
        firstName: 'Alex',
        lastName: 'Adjuster',
        role: 'ClaimsAdjuster',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();

      expect(find.byType(AdjusterNavigationShell), findsOneWidget);
    });

    testWidgets('Underwriter is routed to UnderwriterNavigationShell',
        (tester) async {
      final user = const User(
        id: 'user-uw',
        email: 'underwriter@test.com',
        firstName: 'Sam',
        lastName: 'Underwriter',
        role: 'Underwriter',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();

      expect(find.byType(UnderwriterNavigationShell), findsOneWidget);
    });

    testWidgets('Admin is routed to AdminNavigationShell', (tester) async {
      final user = const User(
        id: 'user-admin',
        email: 'admin@test.com',
        firstName: 'Chief',
        lastName: 'Admin',
        role: 'Admin',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();

      expect(find.byType(AdminNavigationShell), findsOneWidget);
    });

    testWidgets('unrecognized role shows AccessDeniedView', (tester) async {
      final user = const User(
        id: 'user-unknown',
        email: 'mystery@test.com',
        firstName: 'Mystery',
        lastName: 'User',
        role: 'UnknownRole',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();

      expect(find.byType(AccessDeniedView), findsOneWidget);
      expect(find.text('Access Restricted'), findsWidgets);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // MainNavigationShell Bottom Tab Navigation
  // ═════════════════════════════════════════════════════════════════════

  group('MainNavigationShell Bottom Tab Navigation', () {
    testWidgets('renders all 5 bottom navigation tabs', (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      expect(find.byType(BottomNavigationBar), findsOneWidget);
      expect(navItem('Home'), findsOneWidget);
      expect(navItem('Policies'), findsOneWidget);
      expect(navItem('Claims'), findsOneWidget);
      expect(navItem('Payouts'), findsOneWidget);
      expect(navItem('Alerts'), findsOneWidget);
    });

    testWidgets('starts on Home tab by default (initialIndex: 0)',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp(initialIndex: 0));
      await tester.pumpAndSettle();

      // Home tab shows the Dashboard heading
      expect(find.text('Dashboard'), findsOneWidget);
    });

    testWidgets('tapping Policies tab shows My Policies screen',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Policies'));
      await tester.pumpAndSettle();

      expect(find.text('My Policies'), findsOneWidget);
    });

    testWidgets('tapping Claims tab shows Claim History screen',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Claims'));
      await tester.pumpAndSettle();

      expect(find.text('Claim History'), findsOneWidget);
    });

    testWidgets('tapping Payouts tab shows Payout History screen',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Payouts'));
      await tester.pumpAndSettle();

      expect(find.text('Payout History'), findsOneWidget);
    });

    testWidgets('tapping Alerts tab shows Notifications screen',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Alerts'));
      await tester.pumpAndSettle();

      expect(find.text('Notifications'), findsOneWidget);
    });

    testWidgets('switching between tabs preserves state via IndexedStack',
        (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp());
      await tester.pumpAndSettle();

      // Verify IndexedStack with 5 children
      expect(find.byType(IndexedStack), findsOneWidget);
      final stack = tester.widget<IndexedStack>(find.byType(IndexedStack));
      expect(stack.children.length, 5);

      // Navigate to Claims
      await tester.tap(navItem('Claims'));
      await tester.pumpAndSettle();
      expect(find.text('Claim History'), findsOneWidget);

      // Navigate back to Home
      await tester.tap(navItem('Home'));
      await tester.pumpAndSettle();
      expect(find.text('Dashboard'), findsOneWidget);
    });

    testWidgets('initialIndex: 1 starts on Policies tab', (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp(initialIndex: 1));
      await tester.pumpAndSettle();

      expect(find.text('My Policies'), findsOneWidget);
    });

    testWidgets('initialIndex: 3 starts on Payouts tab', (tester) async {
      await tester.pumpWidget(_buildNavigationShellTestApp(initialIndex: 3));
      await tester.pumpAndSettle();

      expect(find.text('Payout History'), findsOneWidget);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // Login-to-Register Navigation
  // ═════════════════════════════════════════════════════════════════════

  group('Login-Register Cross Navigation', () {
    testWidgets('tapping Create Account on login navigates to register',
        (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 404));
      final apiService = ApiService(
        baseUrl: 'http://localhost/api',
        client: mockClient,
      );
      ApiService.shared = apiService;
      final authService = AuthService(api: apiService);
      final authProvider = AuthProvider(
        authService: authService,
        apiService: apiService,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider<AuthProvider>.value(
            value: authProvider,
            child: const LoginScreen(),
          ),
          routes: {
            '/register': (_) => ChangeNotifierProvider<AuthProvider>.value(
                  value: authProvider,
                  child: const RegisterScreen(),
                ),
            '/login': (_) => ChangeNotifierProvider<AuthProvider>.value(
                  value: authProvider,
                  child: const LoginScreen(),
                ),
          },
        ),
      );
      await tester.pumpAndSettle();

      // Scroll down to make Create Account visible
      await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -300));
      await tester.pumpAndSettle();

      // Tap 'Create Account' text button
      await tester.tap(find.text('Create Account'));
      await tester.pumpAndSettle();

      // Should now show RegisterScreen content
      expect(find.byType(RegisterScreen), findsOneWidget);
      expect(find.text('Register as a Policyholder'), findsOneWidget);
    });

    testWidgets('tapping Sign In on register navigates to login',
        (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 404));
      final apiService = ApiService(
        baseUrl: 'http://localhost/api',
        client: mockClient,
      );
      ApiService.shared = apiService;
      final authService = AuthService(api: apiService);
      final authProvider = AuthProvider(
        authService: authService,
        apiService: apiService,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider<AuthProvider>.value(
            value: authProvider,
            child: const RegisterScreen(),
          ),
          routes: {
            '/login': (_) => ChangeNotifierProvider<AuthProvider>.value(
                  value: authProvider,
                  child: const LoginScreen(),
                ),
            '/register': (_) => ChangeNotifierProvider<AuthProvider>.value(
                  value: authProvider,
                  child: const RegisterScreen(),
                ),
          },
        ),
      );
      // Scroll down to make Sign In visible
      await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -300));
      await tester.pumpAndSettle();

      // Tap 'Sign In' text button on register screen
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      // Should now show LoginScreen content
      expect(find.byType(LoginScreen), findsOneWidget);
      expect(find.text('Sign in to manage your claims'), findsOneWidget);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // Named Routes
  // ═════════════════════════════════════════════════════════════════════

  group('Named Route Resolution', () {
    testWidgets('/login route resolves to LoginScreen', (tester) async {
      await tester.pumpWidget(_buildFullApp());
      await tester.pumpAndSettle();

      // The app should show LoginScreen for unauthenticated users at /login route.
      // We already verified it shows LoginScreen via AuthGate above.
      // Let's verify the named route by navigating to /login.
      final context = tester.element(find.byType(LoginScreen));
      Navigator.pushNamed(context, '/login');
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsOneWidget);
    });

    testWidgets('/register route resolves to RegisterScreen', (tester) async {
      await tester.pumpWidget(_buildFullApp());
      await tester.pumpAndSettle();

      final context = tester.element(find.byType(LoginScreen));
      Navigator.pushNamed(context, '/register');
      await tester.pumpAndSettle();

      expect(find.byType(RegisterScreen), findsOneWidget);
    });
  });
}
