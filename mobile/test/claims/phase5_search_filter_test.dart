import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';
import 'package:insurance_claims_mobile/screens/claims/claim_history_screen.dart';
import 'package:insurance_claims_mobile/screens/policy/policies_screen.dart';
import 'package:insurance_claims_mobile/screens/payout/payout_history_screen.dart';

void main() {
  final sampleClaim1 = {
    'id': 'claim-1',
    'policyId': 'policy-1',
    'policyNumber': 'POL-001',
    'policyHolderId': 'user-1',
    'claimNumber': 'CLM-001',
    'claimType': 'Auto',
    'incidentDate': '2026-09-10T10:00:00Z',
    'incidentLocation': 'Highway 1',
    'description': 'Minor bumper scratch',
    'claimedAmount': 1500.0,
    'status': 'Submitted',
    'createdAt': '2026-09-10T12:00:00Z',
    'updatedAt': '2026-09-10T12:30:00Z',
    'submittedAt': '2026-09-10T12:30:00Z',
    'documents': [],
  };

  final sampleClaim2 = {
    'id': 'claim-2',
    'policyId': 'policy-2',
    'policyNumber': 'POL-002',
    'policyHolderId': 'user-1',
    'claimNumber': 'CLM-002',
    'claimType': 'Home',
    'incidentDate': '2026-09-15T10:00:00Z',
    'incidentLocation': 'Oak Street',
    'description': 'Roof water damage',
    'claimedAmount': 5000.0,
    'status': 'Draft',
    'createdAt': '2026-09-15T12:00:00Z',
    'updatedAt': '2026-09-15T12:30:00Z',
    'submittedAt': null,
    'documents': [],
  };

  final samplePolicy1 = {
    'id': 'policy-1',
    'policyNumber': 'POL-AUTO-001',
    'policyHolderId': 'user-1',
    'policyTypeId': 'pt-1',
    'policyTypeName': 'Comprehensive Auto',
    'coverageLimit': 50000.0,
    'deductible': 1000.0,
    'premiumAmount': 1200.0,
    'status': 'Active',
    'startDate': '2026-01-01T00:00:00Z',
    'expiryDate': '2027-01-01T00:00:00Z',
    'createdAt': '2026-01-01T00:00:00Z',
  };

  final samplePolicy2 = {
    'id': 'policy-2',
    'policyNumber': 'POL-HOME-002',
    'policyHolderId': 'user-1',
    'policyTypeId': 'pt-2',
    'policyTypeName': 'Homeowners Protection',
    'coverageLimit': 250000.0,
    'deductible': 2500.0,
    'premiumAmount': 2400.0,
    'status': 'Expired',
    'startDate': '2025-01-01T00:00:00Z',
    'expiryDate': '2026-01-01T00:00:00Z',
    'createdAt': '2025-01-01T00:00:00Z',
  };

  final samplePayout1 = {
    'id': 'payout-1',
    'claimId': 'claim-1',
    'approvedClaimAmount': 1500.0,
    'coverageLimit': 50000.0,
    'deductible': 500.0,
    'proposedPayout': 1000.0,
    'finalPayout': 1000.0,
    'status': '4',
    'statusDisplay': 'Paid',
    'paymentReference': 'MOCK-PAY-999',
    'createdAt': '2026-09-12T10:00:00Z',
    'updatedAt': '2026-09-12T10:00:00Z',
  };

  final samplePayout2 = {
    'id': 'payout-2',
    'claimId': 'claim-2',
    'approvedClaimAmount': 5000.0,
    'coverageLimit': 250000.0,
    'deductible': 1000.0,
    'proposedPayout': 4000.0,
    'finalPayout': 4000.0,
    'status': '2',
    'statusDisplay': 'Approved',
    'paymentReference': null,
    'createdAt': '2026-09-18T10:00:00Z',
    'updatedAt': '2026-09-18T10:00:00Z',
  };

  group('Phase 5: Search and Filtering Tests', () {
    testWidgets('ClaimHistoryScreen filters by search text and status chip',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/api/claims/my-claims')) {
          return http.Response(
            jsonEncode([sampleClaim1, sampleClaim2]),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not Found', 404);
      });

      final apiService = ApiService(client: mockClient);
      final claimService = ClaimService(apiService: apiService);

      await tester.pumpWidget(MaterialApp(
        home: ClaimHistoryScreen(claimService: claimService),
      ));
      await tester.pumpAndSettle();

      // Both claims initially rendered
      expect(find.text('CLM-001'), findsOneWidget);
      expect(find.text('CLM-002'), findsOneWidget);

      // Search for CLM-002
      await tester.enterText(find.byType(TextField), 'Roof water');
      await tester.pumpAndSettle();

      expect(find.text('CLM-002'), findsOneWidget);
      expect(find.text('CLM-001'), findsNothing);

      // Clear search
      await tester.tap(find.byIcon(Icons.clear));
      await tester.pumpAndSettle();

      expect(find.text('CLM-001'), findsOneWidget);
      expect(find.text('CLM-002'), findsOneWidget);

      // Filter by Draft status chip
      await tester.tap(find.widgetWithText(ChoiceChip, 'Draft'));
      await tester.pumpAndSettle();

      expect(find.text('CLM-002'), findsOneWidget);
      expect(find.text('CLM-001'), findsNothing);

      // Search for non-existent text while in Draft
      await tester.enterText(find.byType(TextField), 'NONEXISTENT');
      await tester.pumpAndSettle();

      expect(find.text('No matching claims'), findsOneWidget);

      // Reset filters button restores all claims
      await tester.tap(find.widgetWithText(OutlinedButton, 'Reset Filters'));
      await tester.pumpAndSettle();

      expect(find.text('CLM-001'), findsOneWidget);
      expect(find.text('CLM-002'), findsOneWidget);
    });

    testWidgets('PoliciesScreen filters by search text and status chip',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/api/policies/my')) {
          return http.Response(
            jsonEncode([samplePolicy1, samplePolicy2]),
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

      // Both policies rendered
      expect(find.text('POL-AUTO-001'), findsOneWidget);
      expect(find.text('POL-HOME-002'), findsOneWidget);

      // Search for "Auto"
      await tester.enterText(find.byType(TextField), 'Auto');
      await tester.pumpAndSettle();

      expect(find.text('POL-AUTO-001'), findsOneWidget);
      expect(find.text('POL-HOME-002'), findsNothing);

      // Filter by Expired chip
      await tester.tap(find.byIcon(Icons.clear));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ChoiceChip, 'Expired'));
      await tester.pumpAndSettle();

      expect(find.text('POL-HOME-002'), findsOneWidget);
      expect(find.text('POL-AUTO-001'), findsNothing);

      // Non-existent search shows empty state
      await tester.enterText(find.byType(TextField), 'XYZ-NONEXISTENT');
      await tester.pumpAndSettle();

      expect(find.text('No matching policies'), findsOneWidget);

      // Reset filters restores list
      await tester.tap(find.widgetWithText(OutlinedButton, 'Reset Filters'));
      await tester.pumpAndSettle();

      expect(find.text('POL-AUTO-001'), findsOneWidget);
      expect(find.text('POL-HOME-002'), findsOneWidget);
    });

    testWidgets('PayoutHistoryScreen filters by search text and status chip',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/api/payouts/my')) {
          return http.Response(
            jsonEncode([samplePayout1, samplePayout2]),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not Found', 404);
      });

      final apiService = ApiService(client: mockClient);
      final payoutService = PayoutService(apiService: apiService);

      await tester.pumpWidget(MaterialApp(
        home: PayoutHistoryScreen(payoutService: payoutService),
      ));
      await tester.pumpAndSettle();

      // Both payouts rendered (LKR 1000.00 and LKR 4000.00)
      expect(find.text('LKR 1000.00'), findsOneWidget);
      expect(find.text('LKR 4000.00'), findsOneWidget);

      // Search by reference MOCK-PAY-999
      await tester.enterText(find.byType(TextField), 'MOCK-PAY-999');
      await tester.pumpAndSettle();

      expect(find.text('LKR 1000.00'), findsOneWidget);
      expect(find.text('LKR 4000.00'), findsNothing);

      // Filter by Approved chip
      await tester.tap(find.byIcon(Icons.clear));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ChoiceChip, 'Approved'));
      await tester.pumpAndSettle();

      expect(find.text('LKR 4000.00'), findsOneWidget);
      expect(find.text('LKR 1000.00'), findsNothing);

      // Search for unknown
      await tester.enterText(find.byType(TextField), 'UNKNOWN_PAY');
      await tester.pumpAndSettle();

      expect(find.text('No matching payouts'), findsOneWidget);

      // Reset
      await tester.tap(find.widgetWithText(OutlinedButton, 'Reset Filters'));
      await tester.pumpAndSettle();

      expect(find.text('LKR 1000.00'), findsOneWidget);
      expect(find.text('LKR 4000.00'), findsOneWidget);
    });

    testWidgets('PayoutDetailScreen displays breakdown, payment ref, and accurate status',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() => tester.view.resetPhysicalSize());

      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/api/payouts/payout-1')) {
          return http.Response(
            jsonEncode(samplePayout1),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not Found', 404);
      });

      final apiService = ApiService(client: mockClient);
      final payoutService = PayoutService(apiService: apiService);

      await tester.pumpWidget(MaterialApp(
        home: PayoutDetailScreen(
          payoutId: 'payout-1',
          payoutService: payoutService,
        ),
      ));
      await tester.pumpAndSettle();

      // Calculation Breakdown
      expect(find.text('Calculation Breakdown'), findsOneWidget);
      expect(find.text('LKR 1500.00'), findsOneWidget);
      expect(find.text('LKR 50000.00'), findsOneWidget);
      expect(find.text('- LKR 500.00'), findsOneWidget);
      expect(find.text('LKR 1000.00'), findsNWidgets(2)); // header and breakdown
      expect(find.text('Paid / Disbursed'), findsOneWidget);
      expect(find.text('MOCK-PAY-999'), findsOneWidget);
    });
  });
}
