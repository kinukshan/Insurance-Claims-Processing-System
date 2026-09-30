import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/screens/auth/login_screen.dart';
import 'package:insurance_claims_mobile/screens/auth/register_screen.dart';
import 'package:insurance_claims_mobile/widgets/shared_widgets.dart';

/// Helper to wrap a widget with all required providers for testing.
Widget _buildTestApp({
  required Widget child,
  required MockClient mockClient,
}) {
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  final authService = AuthService(api: apiService);
  return MaterialApp(
    home: ChangeNotifierProvider<AuthProvider>(
      create: (_) => AuthProvider(authService: authService, apiService: apiService),
      child: child,
    ),
    routes: {
      '/login': (context) => const LoginScreen(),
      '/register': (context) => const RegisterScreen(),
      '/claims/history': (context) => const Scaffold(body: Text('Claims')),
    },
  );
}

void main() {
  group('LoginScreen widget tests', () {
    testWidgets('renders email and password fields', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Sign In'), findsOneWidget);
    });

    testWidgets('renders branding elements', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      expect(find.text('Insurance Claims'), findsOneWidget);
      expect(find.text('Sign in to manage your claims'), findsOneWidget);
      expect(find.text('Welcome Back'), findsOneWidget);
    });

    testWidgets('shows validation errors on empty submit', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      // Tap Sign In without entering anything
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
    });

    testWidgets('shows email format validation error', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      // Enter invalid email
      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'notanemail');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
    });

    testWidgets('password visibility toggle works', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      // Initially password is obscured
      final passwordField = find.widgetWithText(TextFormField, 'Password');
      expect(passwordField, findsOneWidget);

      // Find and tap visibility toggle
      final toggleButton = find.byIcon(Icons.visibility_off_outlined);
      expect(toggleButton, findsOneWidget);

      await tester.tap(toggleButton);
      await tester.pump();

      // After toggle, should show visibility icon
      expect(find.byIcon(Icons.visibility_outlined), findsOneWidget);
    });

    testWidgets('shows loading state during login', (tester) async {
      final completer = Completer<http.Response>();
      final mockClient = MockClient((request) => completer.future);

      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'kasun@example.com');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'TestPass123');

      await tester.tap(find.text('Sign In'));
      await tester.pump(); // Start the async operation

      // Should show loading indicator
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      // Complete the request to settle cleanly
      completer.complete(http.Response(jsonEncode({'error': 'Login error'}), 400));
      await tester.pump();
    });

    testWidgets('shows server error message on failed login', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid email or password.'}),
          401,
        );
      });

      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'wrong@example.com');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'wrongpass');

      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      // Should display error banner
      expect(find.byType(ErrorBanner), findsOneWidget);
      expect(find.textContaining('Invalid email or password'), findsOneWidget);
    });

    testWidgets('has register navigation link', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const LoginScreen(),
        mockClient: mockClient,
      ));

      expect(find.text("Don't have an account? "), findsOneWidget);
      expect(find.text('Create Account'), findsOneWidget);
    });
  });

  group('RegisterScreen widget tests', () {
    testWidgets('renders all registration fields', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      expect(find.text('First Name'), findsOneWidget);
      expect(find.text('Last Name'), findsOneWidget);
      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Confirm Password'), findsOneWidget);
      expect(find.text('Create Account'), findsWidgets); // Header + button
    });

    testWidgets('renders Policyholder registration subtitle', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      expect(find.text('Register as a Policyholder'), findsOneWidget);
    });

    testWidgets('shows validation errors on empty submit', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      // Find and tap Create Account button
      final button = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();

      expect(find.text('First name is required'), findsOneWidget);
      expect(find.text('Last name is required'), findsOneWidget);
      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
      expect(find.text('Please confirm your password'), findsOneWidget);
    });

    testWidgets('shows password length validation', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      // Fill in short password
      await tester.enterText(find.widgetWithText(TextFormField, 'First Name'), 'Test');
      await tester.enterText(find.widgetWithText(TextFormField, 'Last Name'), 'User');
      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'test@example.com');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'short');
      await tester.enterText(find.widgetWithText(TextFormField, 'Confirm Password'), 'short');

      final button = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();

      expect(find.text('Password must be at least 8 characters'), findsOneWidget);
    });

    testWidgets('shows password mismatch validation', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      await tester.enterText(find.widgetWithText(TextFormField, 'First Name'), 'Test');
      await tester.enterText(find.widgetWithText(TextFormField, 'Last Name'), 'User');
      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'test@example.com');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'TestPass123');
      await tester.enterText(find.widgetWithText(TextFormField, 'Confirm Password'), 'Different1');

      final button = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();

      expect(find.text('Passwords do not match'), findsOneWidget);
    });

    testWidgets('shows server error on duplicate email', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'An account with this email already exists.'}),
          400,
        );
      });

      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      await tester.enterText(find.widgetWithText(TextFormField, 'First Name'), 'Kasun');
      await tester.enterText(find.widgetWithText(TextFormField, 'Last Name'), 'Perera');
      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'existing@example.com');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'TestPass123');
      await tester.enterText(find.widgetWithText(TextFormField, 'Confirm Password'), 'TestPass123');

      final button = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();

      expect(find.byType(ErrorBanner), findsOneWidget);
      expect(find.textContaining('already exists'), findsOneWidget);
    });

    testWidgets('has login navigation link', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      expect(find.text('Already have an account? '), findsOneWidget);
      expect(find.text('Sign In'), findsOneWidget);
    });

    testWidgets('does not expose role selection', (tester) async {
      final mockClient = MockClient((r) async => http.Response('', 200));
      await tester.pumpWidget(_buildTestApp(
        child: const RegisterScreen(),
        mockClient: mockClient,
      ));

      // No role dropdown or role input should exist
      expect(find.text('Role'), findsNothing);
      expect(find.text('Admin'), findsNothing);
      expect(find.text('ClaimsAdjuster'), findsNothing);
      expect(find.text('Underwriter'), findsNothing);
    });
  });

  group('AuthLoadingScreen widget tests', () {
    testWidgets('renders branding and loading indicator', (tester) async {
      await tester.pumpWidget(const MaterialApp(home: AuthLoadingScreen()));

      expect(find.text('Insurance Claims'), findsOneWidget);
      expect(find.text('Secure Claims Management'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.byIcon(Icons.shield_outlined), findsOneWidget);
    });
  });

  group('AccessDeniedView widget tests', () {
    testWidgets('renders access denied message', (tester) async {
      await tester.pumpWidget(const MaterialApp(
        home: AccessDeniedView(userRole: 'Admin'),
      ));

      expect(find.text('Access Restricted'), findsWidgets);
      expect(find.text('Logged in as: Admin'), findsOneWidget);
      expect(find.byIcon(Icons.lock_outline), findsOneWidget);
    });

    testWidgets('shows logout button when callback provided', (tester) async {
      bool loggedOut = false;
      await tester.pumpWidget(MaterialApp(
        home: AccessDeniedView(
          userRole: 'ClaimsAdjuster',
          onLogout: () => loggedOut = true,
        ),
      ));

      final logoutButton = find.text('Log Out');
      expect(logoutButton, findsOneWidget);

      await tester.tap(logoutButton);
      expect(loggedOut, isTrue);
    });
  });

  group('Protected navigation', () {
    testWidgets('AuthGate shows loading screen during initial state', (tester) async {
      // AuthProvider starts in initial state — the gate should show loading
      final completer = Completer<http.Response>();
      final mockClient = MockClient((request) => completer.future);

      final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: apiService);

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider<AuthProvider>(
            create: (_) => AuthProvider(authService: authService, apiService: apiService)
              ..restoreSession(),
            child: Consumer<AuthProvider>(
              builder: (context, auth, _) {
                if (auth.isInitial || (auth.isLoading && auth.status == AuthStatus.initial)) {
                  return const AuthLoadingScreen();
                }
                if (auth.isUnauthenticated) return const Text('LOGIN');
                return const Text('AUTHENTICATED');
              },
            ),
          ),
        ),
      );

      // Should show loading screen
      expect(find.byType(AuthLoadingScreen), findsOneWidget);

      completer.complete(http.Response('', 404));
      await tester.pump();
    });
  });
}
