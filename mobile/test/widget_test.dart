import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/main.dart';

void main() {
  testWidgets('App smoke test', (WidgetTester tester) async {
    // Build the application.
    await tester.pumpWidget(const InsuranceClaimsApp());
    await tester.pump();

    // Verify that the app starts successfully.
    expect(find.byType(InsuranceClaimsApp), findsOneWidget);
  });
}
