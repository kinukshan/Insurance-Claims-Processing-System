import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:intl/intl.dart';
import 'package:insurance_claims_mobile/models/claim.dart';
import 'package:insurance_claims_mobile/models/policy.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/policy/create_policy_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';
import 'package:provider/provider.dart';

void main() {
  group('Flutter Policy Date Requirements (Tests 8–11)', () {
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
    ];

    Widget buildTestWidget({required PolicyService policyService}) {
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
            policyholderId: 'holder-123',
          ),
        ),
      );
    }

    testWidgets('8. Policy Start Date defaults to local today and Expiry to +1 year', (tester) async {
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

      final now = DateTime.now();
      final todayFormatted = DateFormat('yyyy-MM-dd').format(DateTime(now.year, now.month, now.day));
      final expiryFormatted = DateFormat('yyyy-MM-dd').format(DateTime(now.year + 1, now.month, now.day));

      expect(find.text(todayFormatted), findsOneWidget);
      expect(find.text(expiryFormatted), findsOneWidget);
    });

    testWidgets('9. Date picker for Start Date does not allow past dates (firstDate is today)', (tester) async {
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

      // Tap on Start Date picker
      await tester.tap(find.byKey(const Key('start_date_picker')));
      await tester.pumpAndSettle();

      // Find the date picker dialog
      expect(find.byType(DatePickerDialog), findsOneWidget);
      final datePickerDialog = tester.widget<DatePickerDialog>(find.byType(DatePickerDialog));

      final now = DateTime.now();
      final today = DateTime(now.year, now.month, now.day);

      // firstDate must not be in the past
      expect(datePickerDialog.firstDate.isBefore(today), isFalse);
      expect(datePickerDialog.firstDate.year, today.year);
      expect(datePickerDialog.firstDate.month, today.month);
      expect(datePickerDialog.firstDate.day, today.day);

      // Dismiss dialog
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
    });

    testWidgets('10. Expiry Date picker does not allow date <= Start Date', (tester) async {
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

      // Tap on Expiry Date picker
      await tester.tap(find.byKey(const Key('expiry_date_picker')));
      await tester.pumpAndSettle();

      expect(find.byType(DatePickerDialog), findsOneWidget);
      final datePickerDialog = tester.widget<DatePickerDialog>(find.byType(DatePickerDialog));

      final now = DateTime.now();
      final today = DateTime(now.year, now.month, now.day);
      final expectedMinExpiry = today.add(const Duration(days: 1));

      // firstDate of expiry picker must be strictly after start date (today + 1 day)
      expect(datePickerDialog.firstDate.isAfter(today), isTrue);
      expect(datePickerDialog.firstDate.year, expectedMinExpiry.year);
      expect(datePickerDialog.firstDate.month, expectedMinExpiry.month);
      expect(datePickerDialog.firstDate.day, expectedMinExpiry.day);

      // Dismiss dialog
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
    });

    test('11. Policy date serialization preserves YYYY-MM-DD exactly', () {
      final request = CreatePolicyRequest(
        policyholderId: 'holder-123',
        policyTypeId: 'type-456',
        coverageLimit: 500000.0,
        deductible: 10000.0,
        startDate: DateTime(2026, 10, 4),
        expiryDate: DateTime(2027, 10, 4),
      );

      final json = request.toJson();
      expect(json['startDate'], '2026-10-04');
      expect(json['expiryDate'], '2027-10-04');
    });
  });

  group('Flutter Claim Incident Date Requirements (Tests 12–15)', () {
    test('12. Selecting 2026-10-04 as Incident Date serializes to 2026-10-04 in ClaimService', () async {
      String? recordedBody;
      final mockClient = MockClient((request) async {
        if (request.method == 'POST' && request.url.path.contains('/claims')) {
          recordedBody = request.body;
          return http.Response(
            jsonEncode({
              'id': 'clm-1',
              'policyId': 'pol-1',
              'claimNumber': 'CLM-001',
              'claimType': 'Auto',
              'description': 'Test',
              'claimedAmount': 5000.0,
              'incidentDate': '2026-10-04',
              'incidentLocation': 'Colombo',
              'status': 'Draft',
              'createdAt': '2026-10-04T10:00:00Z',
            }),
            201,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not Found', 404);
      });

      final apiService = ApiService(client: mockClient);
      final claimService = ClaimService(apiService: apiService);

      final selectedIncidentDate = DateTime(2026, 10, 4);
      await claimService.createClaim(
        policyId: 'pol-1',
        claimType: 'Auto',
        incidentDate: selectedIncidentDate,
        incidentLocation: 'Colombo',
        description: 'Test accident',
        claimedAmount: 5000.0,
      );

      expect(recordedBody, isNotNull);
      final decoded = jsonDecode(recordedBody!) as Map<String, dynamic>;
      expect(decoded['incidentDate'], '2026-10-04');
    });

    test('13. API response containing 2026-10-04 displays 2026-10-04', () {
      final json = {
        'id': 'clm-1',
        'policyId': 'pol-1',
        'claimNumber': 'CLM-001',
        'claimType': 'Auto',
        'description': 'Test',
        'claimedAmount': 5000.0,
        'incidentDate': '2026-10-04',
        'incidentLocation': 'Colombo',
        'status': 'Draft',
        'createdAt': '2026-10-04T10:00:00Z',
      };

      final claim = Claim.fromJson(json);

      expect(claim.incidentDate.year, 2026);
      expect(claim.incidentDate.month, 10);
      expect(claim.incidentDate.day, 4);

      final formattedIso = DateFormat('yyyy-MM-dd').format(claim.incidentDate);
      final formattedDisplay = DateFormat('MMM dd, yyyy').format(claim.incidentDate);

      expect(formattedIso, '2026-10-04');
      expect(formattedDisplay, 'Oct 04, 2026');
    });

    test('14. No previous-day shift occurs when formatting or parsing ISO timestamp response', () {
      // Even if backend returns ISO timestamp 2026-10-04T00:00:00Z
      final json = {
        'id': 'clm-2',
        'policyId': 'pol-1',
        'claimNumber': 'CLM-002',
        'claimType': 'Auto',
        'description': 'Test',
        'claimedAmount': 5000.0,
        'incidentDate': '2026-10-04T00:00:00Z',
        'incidentLocation': 'Colombo',
        'status': 'Draft',
        'createdAt': '2026-10-04T10:00:00Z',
      };

      final claim = Claim.fromJson(json);

      // Must remain Oct 4, never shift to Oct 3
      expect(claim.incidentDate.year, 2026);
      expect(claim.incidentDate.month, 10);
      expect(claim.incidentDate.day, 4);

      final formatted = DateFormat.yMMMd().format(claim.incidentDate);
      expect(formatted, contains('Oct 4'));
    });

    test('15. Positive UTC offset (+05:30 Sri Lanka) preserves local calendar date', () {
      // Construct local DateTime representing midnight in Sri Lanka (+05:30)
      final localMidnight = DateTime(2026, 10, 4, 0, 0, 0);

      // Verify date-only string derived from local components remains '2026-10-04'
      final y = localMidnight.year.toString().padLeft(4, '0');
      final m = localMidnight.month.toString().padLeft(2, '0');
      final d = localMidnight.day.toString().padLeft(2, '0');
      final dateOnlyStr = '$y-$m-$d';
      expect(dateOnlyStr, '2026-10-04');

      // Contrast with flawed legacy .toUtc().toIso8601String() in UTC+05:30:
      // If simulated in UTC+5:30, subtracting 5:30 would produce 2026-10-03T18:30:00Z.
      // Date-only serialization completely avoids this issue.
      final claim = Claim.fromJson({
        'id': 'clm-3',
        'claimNumber': 'CLM-003',
        'claimType': 'Health',
        'description': 'Hospitalization',
        'claimedAmount': 15000.0,
        'incidentDate': dateOnlyStr,
        'incidentLocation': 'Kandy',
        'status': 'Submitted',
        'createdAt': '2026-10-04T00:00:00Z',
      });

      expect(claim.incidentDate.year, 2026);
      expect(claim.incidentDate.month, 10);
      expect(claim.incidentDate.day, 4);
      expect(claim.toJson()['incidentDate'], '2026-10-04');
    });
  });
}
