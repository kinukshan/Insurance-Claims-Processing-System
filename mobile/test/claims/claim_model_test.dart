import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/claim.dart';
import 'package:insurance_claims_mobile/models/claim_document.dart';

void main() {
  group('Claim model', () {
    final sampleJson = {
      'id': '11111111-1111-1111-1111-111111111111',
      'policyId': '22222222-2222-2222-2222-222222222222',
      'policyHolderId': '33333333-3333-3333-3333-333333333333',
      'claimNumber': 'CLM-20240101-0001',
      'claimType': 'Auto',
      'description': 'Rear-end collision at intersection',
      'claimedAmount': 5000.50,
      'incidentDate': '2024-06-15T00:00:00Z',
      'incidentLocation': '123 Test Street',
      'status': 'Draft',
      'submittedAt': null,
      'createdAt': '2024-06-20T10:00:00Z',
      'updatedAt': '2024-06-20T10:00:00Z',
      'documents': [
        {
          'id': '44444444-4444-4444-4444-444444444444',
          'claimId': '11111111-1111-1111-1111-111111111111',
          'fileName': 'damage_photo.jpg',
          'fileUrl': '/uploads/damage_photo.jpg',
          'documentType': 'Photos of Damage',
          'contentType': 'image/jpeg',
          'fileSize': 245760,
          'uploadedAt': '2024-06-20T10:30:00Z',
          'verificationStatus': 'Pending',
        }
      ],
    };

    test('fromJson parses all fields correctly', () {
      final claim = Claim.fromJson(sampleJson);

      expect(claim.id, '11111111-1111-1111-1111-111111111111');
      expect(claim.policyId, '22222222-2222-2222-2222-222222222222');
      expect(claim.policyHolderId, '33333333-3333-3333-3333-333333333333');
      expect(claim.claimNumber, 'CLM-20240101-0001');
      expect(claim.claimType, 'Auto');
      expect(claim.description, 'Rear-end collision at intersection');
      expect(claim.claimedAmount, 5000.50);
      expect(claim.incidentLocation, '123 Test Street');
      expect(claim.status, 'Draft');
      expect(claim.submittedAt, isNull);
      expect(claim.createdAt, isA<DateTime>());
      expect(claim.documents.length, 1);
    });

    test('fromJson handles null submittedAt', () {
      final claim = Claim.fromJson(sampleJson);
      expect(claim.submittedAt, isNull);
    });

    test('fromJson handles null documents list', () {
      final jsonWithoutDocs = Map<String, dynamic>.from(sampleJson);
      jsonWithoutDocs.remove('documents');
      final claim = Claim.fromJson(jsonWithoutDocs);
      expect(claim.documents, isEmpty);
    });

    test('fromJson parses submittedAt when present', () {
      final jsonWithSubmitted = Map<String, dynamic>.from(sampleJson);
      jsonWithSubmitted['submittedAt'] = '2024-06-21T08:00:00Z';
      final claim = Claim.fromJson(jsonWithSubmitted);
      expect(claim.submittedAt, isNotNull);
      expect(claim.submittedAt, isA<DateTime>());
    });

    test('toJson produces correct structure', () {
      final claim = Claim.fromJson(sampleJson);
      final json = claim.toJson();

      expect(json['id'], claim.id);
      expect(json['claimNumber'], claim.claimNumber);
      expect(json['claimType'], 'Auto');
      expect(json['claimedAmount'], 5000.50);
      expect(json['submittedAt'], isNull);
    });

    test('roundtrip fromJson/toJson preserves data', () {
      final original = Claim.fromJson(sampleJson);
      final json = original.toJson();

      // Core fields survive roundtrip
      expect(json['id'], sampleJson['id']);
      expect(json['claimNumber'], sampleJson['claimNumber']);
      expect(json['claimType'], sampleJson['claimType']);
      expect(json['status'], sampleJson['status']);
    });
  });

  group('ClaimDocument model', () {
    final docJson = {
      'id': '44444444-4444-4444-4444-444444444444',
      'claimId': '11111111-1111-1111-1111-111111111111',
      'fileName': 'police_report.pdf',
      'fileUrl': '/uploads/police_report.pdf',
      'documentType': 'Police Report',
      'contentType': 'application/pdf',
      'fileSize': 1048576,
      'uploadedAt': '2024-06-20T10:30:00Z',
      'verificationStatus': 'Verified',
    };

    test('fromJson parses all fields correctly', () {
      final doc = ClaimDocument.fromJson(docJson);

      expect(doc.id, '44444444-4444-4444-4444-444444444444');
      expect(doc.claimId, '11111111-1111-1111-1111-111111111111');
      expect(doc.fileName, 'police_report.pdf');
      expect(doc.documentType, 'Police Report');
      expect(doc.contentType, 'application/pdf');
      expect(doc.fileSize, 1048576);
      expect(doc.verificationStatus, 'Verified');
      expect(doc.uploadedAt, isA<DateTime>());
    });

    test('formattedSize returns bytes for small files', () {
      final smallDoc = ClaimDocument.fromJson({
        ...docJson,
        'fileSize': 512,
      });
      expect(smallDoc.formattedSize, '512 B');
    });

    test('formattedSize returns KB for medium files', () {
      final kbDoc = ClaimDocument.fromJson({
        ...docJson,
        'fileSize': 2048,
      });
      expect(kbDoc.formattedSize, '2.0 KB');
    });

    test('formattedSize returns MB for large files', () {
      final doc = ClaimDocument.fromJson(docJson);
      expect(doc.formattedSize, '1.0 MB');
    });

    test('toJson produces correct structure', () {
      final doc = ClaimDocument.fromJson(docJson);
      final json = doc.toJson();

      expect(json['id'], docJson['id']);
      expect(json['fileName'], 'police_report.pdf');
      expect(json['documentType'], 'Police Report');
      expect(json['verificationStatus'], 'Verified');
    });

    test('fromJson defaults verificationStatus to Pending', () {
      final jsonWithoutStatus = Map<String, dynamic>.from(docJson);
      jsonWithoutStatus.remove('verificationStatus');
      final doc = ClaimDocument.fromJson(jsonWithoutStatus);
      expect(doc.verificationStatus, 'Pending');
    });
  });
}
