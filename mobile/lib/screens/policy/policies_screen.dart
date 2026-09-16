import 'package:flutter/material.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';
import 'policy_details_screen.dart';

/// Policies list screen — Component A (Member 1).
/// Displays the policyholder's policies with loading, empty, and error states.
class PoliciesScreen extends StatefulWidget {
  final String policyholderId;

  const PoliciesScreen({super.key, required this.policyholderId});

  @override
  State<PoliciesScreen> createState() => _PoliciesScreenState();
}

class _PoliciesScreenState extends State<PoliciesScreen> {
  final PolicyService _policyService = PolicyService();
  List<Policy> _policies = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _fetchPolicies();
  }

  Future<void> _fetchPolicies() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final policies = await _policyService.getPolicies(widget.policyholderId);
      setState(() {
        _policies = policies;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'Active':
        return Colors.green;
      case 'Expired':
        return Colors.red;
      case 'Lapsed':
        return Colors.orange;
      case 'Cancelled':
        return Colors.grey;
      case 'Draft':
        return Colors.blueGrey;
      default:
        return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('My Policies')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    // Loading state
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    // Error state
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 16),
            Text('Failed to load policies', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: Colors.grey)),
            const SizedBox(height: 16),
            ElevatedButton(onPressed: _fetchPolicies, child: const Text('Retry')),
          ],
        ),
      );
    }

    // Empty state
    if (_policies.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.policy_outlined, size: 64, color: Colors.grey),
            const SizedBox(height: 16),
            Text('No policies found', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            const Text('Your policies will appear here.', style: TextStyle(color: Colors.grey)),
          ],
        ),
      );
    }

    // Policies list
    return RefreshIndicator(
      onRefresh: _fetchPolicies,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _policies.length,
        itemBuilder: (context, index) {
          final policy = _policies[index];
          return Card(
            margin: const EdgeInsets.only(bottom: 12),
            child: ListTile(
              title: Text(policy.policyNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
              subtitle: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const SizedBox(height: 4),
                  Text('Type: ${policy.policyTypeName}'),
                  Text('Premium: \$${policy.premium.toStringAsFixed(2)}'),
                  Text(
                    'Expires: ${policy.expiryDate.day}/${policy.expiryDate.month}/${policy.expiryDate.year}',
                  ),
                ],
              ),
              trailing: Chip(
                label: Text(
                  policy.status,
                  style: const TextStyle(color: Colors.white, fontSize: 12),
                ),
                backgroundColor: _statusColor(policy.status),
              ),
              isThreeLine: true,
              onTap: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => PolicyDetailsScreen(policyId: policy.id),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
