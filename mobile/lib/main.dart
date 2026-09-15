import 'package:flutter/material.dart';
import 'screens/claims/claim_history_screen.dart';
import 'screens/claims/submit_claim_screen.dart';
import 'screens/claims/claim_details_screen.dart';
import 'screens/claims/claim_status_screen.dart';

/// Insurance Claims Mobile App
/// Policyholder-facing application.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
///
/// MODIFICATION (Arulkumaran): Added named routes for claims screens.
void main() {
  runApp(const InsuranceClaimsApp());
}

class InsuranceClaimsApp extends StatelessWidget {
  const InsuranceClaimsApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Insurance Claims',
      theme: ThemeData(
        colorSchemeSeed: Colors.indigo,
        useMaterial3: true,
        brightness: Brightness.light,
      ),
      darkTheme: ThemeData(
        colorSchemeSeed: Colors.indigo,
        useMaterial3: true,
        brightness: Brightness.dark,
      ),
      initialRoute: '/claims/history',
      routes: {
        '/claims/history': (context) => const ClaimHistoryScreen(),
        '/claims/submit': (context) => const SubmitClaimScreen(),
        '/claims/details': (context) => const ClaimDetailsScreen(),
        '/claims/status': (context) => const ClaimStatusScreen(),
      },
    );
  }
}
