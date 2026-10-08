import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/auth/login_screen.dart';
import 'package:insurance_claims_mobile/screens/auth/register_screen.dart';
import 'package:insurance_claims_mobile/screens/claims/submit_claim_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';

// ─────────────────────── Test Helpers ───────────────────────

/// Sets up a mock platform channel for FlutterSecureStorage in tests.
/// Copied from the existing test/authentication/auth_test.dart pattern.
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

/// Creates a test app wrapping the LoginScreen with necessary providers.
Widget _buildLoginTestApp({http.Client? client}) {
  final mockClient = client ?? MockClient((r) async => http.Response('', 404));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = apiService;
  final authService = AuthService(api: apiService);
  final authProvider = AuthProvider(authService: authService, apiService: apiService);

  return MaterialApp(
    home: ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: const LoginScreen(),
    ),
    routes: {
      '/register': (_) => ChangeNotifierProvider<AuthProvider>.value(
            value: authProvider,
            child: const RegisterScreen(),
          ),
    },
  );
}

/// Creates a test app wrapping the RegisterScreen with necessary providers.
Widget _buildRegisterTestApp({http.Client? client}) {
  final mockClient = client ?? MockClient((r) async => http.Response('', 404));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = apiService;
  final authService = AuthService(api: apiService);
  final authProvider = AuthProvider(authService: authService, apiService: apiService);

  return MaterialApp(
    home: ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: const RegisterScreen(),
    ),
    routes: {
      '/login': (_) => ChangeNotifierProvider<AuthProvider>.value(
            value: authProvider,
            child: const LoginScreen(),
          ),
    },
  );
}

/// Creates a test app wrapping the SubmitClaimScreen with necessary providers
/// and injected services so it doesn't hit the real backend.
Widget _buildSubmitClaimTestApp({http.Client? client}) {
  final mockClient = client ?? MockClient((r) async => http.Response('[]', 200));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  ApiService.shared = apiService;
  final authService = AuthService(api: apiService);
  final authProvider = AuthProvider(authService: authService, apiService: apiService);
  authProvider.setAuthenticatedUserForTesting(
    const User(
      id: 'user-test',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Policyholder',
    ),
  );

  return MaterialApp(
    home: ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: SubmitClaimScreen(
        claimService: ClaimService(apiService: apiService),
        policyService: PolicyService(apiService: apiService),
      ),
    ),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    setupMockSecureStorage();
  });

  // ═════════════════════════════════════════════════════════════════════
  // Login Form Validation Tests
  // ═════════════════════════════════════════════════════════════════════

  group('Login Form Validation', () {
    testWidgets('shows error when email is empty and form is submitted',
        (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      // Leave email empty, tap Sign In
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Email is required'), findsOneWidget);
    });

    testWidgets('shows error when password is empty and form is submitted',
        (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      // Enter valid email but leave password empty
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'user@example.com',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Password is required'), findsOneWidget);
    });

    testWidgets('shows error for invalid email format', (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'not-an-email',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'password123',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
    });

    testWidgets('shows error for email without domain', (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'user@',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
    });

    testWidgets('shows error for email without TLD', (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'user@domain',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
    });

    testWidgets('shows both errors when all fields are empty',
        (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
    });

    testWidgets('no validation errors with valid email and password',
        (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'user@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'ValidPassword123!',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      // No client-side validation errors shown
      expect(find.text('Email is required'), findsNothing);
      expect(find.text('Enter a valid email address'), findsNothing);
      expect(find.text('Password is required'), findsNothing);
    });

    testWidgets('email with only whitespace shows required error',
        (tester) async {
      await tester.pumpWidget(_buildLoginTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        '   ',
      );
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Email is required'), findsOneWidget);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // Registration Form Validation Tests
  // ═════════════════════════════════════════════════════════════════════

  group('Registration Form Validation', () {
    testWidgets('shows errors when all fields are empty', (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      // Scroll down to ensure Create Account button is visible
      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('First name is required'), findsOneWidget);
      expect(find.text('Last name is required'), findsOneWidget);
      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
      expect(find.text('Please confirm your password'), findsOneWidget);
    });

    testWidgets('shows error for invalid email format in registration',
        (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        'John',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Last Name'),
        'Doe',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'bademail',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
    });

    testWidgets('shows error when password is too short', (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        'John',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Last Name'),
        'Doe',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'john@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'short',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('Password must be at least 8 characters'), findsOneWidget);
    });

    testWidgets('shows error when passwords do not match', (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        'John',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Last Name'),
        'Doe',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'john@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'Password123!',
      );

      // Scroll to confirm password
      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        'DifferentPwd!',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('Passwords do not match'), findsOneWidget);
    });

    testWidgets('no validation errors with all valid registration fields',
        (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        'John',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Last Name'),
        'Doe',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'john@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'Password123!',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        'Password123!',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      // No validation errors should appear
      expect(find.text('First name is required'), findsNothing);
      expect(find.text('Last name is required'), findsNothing);
      expect(find.text('Email is required'), findsNothing);
      expect(find.text('Enter a valid email address'), findsNothing);
      expect(find.text('Password is required'), findsNothing);
      expect(find.text('Password must be at least 8 characters'), findsNothing);
      expect(find.text('Please confirm your password'), findsNothing);
      expect(find.text('Passwords do not match'), findsNothing);
    });

    testWidgets('first name with only whitespace shows required error',
        (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        '   ',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('First name is required'), findsOneWidget);
    });

    testWidgets('password exactly 8 chars passes validation', (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'First Name'),
        'John',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Last Name'),
        'Doe',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'),
        'john@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        '12345678',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Confirm Password'),
        '12345678',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      // 8-char password should pass the >= 8 check
      expect(find.text('Password must be at least 8 characters'), findsNothing);
    });

    testWidgets('password 7 chars fails validation', (tester) async {
      await tester.pumpWidget(_buildRegisterTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        '1234567',
      );

      await tester.scrollUntilVisible(
        find.text('Create Account').last,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Account').last);
      await tester.pumpAndSettle();

      expect(find.text('Password must be at least 8 characters'), findsOneWidget);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // Submit Claim Form Validation Tests
  // ═════════════════════════════════════════════════════════════════════

  group('Submit Claim Form Validation', () {
    testWidgets('shows validation errors when required fields are empty',
        (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      // Scroll to the bottom to find the submit button
      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      // Check for form validation errors
      // Policy ID, Location, Description, and Amount are all required
      expect(find.text('Policy ID is required'), findsOneWidget);
    });

    testWidgets('shows error for empty incident location', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      // Fill policy ID but leave location empty
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Location is required'), findsOneWidget);
    });

    testWidgets('shows error for empty description', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Colombo',
      );
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Description is required'), findsOneWidget);
    });

    testWidgets('shows error for empty claimed amount', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Colombo',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Description'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'),
        'Test description for the claim',
      );
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Amount is required'), findsOneWidget);
    });

    testWidgets('shows error for zero claimed amount', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Colombo',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Description'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'),
        'Damage to vehicle',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        '0',
      );

      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Must be greater than zero'), findsOneWidget);
    });

    testWidgets('shows error for negative claimed amount', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Colombo',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Description'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'),
        'Damage to vehicle',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        '-500',
      );

      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Must be greater than zero'), findsOneWidget);
    });

    testWidgets('shows error for non-numeric claimed amount', (tester) async {
      await tester.pumpWidget(_buildSubmitClaimTestApp());
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        'policy-123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Colombo',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Description'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'),
        'Damage to vehicle',
      );

      await tester.scrollUntilVisible(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        'abc',
      );

      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();

      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.scrollUntilVisible(
        submitButton,
        100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Must be greater than zero'), findsOneWidget);
    });
  });
}
