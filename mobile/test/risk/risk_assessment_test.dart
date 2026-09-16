import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/risk_assessment.dart';
import 'package:insurance_claims_mobile/screens/risk/claim_risk_status_screen.dart';
import 'package:insurance_claims_mobile/screens/risk/fraud_review_status_screen.dart';

/// Tests for risk assessment screens — Component C (Member 3).
///
/// Verifies:
/// - Only safe statuses are displayed
/// - No internal fraud scores, flags, or rules exposed
/// - Loading, error, and empty states
/// - Navigation between screens
void main() {
  // ══════════════════════════════════════════════════════════
  // Test 1 — RiskAssessment model exposes only safe fields
  // ══════════════════════════════════════════════════════════

  group('RiskAssessment model', () {
    test('fromJson creates model with safe fields only', () {
      final json = {
        'claimId': 'abc-123',
        'reviewStatus': 'Under Manual Review',
        'lastUpdated': '2026-09-10T14:30:00Z',
      };

      final model = RiskAssessment.fromJson(json);

      expect(model.claimId, 'abc-123');
      expect(model.reviewStatus, 'Under Manual Review');
      expect(model.lastUpdated, isNotNull);
      expect(model.isPending, isTrue);
      expect(model.isComplete, isFalse);
    });

    test('fromJson defaults to "Additional Review Required" when status is null', () {
      final json = {
        'claimId': 'abc-456',
        'reviewStatus': null,
      };

      final model = RiskAssessment.fromJson(json);
      expect(model.reviewStatus, 'Additional Review Required');
    });

    test('isComplete returns true for "Review Completed"', () {
      final model = RiskAssessment(
        id: '1',
        claimId: 'c-1',
        reviewStatus: 'Review Completed',
      );

      expect(model.isComplete, isTrue);
      expect(model.isPending, isFalse);
    });

    test('isPending returns true for both pending statuses', () {
      final additionalReview = RiskAssessment(
        id: '1',
        claimId: 'c-1',
        reviewStatus: 'Additional Review Required',
      );
      final manualReview = RiskAssessment(
        id: '2',
        claimId: 'c-2',
        reviewStatus: 'Under Manual Review',
      );

      expect(additionalReview.isPending, isTrue);
      expect(manualReview.isPending, isTrue);
    });

    test('toJson produces a clean map without internal data', () {
      final model = RiskAssessment(
        id: '1',
        claimId: 'c-1',
        reviewStatus: 'Review Completed',
        lastUpdated: DateTime(2026, 9, 10, 14, 30),
      );

      final json = model.toJson();

      expect(json.containsKey('claimId'), isTrue);
      expect(json.containsKey('reviewStatus'), isTrue);
      // Verify no confidential fields leak
      expect(json.containsKey('riskScore'), isFalse);
      expect(json.containsKey('fraudFlags'), isFalse);
      expect(json.containsKey('riskLevel'), isFalse);
      expect(json.containsKey('recommendation'), isFalse);
    });
  });

  // ══════════════════════════════════════════════════════════
  // Test 2 — ClaimRiskStatusScreen renders loading state
  // ══════════════════════════════════════════════════════════

  group('ClaimRiskStatusScreen', () {
    testWidgets('shows loading indicator initially', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: ClaimRiskStatusScreen(claimId: 'test-claim-id'),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('shows app bar with correct title', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: ClaimRiskStatusScreen(claimId: 'test-claim-id'),
        ),
      );

      expect(find.text('Claim Review Status'), findsOneWidget);
    });

    testWidgets('does not display internal fraud terminology', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: ClaimRiskStatusScreen(claimId: 'test-claim-id'),
        ),
      );

      // Verify no confidential terms are displayed
      expect(find.textContaining('fraud score'), findsNothing);
      expect(find.textContaining('risk score'), findsNothing);
      expect(find.textContaining('detection rule'), findsNothing);
      expect(find.textContaining('threshold'), findsNothing);
    });
  });

  // ══════════════════════════════════════════════════════════
  // Test 3 — FraudReviewStatusScreen renders correctly
  // ══════════════════════════════════════════════════════════

  group('FraudReviewStatusScreen', () {
    testWidgets('shows app bar with "Review Status" title', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: FraudReviewStatusScreen(),
        ),
      );

      expect(find.text('Review Status'), findsOneWidget);
    });

    testWidgets('shows loading indicator on initial load', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: FraudReviewStatusScreen(),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('does not expose internal fraud terms', (WidgetTester tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: FraudReviewStatusScreen(),
        ),
      );

      await tester.pump();

      // Screen should never show confidential fraud terminology
      expect(find.textContaining('fraud flag'), findsNothing);
      expect(find.textContaining('risk level'), findsNothing);
      expect(find.textContaining('escalat'), findsNothing);
    });
  });

  // ══════════════════════════════════════════════════════════
  // Test 4 — Safe status values are the only valid options
  // ══════════════════════════════════════════════════════════

  group('Safe status enforcement', () {
    test('all valid statuses are user-friendly and non-confidential', () {
      const validStatuses = [
        'Additional Review Required',
        'Under Manual Review',
        'Review Completed',
      ];

      for (final status in validStatuses) {
        final model = RiskAssessment(
          id: '1',
          claimId: 'c-1',
          reviewStatus: status,
        );

        // Status should NOT contain internal terms
        expect(model.reviewStatus.toLowerCase().contains('fraud'), isFalse);
        expect(model.reviewStatus.toLowerCase().contains('risk score'), isFalse);
        expect(model.reviewStatus.toLowerCase().contains('flag'), isFalse);
        expect(model.reviewStatus.toLowerCase().contains('escalat'), isFalse);
      }
    });
  });
}
