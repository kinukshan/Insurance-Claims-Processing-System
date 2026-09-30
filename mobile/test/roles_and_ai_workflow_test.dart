import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:insurance_claims_mobile/main.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/models/ai_workflow_models.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/screens/home/main_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/adjuster/adjuster_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/underwriter/underwriter_navigation_shell.dart';
import 'package:insurance_claims_mobile/screens/admin/admin_navigation_shell.dart';
import 'package:http/testing.dart';
import 'package:http/http.dart' as http;

Widget _buildAuthTestApp({required User user}) {
  final mockClient = MockClient((r) async => http.Response('[]', 200));
  final apiService = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
  final authService = AuthService(api: apiService);
  final authProvider = AuthProvider(authService: authService, apiService: apiService);
  authProvider.setAuthenticatedUserForTesting(user);

  return MultiProvider(
    providers: [
      ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
      Provider<ApiService>.value(value: apiService),
    ],
    child: const MaterialApp(home: AuthGate()),
  );
}

void main() {
  group('AI Workflow Models Serialization', () {
    test('CoverageValidationResult parses valid payload correctly', () {
      final json = {
        'isValid': true,
        'isCovered': true,
        'coverageLimit': 50000.0,
        'deductibleAmount': 500.0,
        'coverageType': 'Comprehensive',
        'issues': ['Approaching annual limit'],
      };

      final result = CoverageValidationResult.fromJson(json);
      expect(result.isValid, true);
      expect(result.isCovered, true);
      expect(result.coverageLimit, 50000.0);
      expect(result.deductibleAmount, 500.0);
      expect(result.coverageType, 'Comprehensive');
      expect(result.issues.length, 1);
      expect(result.issues.first, 'Approaching annual limit');
    });

    test('DocumentVerificationResult parses AI Agent verification payload', () {
      final json = {
        'complete': true,
        'missingItems': <String>[],
        'inconsistencies': [
          {
            'field': 'DamageDate',
            'description': 'Minor time variance',
            'severity': 'Low',
          }
        ],
        'warnings': ['Document quality medium'],
        'aiUsed': true,
        'aiProvider': 'Google Gemini',
        'aiModel': 'gemini-1.5-flash',
        'reasoningSummary': 'All required documents verified by AI Agent.',
        'fallbackUsed': false,
        'attemptId': 'att-1234',
      };

      final result = DocumentVerificationResult.fromJson(json);
      expect(result.complete, true);
      expect(result.aiUsed, true);
      expect(result.aiProvider, 'Google Gemini');
      expect(result.inconsistencies.length, 1);
      expect(result.inconsistencies.first.field, 'DamageDate');
      expect(result.inconsistencies.first.severity, 'Low');
      expect(result.missingItems.isEmpty, true);
    });

    test('StaffRiskAssessment parses fraud and risk scoring payload', () {
      final json = {
        'id': 'risk-789',
        'claimId': 'claim-123',
        'claimNumber': 'CLM-2026-001',
        'riskScore': 22.0,
        'riskLevel': 'Low',
        'recommendation': 'Approve',
        'summary': 'Consistent damage photos and police report.',
        'fraudFlagCount': 1,
        'hasFraudCase': false,
        'aiUsed': true,
        'aiProvider': 'Google Gemini',
        'aiModel': 'gemini-1.5-pro',
        'reasoningSummary': 'Low behavioral risk flags.',
        'fallbackUsed': false,
        'assessmentTimestamp': '2026-09-28T12:00:00Z',
        'flags': [
          {
            'code': 'RULE-01',
            'description': 'First-party claim',
            'severity': 'Low',
          }
        ],
      };

      final result = StaffRiskAssessment.fromJson(json);
      expect(result.id, 'risk-789');
      expect(result.claimId, 'claim-123');
      expect(result.riskScore, 22.0);
      expect(result.riskLevel, 'Low');
      expect(result.hasFraudCase, false);
      expect(result.flags.length, 1);
      expect(result.flags.first.code, 'RULE-01');
    });

    test('StaffRiskAssessment handles integer enums from backend DTO correctly', () {
      final json = {
        'id': 'risk-int-enum',
        'claimId': 'claim-456',
        'claimNumber': 'CLM-2026-002',
        'riskScore': 78.5,
        'riskLevel': 2,
        'recommendation': 1,
        'summary': 'High risk detected due to rapid submission.',
        'fraudFlagCount': 1,
        'hasFraudCase': true,
        'aiUsed': false,
        'fallbackUsed': true,
        'assessmentTimestamp': '2026-09-30T10:00:00Z',
        'flags': [
          {
            'flagType': 1,
            'flagTypeDisplay': 'RapidSubmission',
            'description': 'Claim submitted within 14 days of policy start',
            'severity': 2,
            'severityDisplay': 'High',
            'source': 0,
            'sourceDisplay': 'RulesEngine',
          },
        ],
      };

      final result = StaffRiskAssessment.fromJson(json);
      expect(result.id, 'risk-int-enum');
      expect(result.riskScore, 78.5);
      expect(result.riskLevel, 'High');
      expect(result.recommendation, 'FurtherInvestigation');
      expect(result.flags.first.code, 'RapidSubmission');
      expect(result.flags.first.severity, 'High');
    });

    test('PayoutValidationResult parses Safety Agent check payload', () {
      final json = {
        'valid': true,
        'violations': <String>[],
        'requiresHumanApproval': true,
        'agentId': 'SafetyValidationAgent',
        'summary': 'Proposed payout within policy limits and deductible accounted.',
        'aiUsed': true,
        'aiProvider': 'Google Gemini',
      };

      final result = PayoutValidationResult.fromJson(json);
      expect(result.valid, true);
      expect(result.requiresHumanApproval, true);
      expect(result.agentId, 'SafetyValidationAgent');
      expect(result.violations.isEmpty, true);
      expect(result.aiUsed, true);
    });

    test('PaymentExecutionResult parses disbursement payload', () {
      final json = {
        'success': true,
        'status': 'Paid',
        'transactionId': 'TXN-MOCK-9999',
        'provider': 'Mock Payment Gateway',
        'amount': 3500.0,
        'errorMessage': null,
      };

      final result = PaymentExecutionResult.fromJson(json);
      expect(result.success, true);
      expect(result.status, 'Paid');
      expect(result.transactionId, 'TXN-MOCK-9999');
      expect(result.provider, 'Mock Payment Gateway');
      expect(result.amount, 3500.0);
    });
  });

  group('AuthGate Role Navigation Shells', () {
    testWidgets('routes Policyholder to MainNavigationShell', (tester) async {
      final user = User(
        id: 'user-ph',
        email: 'holder@test.com',
        firstName: 'Jane',
        lastName: 'Policyholder',
        role: 'Policyholder',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();
      expect(find.byType(MainNavigationShell), findsOneWidget);
    });

    testWidgets('routes ClaimsAdjuster to AdjusterNavigationShell', (tester) async {
      final user = User(
        id: 'user-adj',
        email: 'adjuster@test.com',
        firstName: 'Alex',
        lastName: 'Adjuster',
        role: 'ClaimsAdjuster',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();
      expect(find.byType(AdjusterNavigationShell), findsOneWidget);
    });

    testWidgets('routes Underwriter to UnderwriterNavigationShell', (tester) async {
      final user = User(
        id: 'user-uw',
        email: 'underwriter@test.com',
        firstName: 'Sam',
        lastName: 'Underwriter',
        role: 'Underwriter',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();
      expect(find.byType(UnderwriterNavigationShell), findsOneWidget);
    });

    testWidgets('routes Admin to AdminNavigationShell', (tester) async {
      final user = User(
        id: 'user-admin',
        email: 'admin@test.com',
        firstName: 'Chief',
        lastName: 'Admin',
        role: 'Admin',
      );

      await tester.pumpWidget(_buildAuthTestApp(user: user));
      await tester.pumpAndSettle();
      expect(find.byType(AdminNavigationShell), findsOneWidget);
    });
  });
}
