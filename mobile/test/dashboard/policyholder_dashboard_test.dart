import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/home/home_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';
import 'package:insurance_claims_mobile/widgets/shared_widgets.dart';

void main() {
  final samplePolicy = {
    'id': 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'policyNumber': 'POL-2026-001',
    'policyHolderId': '11111111-1111-1111-1111-111111111111',
    'policyTypeId': 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'policyTypeName': 'Comprehensive Auto Insurance',
    'insuranceClass': 'NonLife',
    'startDate': '2026-01-01T00:00:00Z',
    'expiryDate': '2027-01-01T00:00:00Z',
    'coverageLimit': 2500000.0,
    'premium': 65000.0,
    'deductible': 25000.0,
    'status': 'Active',
    'renewalStatus': 'NotRenewed',
    'isExpired': false,
    'exclusions': null,
    'coverages': [],
  };

  final sampleClaim = {
    'id': 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    'claimNumber': 'CLM-2026-001',
    'policyId': 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'policyNumber': 'POL-2026-001',
    'policyHolderId': '11111111-1111-1111-1111-111111111111',
    'claimType': 'Auto',
    'status': 'Submitted',
    'incidentDate': '2026-09-20T10:00:00Z',
    'incidentLocation': 'Colombo 03',
    'description': 'Minor front bumper damage',
    'claimedAmount': 75000.0,
    'approvedAmount': null,
    'createdAt': '2026-09-21T08:00:00Z',
    'updatedAt': null,
    'documents': [],
  };

  final samplePayout = {
    'id': 'dddddddd-dddd-dddd-dddd-dddddddddddd',
    'claimId': 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    'approvedClaimAmount': 50000.0,
    'coverageLimit': 2500000.0,
    'deductible': 25000.0,
    'proposedPayout': 50000.0,
    'finalPayout': 50000.0,
    'status': 'Approved',
    'statusDisplay': 'Approved',
    'createdAt': '2026-09-22T00:00:00Z',
    'updatedAt': '2026-09-23T00:00:00Z',
  };

  group('PolicyService & PayoutService Tests', () {
    test('PolicyService.getMyPolicies calls /policies/my', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/policies/my'));
        expect(request.method, 'GET');
        return http.Response(jsonEncode([samplePolicy]), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = PolicyService(apiService: api);

      final policies = await service.getMyPolicies();
      expect(policies.length, 1);
      expect(policies.first.policyNumber, 'POL-2026-001');
      expect(policies.first.status, 'Active');
    });

    test('PayoutService.getMyPayouts calls /payouts/my', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/payouts/my'));
        expect(request.method, 'GET');
        return http.Response(
          jsonEncode({
            'items': [samplePayout],
            'page': 1,
            'pageSize': 20,
            'totalCount': 1,
            'totalPages': 1,
          }),
          200,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = PayoutService(apiService: api);

      final payouts = await service.getMyPayouts();
      expect(payouts.length, 1);
      expect(payouts.first.status, 'Approved');
      expect(payouts.first.finalPayout, 50000.0);
    });
  });

  group('HomeScreen Dashboard Widget Tests', () {
    Widget createDashboardTestApp({
      required PolicyService policyService,
      required ClaimService claimService,
      ValueChanged<int>? onNavigateTab,
    }) {
      final mockApi = ApiService(baseUrl: 'http://localhost/api');
      final authService = AuthService(api: mockApi);
      final authProvider = AuthProvider(
        authService: authService,
        apiService: mockApi,
      );

      // Authenticate as test policyholder
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
          child: HomeScreen(
            policyService: policyService,
            claimService: claimService,
            onNavigateTab: onNavigateTab,
          ),
        ),
      );
    }

    testWidgets('renders welcome banner with policyholder greeting and badge', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicy]), 200);
        }
        if (request.url.path.endsWith('/claims/my-claims')) {
          return http.Response(jsonEncode([sampleClaim]), 200);
        }
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(createDashboardTestApp(
        policyService: policyService,
        claimService: claimService,
      ));

      await tester.pumpAndSettle();

      // Check banner elements
      expect(find.text('Welcome back, Kasun'), findsOneWidget);
      expect(find.text('POLICYHOLDER'), findsOneWidget);
      expect(find.text('Dashboard'), findsOneWidget);
    });

    testWidgets('renders metric cards with real counts', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicy]), 200);
        }
        if (request.url.path.endsWith('/claims/my-claims')) {
          return http.Response(jsonEncode([sampleClaim]), 200);
        }
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(createDashboardTestApp(
        policyService: policyService,
        claimService: claimService,
      ));

      await tester.pumpAndSettle();

      // Metric card titles and values
      expect(find.text('Active Policies'), findsWidgets);
      expect(find.text('Active Claims'), findsOneWidget);
      expect(find.text('1 total'), findsNWidgets(2)); // 1 total for policies and 1 total for claims
    });

    testWidgets('renders quick action buttons', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response('[]', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(createDashboardTestApp(
        policyService: policyService,
        claimService: claimService,
      ));

      await tester.pumpAndSettle();

      expect(find.text('Quick Actions'), findsOneWidget);
      expect(find.text('File Claim'), findsOneWidget);
      expect(find.text('Policies'), findsOneWidget);
      expect(find.text('Claims'), findsOneWidget);
      expect(find.text('Payouts'), findsOneWidget);
    });

    testWidgets('renders recent claims and active policies with status badges', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicy]), 200);
        }
        if (request.url.path.endsWith('/claims/my-claims')) {
          return http.Response(jsonEncode([sampleClaim]), 200);
        }
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(createDashboardTestApp(
        policyService: policyService,
        claimService: claimService,
      ));

      await tester.pumpAndSettle();

      // Section titles
      expect(find.text('Recent Claims'), findsOneWidget);
      expect(find.text('Active Policies'), findsWidgets);

      // Claim and Policy entries
      expect(find.text('CLM-2026-001'), findsOneWidget);
      expect(find.text('POL-2026-001'), findsOneWidget);

      // Status badges
      expect(find.byType(StatusBadge), findsWidgets);
    });

    testWidgets('calls onNavigateTab when tapping View All or quick actions', (tester) async {
      int? navigatedTab;
      final mockClient = MockClient((request) async {
        return http.Response('[]', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(createDashboardTestApp(
        policyService: policyService,
        claimService: claimService,
        onNavigateTab: (index) => navigatedTab = index,
      ));

      await tester.pumpAndSettle();

      // Tap "Policies" quick action
      await tester.tap(find.text('Policies'));
      expect(navigatedTab, 1);

      // Tap "Payouts" quick action
      await tester.tap(find.text('Payouts'));
      expect(navigatedTab, 3);
    });
  });
}
