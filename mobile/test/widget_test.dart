import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:insurance_claims_mobile/main.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';

void main() {
  testWidgets('App smoke test', (WidgetTester tester) async {
    // Create mock client for testing — returns 404 for session restore
    final mockClient = MockClient((request) async {
      return http.Response('', 404);
    });

    final apiService = ApiService(
      baseUrl: 'http://localhost/api',
      client: mockClient,
    );
    final authService = AuthService(api: apiService);

    // Build the application with test services.
    await tester.pumpWidget(InsuranceClaimsApp(
      apiService: apiService,
      authService: authService,
    ));
    await tester.pump();

    // Verify that the app starts successfully.
    expect(find.byType(InsuranceClaimsApp), findsOneWidget);
  });
}
