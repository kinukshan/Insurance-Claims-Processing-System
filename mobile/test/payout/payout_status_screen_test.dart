import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/models/payout.dart';
import 'package:insurance_claims_mobile/screens/payout/payout_status_screen.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';

class MockPayoutService extends PayoutService {
  Payout? payoutToReturn;
  bool shouldThrow = false;
  String errorMessage = 'Failed to load payout';
  int callCount = 0;

  @override
  Future<Payout?> getPayoutByClaimId(String claimId) async {
    callCount++;
    if (shouldThrow) {
      throw Exception(errorMessage);
    }
    return payoutToReturn;
  }

  @override
  Future<Payout?> getPayoutById(String id) async {
    callCount++;
    if (shouldThrow) {
      throw Exception(errorMessage);
    }
    return payoutToReturn;
  }
}

void main() {
  late MockPayoutService mockService;

  setUp(() {
    mockService = MockPayoutService();
  });

  Widget createWidgetUnderTest({String? claimId, String? payoutId}) {
    return MaterialApp(
      home: PayoutStatusScreen(
        claimId: claimId,
        payoutId: payoutId,
        payoutService: mockService,
      ),
    );
  }

  group('PayoutStatusScreen Widget Tests', () {
    testWidgets('shows loading indicator initially', (WidgetTester tester) async {
      mockService.payoutToReturn = null;
      await tester.pumpWidget(createWidgetUnderTest(claimId: 'claim-123'));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pumpAndSettle();
    });

    testWidgets('shows empty state when no payout exists', (WidgetTester tester) async {
      mockService.payoutToReturn = null;
      await tester.pumpWidget(createWidgetUnderTest(claimId: 'claim-123'));
      await tester.pumpAndSettle();

      expect(find.text('No payout has been prepared for this claim yet.'), findsOneWidget);
      expect(find.text('Check Again'), findsOneWidget);
    });

    testWidgets('shows error state and retry button on failure', (WidgetTester tester) async {
      mockService.shouldThrow = true;
      mockService.errorMessage = 'Network connection timeout';

      await tester.pumpWidget(createWidgetUnderTest(claimId: 'claim-123'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Network connection timeout'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);

      // Tap retry with success
      mockService.shouldThrow = false;
      mockService.payoutToReturn = Payout(
        id: 'payout-1',
        claimId: 'claim-123',
        approvedClaimAmount: 10000.0,
        coverageLimit: 25000.0,
        deductible: 500.0,
        proposedPayout: 9500.0,
        finalPayout: 9500.0,
        status: '1',
        statusDisplay: 'PendingApproval',
        createdAt: DateTime.utc(2026, 9, 1),
        updatedAt: DateTime.utc(2026, 9, 1),
      );

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('Pending Approval'), findsOneWidget);
    });

    testWidgets('renders payout details and status for approved payout', (WidgetTester tester) async {
      mockService.payoutToReturn = Payout(
        id: 'payout-approved-1',
        claimId: 'claim-456',
        approvedClaimAmount: 20000.0,
        coverageLimit: 50000.0,
        deductible: 1000.0,
        proposedPayout: 19000.0,
        finalPayout: 19000.0,
        status: '2',
        statusDisplay: 'Approved',
        approvedBy: 'underwriter@claims.com',
        approvalTimestamp: DateTime.utc(2026, 9, 2),
        createdAt: DateTime.utc(2026, 9, 1),
        updatedAt: DateTime.utc(2026, 9, 2),
      );

      await tester.pumpWidget(createWidgetUnderTest(payoutId: 'payout-approved-1'));
      await tester.pumpAndSettle();

      expect(find.text('Payout Details'), findsOneWidget);
      expect(find.text('Approved (Pending Disbursement)'), findsOneWidget);
      expect(find.text('Underwriter Approved'), findsOneWidget);
      expect(find.text('LKR 19000.00'), findsWidgets);
    });

    testWidgets('renders disbursement details for paid status', (WidgetTester tester) async {
      mockService.payoutToReturn = Payout(
        id: 'payout-paid-1',
        claimId: 'claim-789',
        approvedClaimAmount: 30000.0,
        coverageLimit: 100000.0,
        deductible: 1500.0,
        proposedPayout: 28500.0,
        finalPayout: 28500.0,
        status: '6',
        statusDisplay: 'Paid',
        paymentReference: 'PAYPAL-TXN-98765',
        approvedBy: 'manager@claims.com',
        approvalTimestamp: DateTime.utc(2026, 9, 5),
        createdAt: DateTime.utc(2026, 9, 1),
        updatedAt: DateTime.utc(2026, 9, 5),
      );

      await tester.pumpWidget(createWidgetUnderTest(claimId: 'claim-789'));
      await tester.pumpAndSettle();

      expect(find.text('Disbursed / Paid'), findsOneWidget);
      expect(find.textContaining('PAYPAL-TXN-98765'), findsWidgets);
    });

    testWidgets('refresh button triggers reload', (WidgetTester tester) async {
      mockService.payoutToReturn = Payout(
        id: 'payout-ref-1',
        claimId: 'claim-ref-1',
        approvedClaimAmount: 5000.0,
        coverageLimit: 10000.0,
        deductible: 200.0,
        proposedPayout: 4800.0,
        finalPayout: 4800.0,
        status: '1',
        statusDisplay: 'PendingApproval',
        createdAt: DateTime.utc(2026, 9, 1),
        updatedAt: DateTime.utc(2026, 9, 1),
      );

      await tester.pumpWidget(createWidgetUnderTest(claimId: 'claim-ref-1'));
      await tester.pumpAndSettle();

      expect(mockService.callCount, 1);

      await tester.tap(find.byTooltip('Refresh Payout'));
      await tester.pumpAndSettle();

      expect(mockService.callCount, 2);
    });
  });
}
