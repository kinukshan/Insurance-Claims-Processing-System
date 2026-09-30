import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/models/notification_item.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';
import 'package:insurance_claims_mobile/screens/claims/claim_details_screen.dart';
import 'package:insurance_claims_mobile/screens/claims/claim_status_screen.dart';
import 'package:insurance_claims_mobile/screens/payout/payout_status_screen.dart';

void main() {
  final sampleClaimJson = {
    'id': 'claim-1111',
    'policyId': 'policy-aaaa-bbbb-cccc-dddddddddddd',
    'policyNumber': 'POL-TEST-001',
    'policyHolderId': 'user-1111-2222-3333-444444444444',
    'claimNumber': 'CLM-20260926-0001',
    'claimType': 'Motor',
    'incidentDate': '2026-09-20T10:00:00Z',
    'incidentLocation': 'Main St & 5th Ave',
    'description': 'Front collision with barrier',
    'claimedAmount': 35000.0,
    'status': 'Submitted',
    'createdAt': '2026-09-20T12:00:00Z',
    'updatedAt': '2026-09-20T12:30:00Z',
    'submittedAt': '2026-09-20T12:30:00Z',
    'documents': [
      {
        'id': 'doc-1',
        'claimId': 'claim-1111-2222-3333-444444444444',
        'fileName': 'police_report.pdf',
        'fileUrl': '/uploads/police_report.pdf',
        'documentType': 'Police Report',
        'fileSize': 716,
        'uploadedAt': '2026-09-20T12:10:00Z',
        'verificationStatus': 'Verified',
      },
      {
        'id': 'doc-2',
        'claimId': 'claim-1111-2222-3333-444444444444',
        'fileName': 'damage_photo.jpg',
        'fileUrl': '/uploads/damage_photo.jpg',
        'documentType': 'Photos of Damage',
        'fileSize': 149,
        'uploadedAt': '2026-09-20T12:15:00Z',
        'verificationStatus': 'Verified',
      }
    ],
  };

  final sampleDraftClaimJson = {
    ...sampleClaimJson,
    'id': 'draft-claim-1111',
    'claimNumber': 'CLM-DRAFT-001',
    'status': 'Draft',
    'submittedAt': null,
  };

  final samplePayoutPendingApproval = {
    'id': 'payout-1111',
    'claimId': 'claim-1111',
    'approvedClaimAmount': 35000.0,
    'coverageLimit': 50000.0,
    'deductible': 10000.0,
    'proposedPayout': 25000.0,
    'finalPayout': 25000.0,
    'status': '1',
    'statusDisplay': 'PendingApproval',
    'approvedBy': null,
    'approvalTimestamp': null,
    'paymentReference': null,
    'createdAt': '2026-09-21T10:00:00Z',
    'updatedAt': '2026-09-21T10:00:00Z',
  };

  final samplePayoutApproved = {
    'id': 'payout-2222',
    'claimId': 'claim-1111',
    'approvedClaimAmount': 35000.0,
    'coverageLimit': 50000.0,
    'deductible': 10000.0,
    'proposedPayout': 25000.0,
    'finalPayout': 25000.0,
    'status': '2',
    'statusDisplay': 'Approved',
    'approvedBy': 'Michael Chen',
    'approvalTimestamp': '2026-09-22T14:00:00Z',
    'paymentReference': null,
    'createdAt': '2026-09-21T10:00:00Z',
    'updatedAt': '2026-09-22T14:00:00Z',
  };

  final samplePayoutPaid = {
    'id': 'payout-3333',
    'claimId': 'claim-1111',
    'approvedClaimAmount': 35000.0,
    'coverageLimit': 50000.0,
    'deductible': 10000.0,
    'proposedPayout': 25000.0,
    'finalPayout': 25000.0,
    'status': '6',
    'statusDisplay': 'Paid',
    'approvedBy': 'Michael Chen',
    'approvalTimestamp': '2026-09-22T14:00:00Z',
    'paymentReference': 'MOCK-PAY-20260926',
    'createdAt': '2026-09-21T10:00:00Z',
    'updatedAt': '2026-09-22T16:00:00Z',
  };

  group('Phase 4: Claim Details and Payout Navigation Tests', () {
    testWidgets('ClaimDetailsScreen renders Payout Card when payout exists and navigates to Payout Details', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/claims/claim-1111')) {
          return http.Response(jsonEncode(sampleClaimJson), 200);
        }
        if (request.url.path.contains('/payouts/claim/claim-1111')) {
          return http.Response(jsonEncode(samplePayoutApproved), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);
      final payoutService = PayoutService(apiService: api);

      String? pushedRoute;
      dynamic pushedArguments;

      await tester.pumpWidget(
        MaterialApp(
          onGenerateRoute: (settings) {
            if (settings.name == '/payout/status') {
              pushedRoute = settings.name;
              pushedArguments = settings.arguments;
              return MaterialPageRoute(builder: (_) => const Scaffold(body: Text('Payout Status Target')));
            }
            return MaterialPageRoute(
              builder: (_) => ClaimDetailsScreen(
                claimId: 'claim-1111',
                claimService: claimService,
                payoutService: payoutService,
              ),
            );
          },
        ),
      );

      await tester.pumpAndSettle();

      // Verify claim details rendered
      expect(find.text('CLM-20260926-0001'), findsOneWidget);
      expect(find.text('Claim Payout'), findsOneWidget);
      expect(find.text('LKR 25000.00'), findsOneWidget);
      // Status badge for Approved shows Approved (Pending Disbursement)
      expect(find.text('Approved (Pending Disbursement)'), findsOneWidget);

      // Tap View Payout Details
      final viewPayoutBtn = find.text('View Payout Details');
      expect(viewPayoutBtn, findsOneWidget);
      await tester.tap(viewPayoutBtn);
      await tester.pumpAndSettle();

      expect(pushedRoute, '/payout/status');
      expect(pushedArguments, isA<Map>());
      expect((pushedArguments as Map)['claimId'], 'claim-1111');
      expect(find.text('Payout Status Target'), findsOneWidget);
    });

    testWidgets('ClaimDetailsScreen handles empty payout gracefully without crashing', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/claims/claim-1111')) {
          return http.Response(jsonEncode(sampleClaimJson), 200);
        }
        if (request.url.path.contains('/payouts/claim/claim-1111')) {
          return http.Response('Not Found', 404);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);
      final payoutService = PayoutService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimDetailsScreen(
            claimId: 'claim-1111',
            claimService: claimService,
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('CLM-20260926-0001'), findsOneWidget);
      // Claim Payout card should not be present
      expect(find.text('Claim Payout'), findsNothing);
    });

    testWidgets('ClaimDetailsScreen supports Claim Withdrawal with confirmation dialog', (tester) async {
      bool withdrawCalled = false;
      final mockClient = MockClient((request) async {
        if (request.url.path == '/api/claims/claim-1111') {
          return http.Response(jsonEncode(sampleClaimJson), 200);
        }
        if (request.url.path == '/api/claims/claim-1111/withdraw') {
          withdrawCalled = true;
          final withdrawn = Map<String, dynamic>.from(sampleClaimJson);
          withdrawn['status'] = 'Withdrawn';
          return http.Response(jsonEncode(withdrawn), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimDetailsScreen(
            claimId: 'claim-1111',
            claimService: claimService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Find Withdraw Claim button
      final withdrawBtn = find.text('Withdraw Claim');
      expect(withdrawBtn, findsOneWidget);

      // Tap Withdraw Claim
      await tester.tap(withdrawBtn);
      await tester.pumpAndSettle();

      // Confirmation dialog should be displayed
      expect(find.text('Withdraw Claim?'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);

      // Tap Confirm in Dialog
      final confirmBtn = find.widgetWithText(FilledButton, 'Withdraw');
      expect(confirmBtn, findsOneWidget);
      await tester.tap(confirmBtn);
      await tester.pumpAndSettle();

      expect(withdrawCalled, isTrue);
      expect(find.text('Claim withdrawn successfully'), findsOneWidget);
    });

    testWidgets('ClaimDetailsScreen supports Draft Deletion on draft claims', (tester) async {
      bool deleteCalled = false;
      final mockClient = MockClient((request) async {
        if (request.url.path == '/api/claims/draft-claim-1111') {
          if (request.method == 'GET') {
            return http.Response(jsonEncode(sampleDraftClaimJson), 200);
          }
          if (request.method == 'DELETE') {
            deleteCalled = true;
            return http.Response('', 204);
          }
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimDetailsScreen(
            claimId: 'draft-claim-1111',
            claimService: claimService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Should show Delete Draft button
      final deleteDraftBtn = find.text('Delete Draft');
      expect(deleteDraftBtn, findsOneWidget);

      await tester.tap(deleteDraftBtn);
      await tester.pumpAndSettle();

      // Dialog confirmation
      expect(find.text('Delete Draft Claim?'), findsOneWidget);
      final confirmDelete = find.widgetWithText(FilledButton, 'Delete');
      await tester.tap(confirmDelete);
      await tester.pumpAndSettle();

      expect(deleteCalled, isTrue);
    });

    testWidgets('ClaimDetailsScreen supports Document Deletion with confirmation', (tester) async {
      bool docDeleteCalled = false;
      final mockClient = MockClient((request) async {
        if (request.url.path == '/api/claims/claim-1111') {
          return http.Response(jsonEncode(sampleClaimJson), 200);
        }
        if (request.url.path == '/api/claims/claim-1111/documents/doc-1') {
          docDeleteCalled = true;
          return http.Response('', 204);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);

      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimDetailsScreen(
            claimId: 'claim-1111',
            claimService: claimService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Find delete icon for document
      final deleteDocBtns = find.byIcon(Icons.delete_outline);
      expect(deleteDocBtns, findsNWidgets(2));

      await tester.tap(deleteDocBtns.first);
      await tester.pumpAndSettle();

      expect(find.text('Delete Document?'), findsOneWidget);
      final confirmBtn = find.widgetWithText(FilledButton, 'Delete');
      await tester.tap(confirmBtn);
      await tester.pumpAndSettle();

      expect(docDeleteCalled, isTrue);
    });
  });

  group('Phase 4: PayoutStatusScreen Status Differentiation Tests', () {
    testWidgets('never presents Approved payout as already paid', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(samplePayoutApproved), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final payoutService = PayoutService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: PayoutStatusScreen(
            payoutId: 'payout-2222',
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Confirms Approved is explicitly distinct from Paid
      expect(find.text('Approved (Pending Disbursement)'), findsOneWidget);
      expect(find.textContaining('Funds have NOT been transferred yet'), findsOneWidget);
      expect(find.text('Disbursed / Paid'), findsNothing);
    });

    testWidgets('accurately presents Paid payout with Payment Reference', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(samplePayoutPaid), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final payoutService = PayoutService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: PayoutStatusScreen(
            payoutId: 'payout-3333',
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('Disbursed / Paid'), findsOneWidget);
      expect(find.text('MOCK-PAY-20260926'), findsOneWidget);
      expect(find.text('Payment Reference Number'), findsOneWidget);
    });

    testWidgets('renders PendingApproval status accurately', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(samplePayoutPendingApproval), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final payoutService = PayoutService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: PayoutStatusScreen(
            payoutId: 'payout-1111',
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('Pending Approval'), findsOneWidget);
      expect(find.textContaining('Awaiting underwriter review'), findsOneWidget);
    });
  });

  group('Phase 4: ClaimStatusScreen Authoritative Timeline Tests', () {
    testWidgets('displays confirmed milestones and timestamps without hardcoded progress', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.contains('/claims/claim-1111')) {
          return http.Response(jsonEncode(sampleClaimJson), 200);
        }
        if (request.url.path.contains('/payouts/claim/claim-1111')) {
          return http.Response(jsonEncode(samplePayoutApproved), 200);
        }
        return http.Response('Not Found', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);
      final payoutService = PayoutService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimStatusScreen(
            claimId: 'claim-1111',
            claimService: claimService,
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Confirms title shows claim number
      expect(find.text('CLM-20260926-0001 Status'), findsOneWidget);

      // Confirmed milestones
      expect(find.text('Claim Created'), findsOneWidget);
      expect(find.text('Claim Submitted'), findsOneWidget);
      expect(find.text('Document Verification'), findsOneWidget);
      expect(find.text('Payout Proposal Prepared'), findsOneWidget);
      expect(find.text('Underwriter Approval'), findsOneWidget);
      expect(find.text('Payment Disbursed'), findsOneWidget);

      // Payment Disbursed should NOT be confirmed since payout is only Approved
      expect(find.text('Approved — pending electronic disbursement'), findsOneWidget);
    });

    testWidgets('renders terminal state correctly for Withdrawn claim', (tester) async {
      final withdrawnClaim = {
        ...sampleClaimJson,
        'status': 'Withdrawn',
      };

      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(withdrawnClaim), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final claimService = ClaimService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimStatusScreen(
            claimId: 'claim-1111',
            claimService: claimService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('Claim Withdrawn'), findsOneWidget);
      expect(find.text('This claim was withdrawn by the policyholder.'), findsOneWidget);
    });
  });

  group('Phase 4: Notification Lifecycle and Semantics Tests', () {
    test('NotificationItem semanticDescription distinguishes Resend Accepted, Mock Sent, and Failed', () {
      final resendItem = NotificationItem.fromJson({
        'id': '1',
        'userId': 'u1',
        'notificationType': 'ClaimSubmitted',
        'channel': 'Email',
        'recipient': 'user@example.com',
        'subject': 'Submitted',
        'success': true,
        'provider': 'Resend',
        'status': 'Accepted',
        'createdAt': '2026-09-26T12:00:00Z',
      });

      final mockItem = NotificationItem.fromJson({
        'id': '2',
        'userId': 'u1',
        'notificationType': 'PayoutApproved',
        'channel': 'Email',
        'recipient': 'user@example.com',
        'subject': 'Approved',
        'success': true,
        'provider': 'Mock',
        'status': 'Sent',
        'createdAt': '2026-09-26T12:00:00Z',
      });

      final failedItem = NotificationItem.fromJson({
        'id': '3',
        'userId': 'u1',
        'notificationType': 'DocumentVerified',
        'channel': 'Email',
        'recipient': 'user@example.com',
        'subject': 'Verified',
        'success': false,
        'provider': 'Resend',
        'status': 'Failed',
        'errorMessage': 'API Key invalid',
        'createdAt': '2026-09-26T12:00:00Z',
      });

      expect(resendItem.semanticDescription, contains('Resend Accepted: Provider accepted'));
      expect(mockItem.semanticDescription, contains('Mock Sent: Successful mock processing'));
      expect(failedItem.semanticDescription, contains('notification attempt failed'));
    });
  });
}
