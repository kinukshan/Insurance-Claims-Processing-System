import 'package:flutter/material.dart';

/// Policies list screen — Component A (Member 1).
class PoliciesScreen extends StatelessWidget {
  const PoliciesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('My Policies')),
      body: const Center(
        child: Text('Policies List — TODO'),
      ),
    );
  }
}
