import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:insurance_claims_mobile/screens/claims/submit_claim_screen.dart';

void main() {
  group('SubmitClaimScreen widget tests', () {
    testWidgets('renders all form fields', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      // AppBar title
      expect(find.text('Submit Claim'), findsOneWidget);

      // Form labels
      expect(find.text('Policy ID'), findsOneWidget);
      expect(find.text('Claim Type'), findsOneWidget);
      expect(find.text('Incident Date'), findsOneWidget);
      expect(find.text('Incident Location'), findsOneWidget);
      expect(find.text('Description'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Claimed Amount (\$)'), findsOneWidget);

      // Evidence section
      expect(find.text('Evidence'), findsOneWidget);

      // Submit button
      expect(find.text('Submit Claim'), findsWidgets);
    });

    testWidgets('shows validation errors on empty submit', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      // Find and tap submit button
      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      // Validation messages should appear
      expect(find.text('Policy ID is required'), findsOneWidget);
      expect(find.text('Location is required'), findsOneWidget);
      expect(find.text('Description is required'), findsOneWidget);
      expect(find.text('Amount is required'), findsOneWidget);
    });

    testWidgets('shows claim type dropdown with all options', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      // Open dropdown
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();

      // All claim types should be available
      expect(find.text('Auto'), findsWidgets);
      expect(find.text('Home'), findsWidgets);
      expect(find.text('Health'), findsWidgets);
      expect(find.text('Life'), findsWidgets);
      expect(find.text('Travel'), findsWidgets);
      expect(find.text('Property'), findsWidgets);
      expect(find.text('Liability'), findsWidgets);
      expect(find.text('Other'), findsWidgets);
    });

    testWidgets('amount validation rejects non-positive values', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      // Enter zero amount
      final amountField = find.widgetWithText(TextFormField, 'Claimed Amount (\$)');
      await tester.enterText(amountField, '0');

      // Fill other required fields to isolate amount validation
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Policy ID'), 'test-policy',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Incident Location'), 'Test location',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Description'), 'Test description',
      );

      // Submit
      final submitButton = find.widgetWithText(FilledButton, 'Submit Claim');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Must be greater than zero'), findsOneWidget);
    });

    testWidgets('empty evidence section shows placeholder text', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      expect(
        find.textContaining('No evidence attached'),
        findsOneWidget,
      );
    });

    testWidgets('add evidence button exists', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: SubmitClaimScreen()),
      );

      expect(find.text('Add'), findsOneWidget);
      expect(find.byIcon(Icons.add_a_photo), findsOneWidget);
    });
  });
}
