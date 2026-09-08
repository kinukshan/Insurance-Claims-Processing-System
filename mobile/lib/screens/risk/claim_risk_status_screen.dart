import 'package:flutter/material.dart';

/// Claim risk status screen — Component C (Member 3).
///
/// Shows policyholder-safe review statuses only:
/// - Additional Review Required
/// - Under Manual Review
/// - Review Completed
///
/// Does NOT expose: fraud scores, fraud flags, detection rules,
/// or sensitive insurer reasoning.
class ClaimRiskStatusScreen extends StatelessWidget {
  const ClaimRiskStatusScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Claim Review Status')),
      body: const Center(
        child: Text('Claim Review Status — TODO'),
      ),
    );
  }
}
