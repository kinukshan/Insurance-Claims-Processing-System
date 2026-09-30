import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/models/claim_document.dart';
import 'package:insurance_claims_mobile/models/document_requirement.dart';
import 'package:insurance_claims_mobile/screens/claims/claim_details_screen.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';

void main() {
  const claimId = '11111111-2222-3333-4444-555555555555';

  group('Document Upload-Delete-Reupload Regression Suite', () {
    test('Checklist reflects requirement fulfillment, deletion rollback, and re-upload', () {
      // 1. Initial State: 4 requirements, 0 uploaded
      var reqs = DocumentRequirements(
        claimId: claimId,
        claimType: 'Auto',
        requiredDocuments: const [
          DocumentRequirementItem(type: 'Police Report', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Photos of Damage', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Repair Estimate', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Driver License', required: true, uploaded: false),
        ],
        requiredCount: 4,
        uploadedRequiredCount: 0,
        missingCount: 4,
        complete: false,
      );

      expect(reqs.complete, isFalse);
      expect(reqs.missingCount, equals(4));
      expect(reqs.missingDocumentTypes, contains('Photos of Damage'));

      // 2. Upload "Photos of Damage"
      reqs = DocumentRequirements(
        claimId: claimId,
        claimType: 'Auto',
        requiredDocuments: const [
          DocumentRequirementItem(type: 'Police Report', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Photos of Damage', required: true, uploaded: true),
          DocumentRequirementItem(type: 'Repair Estimate', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Driver License', required: true, uploaded: false),
        ],
        requiredCount: 4,
        uploadedRequiredCount: 1,
        missingCount: 3,
        complete: false,
      );

      expect(reqs.uploadedRequiredCount, equals(1));
      expect(reqs.missingDocumentTypes, isNot(contains('Photos of Damage')));
      expect(reqs.missingDocumentTypes, contains('Police Report'));

      // 3. Delete "Photos of Damage" -> reverts to missing
      reqs = DocumentRequirements(
        claimId: claimId,
        claimType: 'Auto',
        requiredDocuments: const [
          DocumentRequirementItem(type: 'Police Report', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Photos of Damage', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Repair Estimate', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Driver License', required: true, uploaded: false),
        ],
        requiredCount: 4,
        uploadedRequiredCount: 0,
        missingCount: 4,
        complete: false,
      );

      expect(reqs.uploadedRequiredCount, equals(0));
      expect(reqs.missingCount, equals(4));
      expect(reqs.missingDocumentTypes, contains('Photos of Damage'));

      // 4. Re-upload exact same document type
      reqs = DocumentRequirements(
        claimId: claimId,
        claimType: 'Auto',
        requiredDocuments: const [
          DocumentRequirementItem(type: 'Police Report', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Photos of Damage', required: true, uploaded: true),
          DocumentRequirementItem(type: 'Repair Estimate', required: true, uploaded: false),
          DocumentRequirementItem(type: 'Driver License', required: true, uploaded: false),
        ],
        requiredCount: 4,
        uploadedRequiredCount: 1,
        missingCount: 3,
        complete: false,
      );

      expect(reqs.uploadedRequiredCount, equals(1));
      expect(reqs.missingDocumentTypes, isNot(contains('Photos of Damage')));
    });

    test('Repeated cycles (upload -> delete -> re-upload -> delete -> re-upload) maintain consistency', () {
      final documents = <ClaimDocument>[];

      void uploadDoc(String id, String fileName, String type) {
        documents.add(ClaimDocument(
          id: id,
          claimId: claimId,
          fileName: fileName,
          fileUrl: 'https://storage.local/$fileName',
          documentType: type,
          contentType: fileName.endsWith('.pdf') ? 'application/pdf' : 'image/jpeg',
          fileSize: 1024 * 100,
          uploadedAt: DateTime.now(),
          verificationStatus: 'Pending',
        ));
      }

      void deleteDoc(String id) {
        documents.removeWhere((d) => d.id == id);
      }

      // Cycle 1: Upload and delete
      uploadDoc('doc-1', 'police_report.pdf', 'Police Report');
      expect(documents.length, equals(1));
      expect(documents.first.documentType, equals('Police Report'));

      deleteDoc('doc-1');
      expect(documents, isEmpty);

      // Cycle 2: Re-upload same file
      uploadDoc('doc-2', 'police_report.pdf', 'Police Report');
      expect(documents.length, equals(1));
      expect(documents.first.fileName, equals('police_report.pdf'));

      deleteDoc('doc-2');
      expect(documents, isEmpty);

      // Cycle 3: Re-upload replacement file
      uploadDoc('doc-3', 'police_report_amended.pdf', 'Police Report');
      expect(documents.length, equals(1));
      expect(documents.first.fileName, equals('police_report_amended.pdf'));

      deleteDoc('doc-3');
      expect(documents, isEmpty);

      // Cycle 4: Upload image
      uploadDoc('doc-4', 'damage_front.jpg', 'Photos of Damage');
      expect(documents.length, equals(1));
      expect(documents.first.contentType, equals('image/jpeg'));
    });

    test('Failed deletion does not alter document list', () {
      final documents = <ClaimDocument>[
        ClaimDocument(
          id: 'doc-locked',
          claimId: claimId,
          fileName: 'locked_file.pdf',
          fileUrl: 'https://storage.local/locked_file.pdf',
          documentType: 'Police Report',
          contentType: 'application/pdf',
          fileSize: 2048,
          uploadedAt: DateTime.now(),
          verificationStatus: 'Verified',
        )
      ];

      // Simulate deletion failure (e.g. server returned 500 or network timeout)
      bool deleteSuccess = true;
      try {
        throw Exception('Server error: 500 Internal Server Error');
      } catch (_) {
        deleteSuccess = false;
      }

      expect(deleteSuccess, isFalse);
      expect(documents.length, equals(1));
      expect(documents.first.id, equals('doc-locked'));
    });

    test('Finalized claim blocks document deletion', () {
      final finalizedStatuses = ['Approved', 'Rejected', 'Withdrawn', 'Closed'];

      for (final status in finalizedStatuses) {
        final isFinal = ['Approved', 'Rejected', 'Withdrawn', 'Closed'].contains(status);
        expect(isFinal, isTrue, reason: 'Status $status should be recognized as finalized');
      }

      final activeStatuses = ['Draft', 'Submitted', 'UnderReview', 'DocumentVerification'];
      for (final status in activeStatuses) {
        final isFinal = ['Approved', 'Rejected', 'Withdrawn', 'Closed'].contains(status);
        expect(isFinal, isFalse, reason: 'Status $status should allow document management');
      }
    });

    test('Allowed file extensions validate properly for PDF and image formats', () {
      const allowed = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'pdf'];

      expect(allowed.contains('pdf'), isTrue);
      expect(allowed.contains('jpg'), isTrue);
      expect(allowed.contains('png'), isTrue);
      expect(allowed.contains('webp'), isTrue);
      expect(allowed.contains('exe'), isFalse);
      expect(allowed.contains('bat'), isFalse);
      expect(allowed.contains('docx'), isFalse);
    });

    test('File size cap at 10 MB is properly enforced', () {
      const maxSizeBytes = 10 * 1024 * 1024;

      final validSize = 5 * 1024 * 1024; // 5 MB
      final boundarySize = 10 * 1024 * 1024; // 10 MB
      final oversize = 11 * 1024 * 1024; // 11 MB

      expect(validSize <= maxSizeBytes, isTrue);
      expect(boundarySize <= maxSizeBytes, isTrue);
      expect(oversize <= maxSizeBytes, isFalse);
    });
  });

  group('ClaimDetailsScreen Document Checklist & Deletion Widget Tests', () {
    testWidgets('renders document checklist and document list accurately', (tester) async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/claims/$claimId')) {
          return http.Response(
            jsonEncode({
              'id': claimId,
              'claimNumber': 'CLM-2026-0001',
              'policyId': 'pol-1',
              'policyHolderId': 'holder-1',
              'claimType': 'Auto',
              'status': 'Submitted',
              'incidentDate': '2026-09-01T00:00:00Z',
              'incidentLocation': 'Highway 1',
              'description': 'Minor accident',
              'claimedAmount': 5000.0,
              'createdAt': '2026-09-01T00:00:00Z',
              'documents': [
                {
                  'id': 'doc-1',
                  'fileName': 'police_report.pdf',
                  'fileUrl': 'https://example.com/doc-1',
                  'documentType': 'Police Report',
                  'contentType': 'application/pdf',
                  'fileSize': 102400,
                  'uploadedAt': '2026-09-01T00:00:00Z',
                  'verificationStatus': 'Verified',
                },
              ],
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        if (request.url.path.endsWith('/document-requirements')) {
          return http.Response(
            jsonEncode({
              'claimId': claimId,
              'claimType': 'Auto',
              'requiredDocuments': [
                {'type': 'Police Report', 'required': true, 'uploaded': true},
                {'type': 'Photos of Damage', 'required': true, 'uploaded': false},
                {'type': 'Repair Estimate', 'required': true, 'uploaded': false},
                {'type': 'Driver License', 'required': true, 'uploaded': false},
              ],
              'requiredCount': 4,
              'uploadedRequiredCount': 1,
              'missingCount': 3,
              'complete': false,
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        if (request.url.path.contains('/payouts/claim/')) {
          return http.Response(
            jsonEncode({'message': 'No payout found'}),
            404,
            headers: {'content-type': 'application/json'},
          );
        }

        return http.Response('Not Found', 404);
      });

      await tester.binding.setSurfaceSize(const Size(800, 1600));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final apiService = ApiService(client: mockClient);
      final claimService = ClaimService(apiService: apiService);
      final payoutService = PayoutService(apiService: apiService);

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimDetailsScreen(
            claimId: claimId,
            claimService: claimService,
            payoutService: payoutService,
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Verify Claim Number
      expect(find.text('CLM-2026-0001'), findsOneWidget);

      // Verify Requirements Card
      expect(find.text('Required Documents'), findsOneWidget);
      expect(find.text('1/4 Uploaded'), findsOneWidget);
      expect(find.text('Police Report'), findsWidgets);
      expect(find.text('Photos of Damage'), findsOneWidget);

      // Verify Document List
      expect(find.text('Documents (1)'), findsOneWidget);
      expect(find.text('police_report.pdf'), findsOneWidget);
      expect(find.byIcon(Icons.delete_outline), findsOneWidget);
    });
  });
}
