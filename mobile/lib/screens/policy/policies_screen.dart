import 'package:flutter/material.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import 'create_policy_screen.dart';
import 'policy_details_screen.dart';

/// Policies list screen — Component A (Member 1).
/// Displays the policyholder's policies with loading, empty, and error states.
class PoliciesScreen extends StatefulWidget {
  final String? policyholderId;
  final PolicyService? policyService;

  const PoliciesScreen({
    super.key,
    this.policyholderId,
    this.policyService,
  });

  @override
  State<PoliciesScreen> createState() => _PoliciesScreenState();
}

class _PoliciesScreenState extends State<PoliciesScreen> {
  late final PolicyService _policyService;
  final TextEditingController _searchController = TextEditingController();
  List<Policy> _policies = [];
  bool _loading = true;
  String? _error;
  String _searchQuery = '';
  String _selectedStatus = 'All';

  static const List<String> _statusFilters = [
    'All',
    'Active',
    'Draft',
    'Expired',
    'Cancelled',
    'Lapsed',
  ];

  @override
  void initState() {
    super.initState();
    _policyService = widget.policyService ?? PolicyService();
    _fetchPolicies();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _fetchPolicies() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final policies = widget.policyholderId != null && widget.policyholderId!.isNotEmpty
          ? await _policyService.getPolicies(widget.policyholderId!)
          : await _policyService.getMyPolicies();
      if (mounted) {
        setState(() {
          _policies = policies;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load policies. Please try again.';
          _loading = false;
        });
      }
    }
  }

  void _navigateToCreatePolicy() async {
    final created = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => CreatePolicyScreen(
          policyService: _policyService,
          policyholderId: widget.policyholderId,
        ),
      ),
    );
    if (created == true || mounted) {
      _fetchPolicies();
    }
  }

  List<Policy> get _filteredPolicies {
    return _policies.where((policy) {
      final query = _searchQuery.trim().toLowerCase();
      final matchesQuery = query.isEmpty ||
          policy.policyNumber.toLowerCase().contains(query) ||
          policy.policyTypeName.toLowerCase().contains(query);

      final matchesStatus = _selectedStatus == 'All' ||
          policy.status.toLowerCase() == _selectedStatus.toLowerCase();

      return matchesQuery && matchesStatus;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Policies'),
        actions: [
          IconButton(
            key: const Key('create_policy_appbar_btn'),
            icon: const Icon(Icons.add),
            tooltip: 'Create Policy',
            onPressed: _navigateToCreatePolicy,
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _fetchPolicies,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        key: const Key('create_policy_fab'),
        onPressed: _navigateToCreatePolicy,
        icon: const Icon(Icons.add),
        label: const Text('Create Policy'),
        backgroundColor: AppTheme.primaryTeal,
        foregroundColor: Colors.white,
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: AppTheme.primaryTeal),
      );
    }

    if (_error != null) {
      return ErrorRetryView(
        message: _error!,
        onRetry: _fetchPolicies,
      );
    }

    if (_policies.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.shield_outlined, size: 64, color: AppTheme.textSecondary),
              const SizedBox(height: 16),
              const Text(
                'No Policies Found',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: AppTheme.deepNavy,
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'You do not have any insurance policies linked to your account.',
                textAlign: TextAlign.center,
                style: TextStyle(color: AppTheme.textSecondary),
              ),
              const SizedBox(height: 20),
              ElevatedButton.icon(
                key: const Key('create_policy_empty_btn'),
                onPressed: _navigateToCreatePolicy,
                icon: const Icon(Icons.add),
                label: const Text('Create Policy'),
              ),
            ],
          ),
        ),
      );
    }


    final filtered = _filteredPolicies;

    return Column(
      children: [
        // Search bar
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
          child: TextField(
            controller: _searchController,
            decoration: InputDecoration(
              hintText: 'Search policies (number, type)...',
              prefixIcon: const Icon(Icons.search, size: 20),
              suffixIcon: _searchQuery.isNotEmpty
                  ? IconButton(
                      icon: const Icon(Icons.clear, size: 18),
                      onPressed: () {
                        setState(() {
                          _searchController.clear();
                          _searchQuery = '';
                        });
                      },
                    )
                  : null,
              contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide(color: Colors.grey.shade300),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide(color: Colors.grey.shade300),
              ),
            ),
            onChanged: (val) => setState(() => _searchQuery = val),
          ),
        ),

        // Status filter chips
        SizedBox(
          height: 44,
          child: ListView.separated(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            scrollDirection: Axis.horizontal,
            itemCount: _statusFilters.length,
            separatorBuilder: (_, _) => const SizedBox(width: 8),
            itemBuilder: (context, idx) {
              final status = _statusFilters[idx];
              final isSelected = _selectedStatus == status;
              return ChoiceChip(
                label: Text(status),
                selected: isSelected,
                onSelected: (selected) {
                  if (selected) {
                    setState(() => _selectedStatus = status);
                  }
                },
              );
            },
          ),
        ),
        const SizedBox(height: 8),

        // Filtered list or empty search view
        Expanded(
          child: filtered.isEmpty
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.search_off, size: 54, color: AppTheme.textSecondary),
                        const SizedBox(height: 12),
                        const Text(
                          'No matching policies',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                            color: AppTheme.deepNavy,
                          ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'No policies found matching your search or status filter.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: AppTheme.textSecondary),
                        ),
                        const SizedBox(height: 16),
                        OutlinedButton(
                          onPressed: () {
                            setState(() {
                              _searchController.clear();
                              _searchQuery = '';
                              _selectedStatus = 'All';
                            });
                          },
                          child: const Text('Reset Filters'),
                        ),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
                  color: AppTheme.primaryTeal,
                  onRefresh: _fetchPolicies,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: filtered.length,
                    separatorBuilder: (context, index) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final policy = filtered[index];
                      return Card(
                        elevation: 0.5,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                          side: BorderSide(color: Colors.grey.shade200),
                        ),
                        child: InkWell(
                          borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                          onTap: () {
                            Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (_) => PolicyDetailsScreen(policyId: policy.id),
                              ),
                            );
                          },
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Expanded(
                                      child: Text(
                                        policy.policyNumber,
                                        style: const TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w700,
                                          color: AppTheme.deepNavy,
                                        ),
                                      ),
                                    ),
                                    StatusBadge(status: policy.status),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  policy.policyTypeName,
                                  style: const TextStyle(
                                    fontSize: 14,
                                    fontWeight: FontWeight.w600,
                                    color: AppTheme.primaryTeal,
                                  ),
                                ),
                                if (policy.status.toLowerCase() == 'draft') ...[
                                  const SizedBox(height: 6),
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                    decoration: BoxDecoration(
                                      color: Colors.amber.shade50,
                                      borderRadius: BorderRadius.circular(6),
                                      border: Border.all(color: Colors.amber.shade300),
                                    ),
                                    child: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Icon(Icons.info_outline, size: 13, color: Colors.amber.shade800),
                                        const SizedBox(width: 4),
                                        Text(
                                          'Awaiting Admin Activation — Ineligible for claims',
                                          style: TextStyle(
                                            fontSize: 11,
                                            color: Colors.amber.shade900,
                                            fontWeight: FontWeight.w500,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                                const SizedBox(height: 8),
                                const Divider(height: 1),
                                const SizedBox(height: 8),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text(
                                      'Coverage: LKR ${policy.coverageLimit.toStringAsFixed(0)}',
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: AppTheme.textSecondary,
                                      ),
                                    ),
                                    Text(
                                      'Expires: ${policy.expiryDate.day}/${policy.expiryDate.month}/${policy.expiryDate.year}',
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: AppTheme.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    },
                  ),
                ),
        ),
      ],
    );
  }
}
