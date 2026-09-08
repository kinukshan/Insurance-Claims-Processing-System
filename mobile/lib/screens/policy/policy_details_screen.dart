import 'package:flutter/material.dart';

/// Policy detail screen — Component A (Member 1).
class PolicyDetailsScreen extends StatelessWidget {
  const PolicyDetailsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Policy Details')),
      body: const Center(
        child: Text('Policy Details — TODO'),
      ),
    );
  }
}
