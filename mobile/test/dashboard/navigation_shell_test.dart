import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:insurance_claims_mobile/screens/home/main_navigation_shell.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';

void main() {
  Widget createNavigationShellTestApp({int initialIndex = 0}) {
    final mockClient = MockClient((request) async {
      return http.Response('[]', 200);
    });

    final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
    ApiService.shared = api;
    final authService = AuthService(api: api);
    final authProvider = AuthProvider(
      authService: authService,
      apiService: api,
    );

    authProvider.setAuthenticatedUserForTesting(
      const User(
        id: '11111111-1111-1111-1111-111111111111',
        email: 'kasun@test.com',
        firstName: 'Kasun',
        lastName: 'Perera',
        role: 'Policyholder',
      ),
    );

    return MaterialApp(
      home: ChangeNotifierProvider<AuthProvider>.value(
        value: authProvider,
        child: MainNavigationShell(initialIndex: initialIndex),
      ),
    );
  }

  Finder navItem(String label) => find.descendant(
        of: find.byType(BottomNavigationBar),
        matching: find.text(label),
      );

  group('MainNavigationShell Bottom Navigation Widget Tests', () {
    testWidgets('renders all 5 bottom navigation destinations', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      expect(find.byType(BottomNavigationBar), findsOneWidget);
      expect(navItem('Home'), findsOneWidget);
      expect(navItem('Policies'), findsOneWidget);
      expect(navItem('Claims'), findsOneWidget);
      expect(navItem('Payouts'), findsOneWidget);
      expect(navItem('Alerts'), findsOneWidget);
    });

    testWidgets('starts on Home tab by default', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp(initialIndex: 0));
      await tester.pumpAndSettle();

      expect(find.text('Dashboard'), findsOneWidget);
      expect(find.text('Welcome back, Kasun'), findsOneWidget);
    });

    testWidgets('switches to Policies tab when tapped', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Policies'));
      await tester.pumpAndSettle();

      expect(find.text('My Policies'), findsOneWidget);
    });

    testWidgets('switches to Claims tab when tapped', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Claims'));
      await tester.pumpAndSettle();

      expect(find.text('Claim History'), findsOneWidget);
    });

    testWidgets('switches to Payouts tab when tapped', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Payouts'));
      await tester.pumpAndSettle();

      expect(find.text('Payout History'), findsOneWidget);
    });

    testWidgets('switches to Alerts tab when tapped', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      await tester.tap(navItem('Alerts'));
      await tester.pumpAndSettle();

      expect(find.text('Notifications'), findsOneWidget);
    });

    testWidgets('uses IndexedStack for tab state preservation', (tester) async {
      await tester.pumpWidget(createNavigationShellTestApp());
      await tester.pumpAndSettle();

      expect(find.byType(IndexedStack), findsOneWidget);
      final indexedStack = tester.widget<IndexedStack>(find.byType(IndexedStack));
      expect(indexedStack.children.length, 5);
    });
  });
}
