import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/payout.dart';

void main() {
  group('Payout Model', () {
    test('fromJson creates Payout with correct fields', () {
      final json = {
        'id': 'test-id-001',
        'claimId': 'claim-id-001',
        'approvedClaimAmount': 15000.0,
        'coverageLimit': 50000.0,
        'deductible': 500.0,
        'proposedPayout': 14500.0,
        'finalPayout': 14500.0,
        'status': '1',
        'statusDisplay': 'PendingApproval',
        'approvedBy': null,
        'approvalTimestamp': null,
        'paymentReference': null,
        'createdAt': '2026-09-10T10:00:00Z',
        'updatedAt': '2026-09-10T10:00:00Z',
      };

      final payout = Payout.fromJson(json);

      expect(payout.id, 'test-id-001');
      expect(payout.claimId, 'claim-id-001');
      expect(payout.approvedClaimAmount, 15000.0);
      expect(payout.coverageLimit, 50000.0);
      expect(payout.deductible, 500.0);
      expect(payout.finalPayout, 14500.0);
      expect(payout.statusDisplay, 'PendingApproval');
    });

    test('toJson produces correct map', () {
      final payout = Payout(
        id: 'test-id-001',
        claimId: 'claim-id-001',
        approvedClaimAmount: 15000.0,
        coverageLimit: 50000.0,
        deductible: 500.0,
        proposedPayout: 14500.0,
        finalPayout: 14500.0,
        status: '1',
        statusDisplay: 'PendingApproval',
        createdAt: DateTime.utc(2026, 9, 10),
        updatedAt: DateTime.utc(2026, 9, 10),
      );

      final json = payout.toJson();

      expect(json['id'], 'test-id-001');
      expect(json['finalPayout'], 14500.0);
      expect(json['statusDisplay'], 'PendingApproval');
    });

    test('formattedPayout returns dollar formatted string', () {
      final payout = Payout(
        id: 'test-id',
        claimId: 'claim-id',
        approvedClaimAmount: 15000,
        coverageLimit: 50000,
        deductible: 500,
        proposedPayout: 14500,
        finalPayout: 14500,
        status: '2',
        statusDisplay: 'Approved',
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

      expect(payout.formattedPayout, '\$14500.00');
    });

    test('isTerminal returns true for Paid status', () {
      final payout = Payout(
        id: 'test-id',
        claimId: 'claim-id',
        approvedClaimAmount: 15000,
        coverageLimit: 50000,
        deductible: 500,
        proposedPayout: 14500,
        finalPayout: 14500,
        status: '6',
        statusDisplay: 'Paid',
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

      expect(payout.isTerminal, true);
      expect(payout.isProcessing, false);
    });

    test('isTerminal returns false for Processing status', () {
      final payout = Payout(
        id: 'test-id',
        claimId: 'claim-id',
        approvedClaimAmount: 15000,
        coverageLimit: 50000,
        deductible: 500,
        proposedPayout: 14500,
        finalPayout: 14500,
        status: '5',
        statusDisplay: 'Processing',
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

      expect(payout.isTerminal, false);
      expect(payout.isProcessing, true);
    });

    test('fromJson handles optional payment reference', () {
      final json = {
        'id': 'test-id',
        'claimId': 'claim-id',
        'approvedClaimAmount': 14500,
        'coverageLimit': 50000,
        'deductible': 500,
        'proposedPayout': 14000,
        'finalPayout': 14000,
        'status': '6',
        'statusDisplay': 'Paid',
        'approvedBy': 'Admin',
        'approvalTimestamp': '2026-09-10T12:00:00Z',
        'paymentReference': 'SANDBOX-REF-001',
        'createdAt': '2026-09-10T10:00:00Z',
        'updatedAt': '2026-09-10T12:00:00Z',
      };

      final payout = Payout.fromJson(json);

      expect(payout.paymentReference, 'SANDBOX-REF-001');
      expect(payout.approvedBy, 'Admin');
      expect(payout.approvalTimestamp, isNotNull);
    });
  });
}
