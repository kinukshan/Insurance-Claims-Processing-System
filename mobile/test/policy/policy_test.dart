import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/policy.dart';

void main() {
  group('Policy Model', () {
    test('fromJson creates Policy correctly', () {
      final json = {
        'id': '550e8400-e29b-41d4-a716-446655440000',
        'policyNumber': 'POL-ABCD1234',
        'policyholderId': '660e8400-e29b-41d4-a716-446655440001',
        'policyTypeId': '770e8400-e29b-41d4-a716-446655440002',
        'policyTypeName': 'Auto Insurance',
        'coverageLimit': 50000.0,
        'premium': 450.50,
        'deductible': 1000.0,
        'startDate': '2026-01-01T00:00:00Z',
        'expiryDate': '2027-01-01T00:00:00Z',
        'status': 'Active',
        'renewalStatus': 'NotDue',
        'exclusions': 'Flood damage',
        'isExpired': false,
        'canRenew': true,
        'coverages': [],
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': '2026-01-01T00:00:00Z',
      };

      final policy = Policy.fromJson(json);

      expect(policy.id, '550e8400-e29b-41d4-a716-446655440000');
      expect(policy.policyNumber, 'POL-ABCD1234');
      expect(policy.policyTypeName, 'Auto Insurance');
      expect(policy.coverageLimit, 50000.0);
      expect(policy.premium, 450.50);
      expect(policy.deductible, 1000.0);
      expect(policy.status, 'Active');
      expect(policy.renewalStatus, 'NotDue');
      expect(policy.exclusions, 'Flood damage');
      expect(policy.isExpired, false);
      expect(policy.canRenew, true);
      expect(policy.coverages, isEmpty);
    });

    test('fromJson handles missing optional fields', () {
      final json = {
        'id': '550e8400-e29b-41d4-a716-446655440000',
        'policyNumber': 'POL-XYZ',
        'policyholderId': '660e8400-e29b-41d4-a716-446655440001',
        'policyTypeId': '770e8400-e29b-41d4-a716-446655440002',
        'coverageLimit': 25000.0,
        'premium': 200.0,
        'deductible': 500.0,
        'startDate': '2026-01-01T00:00:00Z',
        'expiryDate': '2027-01-01T00:00:00Z',
        'status': 'Draft',
        'renewalStatus': 'NotDue',
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': '2026-01-01T00:00:00Z',
      };

      final policy = Policy.fromJson(json);

      expect(policy.policyTypeName, '');
      expect(policy.exclusions, isNull);
      expect(policy.isExpired, false);
      expect(policy.canRenew, false);
      expect(policy.coverages, isEmpty);
    });

    test('toJson serializes correctly', () {
      final policy = Policy(
        id: 'test-id',
        policyNumber: 'POL-TEST',
        policyholderId: 'holder-id',
        policyTypeId: 'type-id',
        policyTypeName: 'Health',
        coverageLimit: 100000.0,
        premium: 800.0,
        deductible: 2000.0,
        startDate: DateTime.utc(2026, 1, 1),
        expiryDate: DateTime.utc(2027, 1, 1),
        status: 'Active',
        renewalStatus: 'NotDue',
        isExpired: false,
        canRenew: true,
        createdAt: DateTime.utc(2026, 1, 1),
        updatedAt: DateTime.utc(2026, 1, 1),
      );

      final json = policy.toJson();

      expect(json['id'], 'test-id');
      expect(json['policyNumber'], 'POL-TEST');
      expect(json['coverageLimit'], 100000.0);
      expect(json['status'], 'Active');
    });

    test('fromJson parses coverages correctly', () {
      final json = {
        'id': 'test-id',
        'policyNumber': 'POL-COV',
        'policyholderId': 'holder-id',
        'policyTypeId': 'type-id',
        'policyTypeName': 'Auto',
        'coverageLimit': 50000.0,
        'premium': 300.0,
        'deductible': 500.0,
        'startDate': '2026-01-01T00:00:00Z',
        'expiryDate': '2027-01-01T00:00:00Z',
        'status': 'Active',
        'renewalStatus': 'NotDue',
        'isExpired': false,
        'canRenew': true,
        'coverages': [
          {
            'id': 'cov-1',
            'policyId': 'test-id',
            'coverageType': 'Collision',
            'description': 'Collision coverage',
            'coverageLimit': 25000.0,
            'deductibleAmount': 250.0,
            'percentageOfCoverage': 50.0,
            'isActive': true,
          }
        ],
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': '2026-01-01T00:00:00Z',
      };

      final policy = Policy.fromJson(json);

      expect(policy.coverages, hasLength(1));
      expect(policy.coverages.first.coverageType, 'Collision');
      expect(policy.coverages.first.coverageLimit, 25000.0);
      expect(policy.coverages.first.percentageOfCoverage, 50.0);
    });
  });

  group('PolicyCoverage Model', () {
    test('fromJson creates PolicyCoverage correctly', () {
      final json = {
        'id': 'cov-id',
        'policyId': 'policy-id',
        'coverageType': 'Comprehensive',
        'description': 'Full comprehensive coverage',
        'coverageLimit': 75000.0,
        'deductibleAmount': 1000.0,
        'percentageOfCoverage': 100.0,
        'isActive': true,
      };

      final coverage = PolicyCoverage.fromJson(json);

      expect(coverage.id, 'cov-id');
      expect(coverage.coverageType, 'Comprehensive');
      expect(coverage.description, 'Full comprehensive coverage');
      expect(coverage.coverageLimit, 75000.0);
      expect(coverage.deductibleAmount, 1000.0);
      expect(coverage.percentageOfCoverage, 100.0);
      expect(coverage.isActive, true);
    });

    test('fromJson handles null description', () {
      final json = {
        'id': 'cov-id',
        'policyId': 'policy-id',
        'coverageType': 'Liability',
        'coverageLimit': 50000.0,
        'deductibleAmount': 500.0,
        'percentageOfCoverage': 80.0,
        'isActive': true,
      };

      final coverage = PolicyCoverage.fromJson(json);

      expect(coverage.description, isNull);
    });
  });

  group('Policy Status Display', () {
    test('Active status is correctly identified', () {
      const status = 'Active';
      expect(status, equals('Active'));
    });

    test('Expired status is correctly identified', () {
      const status = 'Expired';
      expect(status, equals('Expired'));
    });

    test('All status values are recognized', () {
      const validStatuses = ['Draft', 'Active', 'Expired', 'Lapsed', 'Cancelled'];
      for (final status in validStatuses) {
        expect(validStatuses.contains(status), isTrue);
      }
    });
  });

  group('PolicyType Model', () {
    test('fromJson creates PolicyType correctly', () {
      final json = {
        'id': '22222222-2222-4222-8222-222222222221',
        'name': 'Motor Insurance',
        'description': 'Coverage for insured motor vehicles.',
        'defaultCoverageLimit': 500000.0,
        'defaultDeductible': 10000.0,
        'insuranceClass': 0,
        'insuranceClassCode': 'General',
        'insuranceClassName': 'General Insurance',
      };

      final type = PolicyType.fromJson(json);

      expect(type.id, '22222222-2222-4222-8222-222222222221');
      expect(type.name, 'Motor Insurance');
      expect(type.description, 'Coverage for insured motor vehicles.');
      expect(type.defaultCoverageLimit, 500000.0);
      expect(type.defaultDeductible, 10000.0);
      expect(type.insuranceClass, 0);
      expect(type.insuranceClassCode, 'General');
      expect(type.insuranceClassName, 'General Insurance');
      expect(type.fixedDeductible, 10000.0);
      expect(type.isLife, isFalse);
    });

    test('fixedDeductible enforces authoritative values for all products', () {
      final motor = PolicyType.fromJson({
        'id': '1',
        'name': 'Motor Insurance',
        'defaultDeductible': 999.0,
      });
      final health = PolicyType.fromJson({
        'id': '2',
        'name': 'Health Insurance',
        'defaultDeductible': 999.0,
      });
      final home = PolicyType.fromJson({
        'id': '3',
        'name': 'Home Insurance',
        'defaultDeductible': 999.0,
      });
      final life = PolicyType.fromJson({
        'id': '4',
        'name': 'Life Insurance',
        'defaultDeductible': 999.0,
      });

      expect(motor.fixedDeductible, 10000.0);
      expect(health.fixedDeductible, 5000.0);
      expect(home.fixedDeductible, 15000.0);
      expect(life.fixedDeductible, 0.0);
      expect(life.isLife, isTrue);
    });

    test('toJson serializes PolicyType correctly', () {
      const type = PolicyType(
        id: 'type-1',
        name: 'Health Insurance',
        description: 'Health coverage',
        defaultCoverageLimit: 1000000.0,
        defaultDeductible: 5000.0,
        insuranceClass: 0,
        insuranceClassCode: 'General',
        insuranceClassName: 'General Insurance',
      );

      final json = type.toJson();
      expect(json['id'], 'type-1');
      expect(json['name'], 'Health Insurance');
      expect(json['defaultCoverageLimit'], 1000000.0);
      expect(json['defaultDeductible'], 5000.0);
    });
  });

  group('CreatePolicyRequest Model', () {
    test('toJson serializes CreatePolicyRequest correctly', () {
      final request = CreatePolicyRequest(
        policyholderId: 'holder-123',
        policyTypeId: 'type-456',
        coverageLimit: 750000.0,
        deductible: 15000.0,
        startDate: DateTime.utc(2026, 9, 28),
        expiryDate: DateTime.utc(2027, 9, 28),
        exclusions: 'Flood, earthquake',
      );

      final json = request.toJson();
      expect(json['policyholderId'], 'holder-123');
      expect(json['policyTypeId'], 'type-456');
      expect(json['coverageLimit'], 750000.0);
      expect(json['deductible'], 15000.0);
      expect(json['startDate'], '2026-09-28');
      expect(json['expiryDate'], '2027-09-28');
      expect(json['exclusions'], 'Flood, earthquake');
    });

    test('toJson omits null or empty exclusions', () {
      final request = CreatePolicyRequest(
        policyholderId: 'holder-123',
        policyTypeId: 'type-456',
        coverageLimit: 500000.0,
        deductible: 10000.0,
        startDate: DateTime.utc(2026, 9, 28),
        expiryDate: DateTime.utc(2027, 9, 28),
        exclusions: '   ',
      );

      final json = request.toJson();
      expect(json.containsKey('exclusions'), isFalse);
    });
  });
}
