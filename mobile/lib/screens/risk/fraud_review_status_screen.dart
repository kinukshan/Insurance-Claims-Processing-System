import 'package:flutter/material.dart';

/// Fraud review status screen — Component C (Member 3).
///
/// Shows policyholder-safe review statuses only:
/// - Additional Review Required
/// - Under Manual Review
/// - Review Completed
///
/// Does NOT expose: fraud scores, fraud flags, detection rules,
/// or sensitive insurer reasoning.
class FraudReviewStatusScreen extends StatelessWidget {
  const FraudReviewStatusScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Review Status')),
      body: const Center(
        child: Text('Fraud Review Status — TODO'),
      ),
    );
  }
}
