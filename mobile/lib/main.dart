import 'package:flutter/material.dart';

/// Insurance Claims Mobile App
/// Policyholder-facing application.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
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
        colorSchemeSeed: Colors.blue,
        useMaterial3: true,
      ),
      home: const Scaffold(
        body: Center(
          child: Text('Insurance Claims Mobile App — Under Construction'),
        ),
      ),
      // TODO: Add routing
      // TODO: Add AuthProvider
    );
  }
}
