import 'package:flutter/material.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';

/// Policy detail screen — Component A (Member 1).
/// Displays full policy details, coverage, premium, expiry, and renewal status.
class PolicyDetailsScreen extends StatefulWidget {
  final String policyId;

  const PolicyDetailsScreen({super.key, required this.policyId});

  @override
  State<PolicyDetailsScreen> createState() => _PolicyDetailsScreenState();
}

class _PolicyDetailsScreenState extends State<PolicyDetailsScreen> {
  final PolicyService _policyService = PolicyService();
  Policy? _policy;
  List<PolicyCoverage> _coverages = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _fetchDetails();
  }

  Future<void> _fetchDetails() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _policyService.getPolicyById(widget.policyId),
        _policyService.getCoverage(widget.policyId),
      ]);
      setState(() {
        _policy = results[0] as Policy;
        _coverages = results[1] as List<PolicyCoverage>;
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
      appBar: AppBar(title: const Text('Policy Details')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 16),
            Text('Failed to load policy', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: Colors.grey)),
            const SizedBox(height: 16),
            ElevatedButton(onPressed: _fetchDetails, child: const Text('Retry')),
          ],
        ),
      );
    }

    if (_policy == null) {
      return const Center(child: Text('Policy not found.'));
    }

    final policy = _policy!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        policy.policyNumber,
                        style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
                      ),
                      Chip(
                        label: Text(policy.status, style: const TextStyle(color: Colors.white)),
                        backgroundColor: _statusColor(policy.status),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text('Type: ${policy.policyTypeName}', style: const TextStyle(color: Colors.grey)),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Policy Information
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Policy Information', style: Theme.of(context).textTheme.titleMedium),
                  const Divider(),
                  _infoRow('Coverage Limit', '\$${policy.coverageLimit.toStringAsFixed(2)}'),
                  _infoRow('Premium', '\$${policy.premium.toStringAsFixed(2)}'),
                  _infoRow('Deductible', '\$${policy.deductible.toStringAsFixed(2)}'),
                  _infoRow(
                    'Start Date',
                    '${policy.startDate.day}/${policy.startDate.month}/${policy.startDate.year}',
                  ),
                  _infoRow(
                    'Expiry Date',
                    '${policy.expiryDate.day}/${policy.expiryDate.month}/${policy.expiryDate.year}',
                  ),
                  _infoRow('Renewal Status', policy.renewalStatus),
                  _infoRow('Expired', policy.isExpired ? 'Yes' : 'No'),
                  if (policy.exclusions != null && policy.exclusions!.isNotEmpty)
                    _infoRow('Exclusions', policy.exclusions!),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Coverage Details
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Coverage Details', style: Theme.of(context).textTheme.titleMedium),
                  const Divider(),
                  if (_coverages.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Text('No coverage details available.', style: TextStyle(color: Colors.grey)),
                    )
                  else
                    ..._coverages.map(
                      (cov) => Padding(
                        padding: const EdgeInsets.symmetric(vertical: 6),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(cov.coverageType, style: const TextStyle(fontWeight: FontWeight.w600)),
                            if (cov.description != null) Text(cov.description!, style: const TextStyle(color: Colors.grey)),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text('Limit: \$${cov.coverageLimit.toStringAsFixed(2)}'),
                                Text('Deductible: \$${cov.deductibleAmount.toStringAsFixed(2)}'),
                              ],
                            ),
                            Text('Coverage: ${cov.percentageOfCoverage}%'),
                            const Divider(),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 140,
            child: Text(label, style: const TextStyle(fontWeight: FontWeight.w500, color: Colors.black87)),
          ),
          Expanded(child: Text(value, style: const TextStyle(color: Colors.black54))),
        ],
      ),
    );
  }
}
