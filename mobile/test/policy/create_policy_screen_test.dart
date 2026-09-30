import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/policy/create_policy_screen.dart';
import 'package:insurance_claims_mobile/screens/policy/policies_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';
import 'package:provider/provider.dart';

void main() {
  final samplePolicyTypes = [
    {
      'id': '22222222-2222-4222-8222-222222222221',
      'name': 'Motor Insurance',
      'description': 'Coverage for insured motor vehicles.',
      'defaultCoverageLimit': 500000.0,
      'defaultDeductible': 10000.0,
      'insuranceClass': 0,
      'insuranceClassCode': 'General',
      'insuranceClassName': 'General Insurance',
    },
    {
      'id': '22222222-2222-4222-8222-222222222222',
      'name': 'Health Insurance',
      'description': 'Medical expenses coverage.',
      'defaultCoverageLimit': 1000000.0,
      'defaultDeductible': 5000.0,
      'insuranceClass': 0,
      'insuranceClassCode': 'General',
      'insuranceClassName': 'General Insurance',
    },
    {
      'id': '22222222-2222-4222-8222-222222222224',
      'name': 'Life Insurance',
      'description': 'Long-term death benefit coverage.',
      'defaultCoverageLimit': 2000000.0,
      'defaultDeductible': 0.0,
      'insuranceClass': 1,
      'insuranceClassCode': 'LongTerm',
      'insuranceClassName': 'Long-Term Insurance',
    },
  ];

  final sampleCreatedPolicy = {
    'id': '99999999-9999-9999-9999-999999999999',
    'policyNumber': 'POL-NEW-2026',
    'policyholderId': 'holder-123',
    'policyTypeId': '22222222-2222-4222-8222-222222222221',
    'policyTypeName': 'Motor Insurance',
    'insuranceClass': 0,
    'insuranceClassCode': 'General',
    'insuranceClassName': 'General Insurance',
    'coverageLimit': 500000.0,
    'premium': 750.0,
    'deductible': 10000.0,
    'startDate': '2026-09-28T00:00:00Z',
    'expiryDate': '2027-09-28T00:00:00Z',
    'status': 'Draft',
    'renewalStatus': 'NotDue',
    'exclusions': null,
    'isExpired': false,
    'canRenew': false,
    'coverages': [],
    'createdAt': '2026-09-28T00:00:00Z',
    'updatedAt': '2026-09-28T00:00:00Z',
  };

  Widget buildTestWidget({
    required PolicyService policyService,
    String policyholderId = 'holder-123',
  }) {
    final mockClient = MockClient((_) async => http.Response('{}', 200));
    final apiService = ApiService(client: mockClient);
    final authService = AuthService(api: apiService);
    final authProvider = AuthProvider(
      authService: authService,
      apiService: apiService,
    );
    authProvider.setAuthenticatedUserForTesting(
      const User(
        id: 'holder-123',
        email: 'holder@test.com',
        firstName: 'John',
        lastName: 'Doe',
        role: 'Policyholder',
      ),
    );

    return ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: MaterialApp(
        home: CreatePolicyScreen(
          policyService: policyService,
          policyholderId: policyholderId,
        ),
      ),
    );
  }

  group('CreatePolicyScreen Widget Tests', () {
    testWidgets('renders loading state initially while products fetch', (tester) async {
      final mockClient = MockClient((request) async {
        // Return delayed or pending response
        await Future.delayed(const Duration(milliseconds: 50));
        return http.Response(jsonEncode(samplePolicyTypes), 200, headers: {'content-type': 'application/json'});
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));

      expect(find.text('Loading insurance products...'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      await tester.pumpAndSettle();
      expect(find.widgetWithText(AppBar, 'Create Policy'), findsOneWidget);
    });

    testWidgets('renders error retry view when policy products load fails', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response('Internal Server Error', 500);
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));
      await tester.pumpAndSettle();

      expect(find.text('Failed to load policy products. Please check your connection and try again.'), findsOneWidget);
      expect(find.text('Try Again'), findsOneWidget);
    });

    testWidgets('renders form fields with default values for first product', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/policytypes')) {
          return http.Response(jsonEncode(samplePolicyTypes), 200, headers: {'content-type': 'application/json'});
        }
        return http.Response('Not Found', 404);
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));
      await tester.pumpAndSettle();

      // Form header and draft notice banner
      expect(find.text('Policy Creation Notice'), findsOneWidget);
      expect(
        find.textContaining('Newly created policies will be created in Draft status.'),
        findsOneWidget,
      );

      // Selected first product: Motor Insurance
      expect(find.text('Motor Insurance (5% deductible)'), findsOneWidget);
      expect(find.byKey(const Key('coverage_limit_field')), findsOneWidget);
      expect(find.text('500000'), findsOneWidget);

      // Percentage deductible: 5% for Motor
      expect(find.text('5% of approved claim'), findsOneWidget);
      expect(find.text('5% of eligible approved claim amount'), findsOneWidget);

      // Start and Expiry date pickers
      expect(find.byKey(const Key('start_date_picker')), findsOneWidget);
      expect(find.byKey(const Key('expiry_date_picker')), findsOneWidget);

      // Exclusions
      expect(find.byKey(const Key('exclusions_field')), findsOneWidget);

      // Submit button
      expect(find.widgetWithText(ElevatedButton, 'Create Policy'), findsOneWidget);
    });

    testWidgets('selecting Life Insurance updates deductible to 0% and shows rule note', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/policytypes')) {
          return http.Response(jsonEncode(samplePolicyTypes), 200, headers: {'content-type': 'application/json'});
        }
        return http.Response('Not Found', 404);
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));
      await tester.pumpAndSettle();

      // Open dropdown and select Life Insurance
      await tester.tap(find.byKey(const Key('policy_type_dropdown')));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Life Insurance (0% deductible)').last);
      await tester.pumpAndSettle();

      // Check updated coverage limit: 2000000
      expect(find.text('2000000'), findsOneWidget);

      // Check updated deductible: 0% for Life
      expect(find.text('0% of approved claim'), findsOneWidget);
      expect(find.text('0% of eligible approved claim amount'), findsOneWidget);
    });

    testWidgets('validates required coverage limit > 0', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/policytypes')) {
          return http.Response(jsonEncode(samplePolicyTypes), 200, headers: {'content-type': 'application/json'});
        }
        return http.Response('Not Found', 404);
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));
      await tester.pumpAndSettle();

      // Clear coverage limit
      await tester.enterText(find.byKey(const Key('coverage_limit_field')), '');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Create Policy'));
      await tester.pumpAndSettle();

      expect(find.text('Coverage limit is required'), findsOneWidget);

      // Enter 0
      await tester.enterText(find.byKey(const Key('coverage_limit_field')), '0');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Create Policy'));
      await tester.pumpAndSettle();

      expect(find.text('Coverage limit must be greater than zero'), findsOneWidget);
    });

    testWidgets('successful submission displays Draft status and activation guidance', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      bool postCalled = false;
      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/policytypes')) {
          return http.Response(jsonEncode(samplePolicyTypes), 200, headers: {'content-type': 'application/json'});
        }
        if (request.method == 'POST' && request.url.path.contains('/policies')) {
          postCalled = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          expect(body['coverageLimit'], 500000.0);
          expect(body['deductible'], 10000.0);
          return http.Response(jsonEncode(sampleCreatedPolicy), 201, headers: {'content-type': 'application/json'});
        }
        return http.Response('Not Found', 404);
      });
      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(buildTestWidget(policyService: policyService));
      await tester.pumpAndSettle();

      // Tap submit
      await tester.tap(find.widgetWithText(ElevatedButton, 'Create Policy'));
      await tester.pumpAndSettle();

      expect(postCalled, isTrue);

      // Verify success view
      expect(find.text('Policy Created Successfully!'), findsOneWidget);
      expect(find.text('Policy #POL-NEW-2026'), findsOneWidget);

      // Persisted status is Draft
      expect(find.text('Draft'), findsOneWidget);

      // Explains ineligibility for claims until admin activates
      expect(
        find.textContaining('This policy is currently in Draft status. It requires Administrator activation before claims can be submitted against it.'),
        findsOneWidget,
      );

      // Return button
      expect(find.byKey(const Key('return_to_policies_btn')), findsOneWidget);
    });
  });

  group('PoliciesScreen Navigation & Filter Integration Tests', () {
    testWidgets('PoliciesScreen displays Create Policy FAB and includes all backend statuses in filter', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/policies/my')) {
          return http.Response(
            jsonEncode([
              sampleCreatedPolicy, // Draft
              {
                ...sampleCreatedPolicy,
                'id': 'policy-active',
                'policyNumber': 'POL-ACTIVE-001',
                'status': 'Active',
              },
              {
                ...sampleCreatedPolicy,
                'id': 'policy-cancelled',
                'policyNumber': 'POL-CANCELLED-002',
                'status': 'Cancelled',
              },
            ]),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not Found', 404);
      });

      final apiService = ApiService(client: mockClient);
      final policyService = PolicyService(apiService: apiService);

      await tester.pumpWidget(MaterialApp(
        home: PoliciesScreen(policyService: policyService),
      ));
      await tester.pumpAndSettle();

      // Verify FAB exists
      expect(find.byKey(const Key('create_policy_fab')), findsOneWidget);
      expect(find.byKey(const Key('create_policy_appbar_btn')), findsOneWidget);

      // Verify all backend statuses in filter chips
      expect(find.widgetWithText(ChoiceChip, 'All'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, 'Active'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, 'Draft'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, 'Expired'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, 'Cancelled'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, 'Lapsed'), findsOneWidget);

      // Verify currency displays with 'LKR' rather than '$'
      expect(find.textContaining('Coverage: LKR 500000'), findsWidgets);
      expect(find.textContaining(r'$'), findsNothing);

      // Verify Draft ineligibility notice is displayed
      expect(
        find.text('Awaiting Admin Activation — Ineligible for claims'),
        findsOneWidget,
      );

      // Filter by Cancelled chip
      await tester.tap(find.widgetWithText(ChoiceChip, 'Cancelled'));
      await tester.pumpAndSettle();

      expect(find.text('POL-CANCELLED-002'), findsOneWidget);
      expect(find.text('POL-ACTIVE-001'), findsNothing);
      expect(find.text('POL-NEW-2026'), findsNothing);

      // Filter by Draft chip
      await tester.tap(find.widgetWithText(ChoiceChip, 'Draft'));
      await tester.pumpAndSettle();

      expect(find.text('POL-NEW-2026'), findsOneWidget);
      expect(find.text('POL-ACTIVE-001'), findsNothing);
    });
  });
}
