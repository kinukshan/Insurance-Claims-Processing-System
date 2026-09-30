import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/models/document_requirement.dart';
import 'package:insurance_claims_mobile/models/policy.dart';
import 'package:insurance_claims_mobile/screens/claims/submit_claim_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';

void main() {
  final samplePolicyJson = {
    'id': '11111111-1111-1111-1111-111111111111',
    'policyNumber': 'POL-MOTOR-001',
    'policyHolderId': '22222222-2222-2222-2222-222222222222',
    'policyTypeId': '33333333-3333-3333-3333-333333333333',
    'policyTypeName': 'Motor Insurance',
    'insuranceClass': 'NonLife',
    'startDate': '2026-01-01T00:00:00Z',
    'expiryDate': '2027-01-01T00:00:00Z',
    'coverageLimit': 50000.0,
    'premium': 1200.0,
    'deductible': 500.0,
    'status': 'Active',
    'renewalStatus': 'NotRenewed',
    'isExpired': false,
    'exclusions': null,
    'coverages': [],
  };

  final sampleHealthPolicyJson = {
    'id': '44444444-4444-4444-4444-444444444444',
    'policyNumber': 'POL-HLTH-002',
    'policyHolderId': '22222222-2222-2222-2222-222222222222',
    'policyTypeId': '55555555-5555-5555-5555-555555555555',
    'policyTypeName': 'Health Insurance',
    'insuranceClass': 'Life',
    'startDate': '2026-01-01T00:00:00Z',
    'expiryDate': '2027-01-01T00:00:00Z',
    'coverageLimit': 25000.0,
    'premium': 800.0,
    'deductible': 250.0,
    'status': 'Active',
    'renewalStatus': 'NotRenewed',
    'isExpired': false,
    'exclusions': null,
    'coverages': [],
  };

  final sampleDraftClaimJson = {
    'id': '66666666-6666-6666-6666-666666666666',
    'claimNumber': 'CLM-2026-0099',
    'policyId': '11111111-1111-1111-1111-111111111111',
    'policyHolderId': '22222222-2222-2222-2222-222222222222',
    'claimType': 'Auto',
    'status': 'Draft',
    'incidentDate': '2026-09-15T12:00:00Z',
    'incidentLocation': 'Colombo Expressway',
    'description': 'Vehicle fender damaged in rear collision',
    'claimedAmount': 4500.0,
    'createdAt': '2026-09-15T13:00:00Z',
    'updatedAt': null,
    'documents': [],
  };

  final sampleSubmittedClaimJson = {
    ...sampleDraftClaimJson,
    'status': 'Submitted',
    'submittedAt': '2026-09-15T14:00:00Z',
  };

  // ─────────────────────────────────────────────────────────────
  // 1. Policy & Claim Type Compatibility Tests
  // ─────────────────────────────────────────────────────────────
  group('Phase 3: Policy & Claim Type Compatibility', () {
    test('Motor / Auto policy compatibility includes Motor and Auto', () {
      final types = ClaimService.getCompatibleClaimTypes('Motor Insurance');
      expect(types, contains('Motor'));
      expect(types, contains('Auto'));
      expect(types, isNot(contains('Health')));
      expect(types, isNot(contains('Life')));

      // Test aliases
      expect(ClaimService.getCompatibleClaimTypes('Comprehensive Auto'),
          contains('Auto'));
      expect(ClaimService.getCompatibleClaimTypes('auto'), contains('Motor'));
    });

    test('Health Insurance policy compatibility includes Health only', () {
      final types = ClaimService.getCompatibleClaimTypes('Health Insurance');
      expect(types, equals(['Health']));
    });

    test('Home Insurance policy compatibility includes Property and Home', () {
      final types = ClaimService.getCompatibleClaimTypes('Home Insurance');
      expect(types, contains('Property'));
      expect(types, contains('Home'));
      expect(types, isNot(contains('Auto')));
    });

    test('Life Insurance policy compatibility includes Life only', () {
      final types = ClaimService.getCompatibleClaimTypes('Life Insurance');
      expect(types, equals(['Life']));
    });

    test('Unknown or empty policy type returns empty list', () {
      expect(ClaimService.getCompatibleClaimTypes(''), isEmpty);
      expect(ClaimService.getCompatibleClaimTypes(null), isEmpty);
      expect(ClaimService.getCompatibleClaimTypes('Cyber Security'), isEmpty);
    });

    test('ClaimType enum mapping includes Motor with index 8', () {
      expect(ClaimService.claimTypeIndices['Auto'], equals(0));
      expect(ClaimService.claimTypeIndices['Home'], equals(1));
      expect(ClaimService.claimTypeIndices['Health'], equals(2));
      expect(ClaimService.claimTypeIndices['Life'], equals(3));
      expect(ClaimService.claimTypeIndices['Travel'], equals(4));
      expect(ClaimService.claimTypeIndices['Property'], equals(5));
      expect(ClaimService.claimTypeIndices['Liability'], equals(6));
      expect(ClaimService.claimTypeIndices['Other'], equals(7));
      expect(ClaimService.claimTypeIndices['Motor'], equals(8));
    });
  });

  // ─────────────────────────────────────────────────────────────
  // 2. Document Checklist Tests
  // ─────────────────────────────────────────────────────────────
  group('Phase 3: Document Checklist & Requirements Model', () {
    test('Auto claim requires 4 specific documents', () {
      final docs = ClaimService.getRequiredDocuments('Auto');
      expect(docs, contains('Police Report'));
      expect(docs, contains('Photos of Damage'));
      expect(docs, contains('Repair Estimate'));
      expect(docs, contains('Driver License'));
      expect(docs.length, equals(4));
    });

    test('Health claim requires 4 medical documents', () {
      final docs = ClaimService.getRequiredDocuments('Health');
      expect(docs, contains('Medical Report'));
      expect(docs, contains('Hospital Bills'));
      expect(docs, contains('Prescription'));
      expect(docs, contains('Doctor Referral'));
    });

    test('Life claim requires death certificate and beneficiary ID', () {
      final docs = ClaimService.getRequiredDocuments('Life');
      expect(docs, contains('Death Certificate'));
      expect(docs, contains('Beneficiary / Nominee Identification'));
      expect(docs, contains('Policy Document'));
      expect(docs, contains('Claim Form'));
    });

    test('DocumentRequirements model parses checklist and identifies missing items', () {
      final json = {
        'claimId': '66666666-6666-6666-6666-666666666666',
        'claimType': 'Auto',
        'requiredDocuments': [
          {'type': 'Police Report', 'required': true, 'uploaded': true},
          {'type': 'Photos of Damage', 'required': true, 'uploaded': false},
          {'type': 'Repair Estimate', 'required': true, 'uploaded': false},
          {'type': 'Driver License', 'required': true, 'uploaded': true},
        ],
        'requiredCount': 4,
        'uploadedRequiredCount': 2,
        'missingCount': 2,
        'complete': false,
      };

      final reqs = DocumentRequirements.fromJson(json);
      expect(reqs.claimType, equals('Auto'));
      expect(reqs.requiredCount, equals(4));
      expect(reqs.uploadedRequiredCount, equals(2));
      expect(reqs.complete, isFalse);
      expect(reqs.missingDocumentTypes, contains('Photos of Damage'));
      expect(reqs.missingDocumentTypes, contains('Repair Estimate'));
      expect(reqs.missingDocumentTypes, isNot(contains('Police Report')));
    });
  });

  // ─────────────────────────────────────────────────────────────
  // 3. ClaimService Mocked API Operations
  // ─────────────────────────────────────────────────────────────
  group('Phase 3: ClaimService API Operations', () {
    test('createClaim sends POST to /claims with integer claimType', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims'));
        expect(request.method, equals('POST'));
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['policyId'], equals(samplePolicyJson['id']));
        expect(body['claimType'], equals(8)); // Motor maps to 8
        expect(body['claimedAmount'], equals(3500.0));
        return http.Response(jsonEncode(sampleDraftClaimJson), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      final claim = await service.createClaim(
        policyId: samplePolicyJson['id'] as String,
        claimType: 'Motor',
        incidentDate: DateTime.parse('2026-09-15T12:00:00Z'),
        incidentLocation: 'Colombo Expressway',
        description: 'Rear bumper scratch',
        claimedAmount: 3500.0,
      );

      expect(claim.id, equals(sampleDraftClaimJson['id']));
      expect(claim.status, equals('Draft'));
    });

    test('updateClaim sends PUT to /claims/{id} with modified fields', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims/66666666-6666-6666-6666-666666666666'));
        expect(request.method, equals('PUT'));
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['claimedAmount'], equals(5000.0));
        expect(body['description'], equals('Updated description'));
        return http.Response(jsonEncode({
          ...sampleDraftClaimJson,
          'claimedAmount': 5000.0,
          'description': 'Updated description',
        }), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      final updated = await service.updateClaim(
        '66666666-6666-6666-6666-666666666666',
        description: 'Updated description',
        claimedAmount: 5000.0,
      );

      expect(updated.claimedAmount, equals(5000.0));
      expect(updated.description, equals('Updated description'));
    });

    test('submitClaim sends POST to /claims/{id}/submit', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims/66666666-6666-6666-6666-666666666666/submit'));
        expect(request.method, equals('POST'));
        return http.Response(jsonEncode(sampleSubmittedClaimJson), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      final submitted = await service.submitClaim('66666666-6666-6666-6666-666666666666');
      expect(submitted.status, equals('Submitted'));
    });

    test('withdrawClaim sends POST to /claims/{id}/withdraw', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims/66666666-6666-6666-6666-666666666666/withdraw'));
        expect(request.method, equals('POST'));
        return http.Response(jsonEncode({
          ...sampleDraftClaimJson,
          'status': 'Withdrawn',
        }), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      final withdrawn = await service.withdrawClaim('66666666-6666-6666-6666-666666666666');
      expect(withdrawn.status, equals('Withdrawn'));
    });

    test('deleteClaim sends DELETE to /claims/{id}', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims/66666666-6666-6666-6666-666666666666'));
        expect(request.method, equals('DELETE'));
        return http.Response('', 204);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      await expectLater(
        service.deleteClaim('66666666-6666-6666-6666-666666666666'),
        completes,
      );
    });

    test('getDocumentRequirements fetches checklist status', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/claims/66666666-6666-6666-6666-666666666666/document-requirements'));
        return http.Response(jsonEncode({
          'claimId': '66666666-6666-6666-6666-666666666666',
          'claimType': 'Auto',
          'requiredDocuments': [
            {'type': 'Police Report', 'required': true, 'uploaded': true},
          ],
          'requiredCount': 1,
          'uploadedRequiredCount': 1,
          'missingCount': 0,
          'complete': true,
        }), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      final reqs = await service.getDocumentRequirements('66666666-6666-6666-6666-666666666666');
      expect(reqs.complete, isTrue);
      expect(reqs.missingCount, equals(0));
    });
  });

  // ─────────────────────────────────────────────────────────────
  // 4. Session Expiry & Authorization Error Tests
  // ─────────────────────────────────────────────────────────────
  group('Phase 3: Session Expiry & Authorization Handling', () {
    test('401 Unauthorized throws ApiException with code 401', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'error': 'Token has expired'}), 401);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      expect(
        () => service.getClaim('66666666-6666-6666-6666-666666666666'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 401)),
      );
    });

    test('403 Forbidden throws ApiException with code 403', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'error': 'Forbidden'}), 403);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = ClaimService(apiService: api);

      expect(
        () => service.getClaim('66666666-6666-6666-6666-666666666666'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 403)),
      );
    });
  });

  // ─────────────────────────────────────────────────────────────
  // 5. SubmitClaimScreen Widget Tests (Phase 3 Features)
  // ─────────────────────────────────────────────────────────────
  group('Phase 3: SubmitClaimScreen Widget Tests', () {
    testWidgets('populates policy selector when policies are loaded', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicyJson, sampleHealthPolicyJson]), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(MaterialApp(
        home: SubmitClaimScreen(
          policyService: policyService,
          claimService: claimService,
        ),
      ));
      await tester.pumpAndSettle();

      // Policy selector dropdown exists
      expect(find.text('Select Policy'), findsOneWidget);

      // Open policy selector
      await tester.tap(find.widgetWithText(DropdownButtonFormField<Policy>, 'Select Policy'));
      await tester.pumpAndSettle();

      expect(find.textContaining('POL-MOTOR-001'), findsWidgets);
      expect(find.textContaining('POL-HLTH-002'), findsWidgets);
    });

    testWidgets('selecting a policy updates Policy ID and displays policy card', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicyJson]), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(MaterialApp(
        home: SubmitClaimScreen(
          policyService: policyService,
          claimService: claimService,
        ),
      ));
      await tester.pumpAndSettle();

      // Select policy
      await tester.tap(find.widgetWithText(DropdownButtonFormField<Policy>, 'Select Policy'));
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining('POL-MOTOR-001').last);
      await tester.pumpAndSettle();

      // Policy card displays coverage limit and status
      expect(find.text('POL-MOTOR-001'), findsWidgets);
      expect(find.textContaining('LKR 50000.00'), findsWidgets);

      // Policy ID field is populated
      final policyIdField = tester.widget<TextFormField>(
        find.widgetWithText(TextFormField, 'Policy ID'),
      );
      expect(policyIdField.controller?.text, equals(samplePolicyJson['id']));
    });

    testWidgets('exceeding policy coverage limit displays validation error', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicyJson]), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(MaterialApp(
        home: SubmitClaimScreen(
          policyService: policyService,
          claimService: claimService,
        ),
      ));
      await tester.pumpAndSettle();

      // Select Motor policy (Coverage limit = 50,000)
      await tester.tap(find.widgetWithText(DropdownButtonFormField<Policy>, 'Select Policy'));
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining('POL-MOTOR-001').last);
      await tester.pumpAndSettle();

      // Enter amount exceeding coverage limit
      final amountField = find.widgetWithText(TextFormField, 'Claimed Amount (LKR)');
      await tester.enterText(amountField, '999999');

      await tester.enterText(find.widgetWithText(TextFormField, 'Incident Location'), 'Colombo');
      await tester.enterText(find.widgetWithText(TextFormField, 'Description'), 'Accident');

      final submitBtn = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.textContaining('Exceeds policy coverage limit'), findsOneWidget);
    });

    testWidgets('loads existing draft claim and populates fields', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicyJson]), 200);
        }
        if (request.url.path.endsWith('/claims/66666666-6666-6666-6666-666666666666')) {
          return http.Response(jsonEncode(sampleDraftClaimJson), 200);
        }
        if (request.url.path.endsWith('/documents')) {
          return http.Response(jsonEncode([]), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(MaterialApp(
        home: SubmitClaimScreen(
          draftClaimId: '66666666-6666-6666-6666-666666666666',
          policyService: policyService,
          claimService: claimService,
        ),
      ));
      await tester.pumpAndSettle();

      // Should show draft banner
      expect(find.textContaining('CLM-2026-0099'), findsWidgets);

      // Fields should be populated
      expect(find.text('Colombo Expressway'), findsOneWidget);
      expect(find.text('Vehicle fender damaged in rear collision'), findsOneWidget);
      expect(find.text('4500.00'), findsOneWidget);
    });

    testWidgets('Save Draft button triggers draft creation', (tester) async {
      bool draftCreated = false;
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/policies/my')) {
          return http.Response(jsonEncode([samplePolicyJson]), 200);
        }
        if (request.url.path.endsWith('/claims') && request.method == 'POST') {
          draftCreated = true;
          return http.Response(jsonEncode(sampleDraftClaimJson), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final policyService = PolicyService(apiService: api);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(MaterialApp(
        home: SubmitClaimScreen(
          policyService: policyService,
          claimService: claimService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'),
        '11111111-1111-1111-1111-111111111111',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'),
        'Kandy Road',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'),
        'Engine overheating incident',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Claimed Amount (LKR)'),
        '1500',
      );

      final saveDraftBtn = find.widgetWithText(OutlinedButton, 'Save Draft');
      await tester.ensureVisible(saveDraftBtn);
      await tester.tap(saveDraftBtn);
      await tester.pumpAndSettle();

      expect(draftCreated, isTrue);
      expect(find.textContaining('Draft saved'), findsOneWidget);
    });
  });
}
