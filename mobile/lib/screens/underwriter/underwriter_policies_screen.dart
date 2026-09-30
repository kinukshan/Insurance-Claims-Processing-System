import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import 'edit_policy_screen.dart';

/// Policy management screen for Underwriters and Admins.
/// Allows viewing all policies across the organization, inspecting coverage,
/// and editing policy terms. Admins can additionally activate Draft policies.
class UnderwriterPoliciesScreen extends StatefulWidget {
  final bool isAdmin;

  const UnderwriterPoliciesScreen({super.key, this.isAdmin = false});

  @override
  State<UnderwriterPoliciesScreen> createState() => _UnderwriterPoliciesScreenState();
}

class _UnderwriterPoliciesScreenState extends State<UnderwriterPoliciesScreen> {
  final PolicyService _policyService = PolicyService();
  final TextEditingController _searchCtrl = TextEditingController();

  List<Policy> _allPolicies = [];
  List<Policy> _filteredPolicies = [];
  bool _loading = true;
  String? _error;
  String _selectedStatus = 'All';

  final List<String> _statuses = ['All', 'Draft', 'Active', 'Expired', 'Cancelled'];
  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  @override
  void initState() {
    super.initState();
    _loadPolicies();
  }

  @override
  void dispose() {
    _searchCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadPolicies() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final policies = await _policyService.getAllPolicies();
      if (!mounted) return;
      setState(() {
        _allPolicies = policies;
        _loading = false;
        _applyFilters();
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _loading = false;
      });
    }
  }

  void _applyFilters() {
    final query = _searchCtrl.text.trim().toLowerCase();
    setState(() {
      _filteredPolicies = _allPolicies.where((p) {
        final matchesStatus = _selectedStatus == 'All' ||
            p.status.toLowerCase() == _selectedStatus.toLowerCase();
        final matchesQuery = query.isEmpty ||
            p.policyNumber.toLowerCase().contains(query) ||
            p.policyTypeName.toLowerCase().contains(query) ||
            p.policyholderId.toLowerCase().contains(query);
        return matchesStatus && matchesQuery;
      }).toList();
    });
  }

  Future<void> _activatePolicy(Policy policy) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Activate Draft Policy'),
        content: Text(
          'Activate policy #${policy.policyNumber}? Once active, policyholders can submit insurance claims against this policy.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: Colors.green, foregroundColor: Colors.white),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Activate Policy'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    try {
      await _policyService.updatePolicy(policy.id, status: 'Active');
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Policy #${policy.policyNumber} activated successfully.'),
          backgroundColor: Colors.green,
        ),
      );
      _loadPolicies();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Activation failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.isAdmin ? 'Policy Administration' : 'Policy Portfolio'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadPolicies),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: AppTheme.primaryTeal));
    }
    if (_error != null) {
      return ErrorRetryView(message: _error!, onRetry: _loadPolicies);
    }

    final draftCount = _allPolicies.where((p) => p.status == 'Draft').length;
    final activeCount = _allPolicies.where((p) => p.status == 'Active').length;

    return RefreshIndicator(
      onRefresh: _loadPolicies,
      child: Column(
        children: [
          // Metric Summary Header
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            color: Colors.grey.shade50,
            child: Row(
              children: [
                _miniMetric('Total Policies', '${_allPolicies.length}', AppTheme.deepNavy),
                const SizedBox(width: 8),
                _miniMetric('Active', '$activeCount', Colors.green.shade700),
                const SizedBox(width: 8),
                _miniMetric('Drafts', '$draftCount', Colors.orange.shade800),
              ],
            ),
          ),

          // Search Field
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchCtrl,
              decoration: InputDecoration(
                hintText: 'Search by policy #, type, policyholder...',
                prefixIcon: const Icon(Icons.search, size: 20),
                suffixIcon: _searchCtrl.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear, size: 18),
                        onPressed: () {
                          _searchCtrl.clear();
                          _applyFilters();
                        },
                      )
                    : null,
                contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
              ),
              onChanged: (_) => _applyFilters(),
            ),
          ),

          // Status Filter Chips
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
            child: Row(
              children: _statuses.map((status) {
                final isSelected = _selectedStatus == status;
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: FilterChip(
                    label: Text(status),
                    selected: isSelected,
                    selectedColor: AppTheme.primaryTeal.withValues(alpha: 0.2),
                    checkmarkColor: AppTheme.primaryTeal,
                    onSelected: (_) {
                      setState(() => _selectedStatus = status);
                      _applyFilters();
                    },
                  ),
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: 6),

          // Policy Cards List
          Expanded(
            child: _filteredPolicies.isEmpty
                ? const EmptyStateView(
                    icon: Icons.shield_outlined,
                    title: 'No Policies Found',
                    description: 'No policies match the selected criteria.',
                  )
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredPolicies.length,
                    itemBuilder: (context, index) {
                      final policy = _filteredPolicies[index];
                      return _buildPolicyCard(policy);
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _miniMetric(String label, String value, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.grey.shade200),
        ),
        child: Column(
          children: [
            Text(value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color)),
            const SizedBox(height: 2),
            Text(
              label,
              style: const TextStyle(fontSize: 10.5, color: AppTheme.textSecondary),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPolicyCard(Policy policy) {
    final isDraft = policy.status == 'Draft';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 1.5,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      policy.policyNumber,
                      style: const TextStyle(
                        fontSize: 16.5,
                        fontWeight: FontWeight.w800,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      policy.policyTypeName,
                      style: const TextStyle(fontSize: 12.5, color: AppTheme.primaryTeal, fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
                StatusBadge(status: policy.status),
              ],
            ),
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Coverage Limit', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                    Text(
                      _currencyFormat.format(policy.coverageLimit),
                      style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Deductible', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                    Text(
                      _currencyFormat.format(policy.deductible),
                      style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text('Expires', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                    Text(
                      _dateFormat.format(policy.expiryDate),
                      style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              'Holder: ${policy.policyholderId}',
              style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary),
            ),
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                // Admin-only Activate action for Draft policies
                if (widget.isAdmin && isDraft) ...[
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.green.shade700,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    ),
                    onPressed: () => _activatePolicy(policy),
                    icon: const Icon(Icons.check_circle_outline, size: 16),
                    label: const Text('Activate Policy', style: TextStyle(fontSize: 12.5)),
                  ),
                  const SizedBox(width: 8),
                ],
                // Edit Policy Terms
                OutlinedButton.icon(
                  onPressed: () async {
                    final updated = await Navigator.push<Policy>(
                      context,
                      MaterialPageRoute(
                        builder: (_) => EditPolicyScreen(policy: policy),
                      ),
                    );
                    if (updated != null) _loadPolicies();
                  },
                  icon: const Icon(Icons.edit, size: 16),
                  label: const Text('Edit Terms', style: TextStyle(fontSize: 12.5)),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
