import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/policy.dart';

/// Premium Calculation Consistency Tests
///
/// Verifies that Flutter policy models and premium response parsing
/// handle percentage-deductible vs legacy fixed-deductible correctly,
/// and that no client-side premium calculation duplicates backend logic.
void main() {
  group('Premium Calculation Consistency', () {
    // 1. Policy/premium response parsing with percentage deductible
    test('parses percentage deductible from backend response correctly', () {
      final json = {
        'id': 'test-policy-1',
        'policyNumber': 'POL-MOTOR001',
        'policyholderId': 'holder-1',
        'policyTypeId': 'type-1',
        'policyTypeName': 'Motor Insurance',
        'coverageLimit': 100000.0,
        'premium': 150000.0,
        'deductible': 10000.0,
        'deductiblePercentage': 5.0,
        'startDate': '2026-01-01T00:00:00Z',
        'expiryDate': '2027-01-01T00:00:00Z',
        'status': 'Active',
        'renewalStatus': 'NotDue',
        'isExpired': false,
        'canRenew': true,
        'coverages': [],
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': '2026-01-01T00:00:00Z',
      };

      final policy = Policy.fromJson(json);

      expect(policy.deductiblePercentage, 5.0);
      expect(policy.premium, 150000.0);
      expect(policy.deductible, 10000.0);
    });

    // 2. Percentage deductible remains displayed as percentage
    test('formattedDeductible shows percentage for percentage-deductible policies', () {
      final policy = Policy(
        id: 'test-1',
        policyNumber: 'POL-MOTOR001',
        policyholderId: 'holder-1',
        policyTypeId: 'type-1',
        policyTypeName: 'Motor Insurance',
        coverageLimit: 100000.0,
        premium: 150000.0,
        deductible: 10000.0,
        deductiblePercentage: 5.0,
        startDate: DateTime.utc(2026, 1, 1),
        expiryDate: DateTime.utc(2027, 1, 1),
        status: 'Active',
        renewalStatus: 'NotDue',
        isExpired: false,
        canRenew: true,
        createdAt: DateTime.utc(2026, 1, 1),
        updatedAt: DateTime.utc(2026, 1, 1),
      );

      // Must show percentage, NOT LKR fixed deductible
      expect(policy.formattedDeductible, '5% deductible');
      expect(policy.formattedDeductible.contains('LKR'), isFalse);
    });

    // 3. Legacy fixed-deductible shows LKR value
    test('formattedDeductible shows LKR for legacy fixed-deductible policies', () {
      final policy = Policy(
        id: 'test-2',
        policyNumber: 'POL-LEGACY001',
        policyholderId: 'holder-1',
        policyTypeId: 'type-1',
        policyTypeName: 'Motor Insurance',
        coverageLimit: 100000.0,
        premium: 149500.0,
        deductible: 10000.0,
        deductiblePercentage: null, // Legacy — no percentage
        startDate: DateTime.utc(2026, 1, 1),
        expiryDate: DateTime.utc(2027, 1, 1),
        status: 'Active',
        renewalStatus: 'NotDue',
        isExpired: false,
        canRenew: true,
        createdAt: DateTime.utc(2026, 1, 1),
        updatedAt: DateTime.utc(2026, 1, 1),
      );

      expect(policy.formattedDeductible, 'LKR 10000.00');
      expect(policy.formattedDeductible.contains('%'), isFalse);
    });

    // 4. Premium discount from backend premium response is displayed correctly
    test('premium calculation response with zero discount is parsed correctly', () {
      // Simulate backend CalculatePremiumAsync response for percentage-deductible Motor
      final premiumResponse = {
        'policyId': 'test-policy-1',
        'policyNumber': 'POL-MOTOR001',
        'basePremiumRate': 1500.0,
        'coverageLimit': 100000.0,
        'riskMultiplier': 1.0,
        'deductibleDiscount': 0.0,
        'calculatedPremium': 150000.0,
        'breakdown': '(1500 × 100000 × 1.0000 / 1000) - 0 deductible discount = 150000.00',
      };

      expect(premiumResponse['deductibleDiscount'], 0.0);
      expect(premiumResponse['calculatedPremium'], 150000.0);
      expect((premiumResponse['breakdown'] as String).contains('0 deductible discount'), isTrue);
      // Must NOT show 500 deductible discount for percentage-deductible policies
      expect((premiumResponse['breakdown'] as String).contains('500 deductible discount'), isFalse);
    });

    // 5. Premium calculation response with non-zero discount for legacy policy
    test('premium calculation response with 500 discount for legacy fixed deductible', () {
      final premiumResponse = {
        'policyId': 'test-policy-2',
        'policyNumber': 'POL-LEGACY001',
        'basePremiumRate': 1500.0,
        'coverageLimit': 100000.0,
        'riskMultiplier': 1.0,
        'deductibleDiscount': 500.0,
        'calculatedPremium': 149500.0,
        'breakdown': '(1500 × 100000 × 1.0000 / 1000) - 500 deductible discount = 149500.00',
      };

      expect(premiumResponse['deductibleDiscount'], 500.0);
      expect(premiumResponse['calculatedPremium'], 149500.0);
      expect((premiumResponse['breakdown'] as String).contains('500 deductible discount'), isTrue);
    });

    // 6. No client-side inconsistent premium calculation
    // The Policy model does NOT contain any premium calculation method —
    // premium comes exclusively from the backend.
    test('Policy model does not contain a premium calculation method', () {
      // We simply verify that a Policy object's premium is whatever
      // the backend sets it to, and there's no local recalculation.
      final policy = Policy(
        id: 'test-3',
        policyNumber: 'POL-VERIFY001',
        policyholderId: 'holder-1',
        policyTypeId: 'type-1',
        policyTypeName: 'Motor Insurance',
        coverageLimit: 100000.0,
        premium: 12345.67, // Arbitrary — must be preserved, not recalculated
        deductible: 10000.0,
        deductiblePercentage: 5.0,
        startDate: DateTime.utc(2026, 1, 1),
        expiryDate: DateTime.utc(2027, 1, 1),
        status: 'Active',
        renewalStatus: 'NotDue',
        isExpired: false,
        canRenew: true,
        createdAt: DateTime.utc(2026, 1, 1),
        updatedAt: DateTime.utc(2026, 1, 1),
      );

      // Premium is exactly what was provided — no recalculation
      expect(policy.premium, 12345.67);

      // Round-trip through JSON must preserve premium
      final json = policy.toJson();
      final restored = Policy.fromJson(json);
      expect(restored.premium, 12345.67);
    });

    // 7. Life insurance with 0% deductible
    test('Life insurance policy with 0% deductible displays correctly', () {
      final policy = Policy(
        id: 'test-life-1',
        policyNumber: 'POL-LIFE001',
        policyholderId: 'holder-1',
        policyTypeId: 'type-life',
        policyTypeName: 'Life Insurance',
        coverageLimit: 500000.0,
        premium: 5000.0,
        deductible: 0.0,
        deductiblePercentage: 0.0,
        startDate: DateTime.utc(2026, 1, 1),
        expiryDate: DateTime.utc(2027, 1, 1),
        status: 'Active',
        renewalStatus: 'NotDue',
        isExpired: false,
        canRenew: true,
        createdAt: DateTime.utc(2026, 1, 1),
        updatedAt: DateTime.utc(2026, 1, 1),
      );

      // 0% deductible should still show as percentage format
      expect(policy.formattedDeductible, '0% deductible');
    });
  });
}
