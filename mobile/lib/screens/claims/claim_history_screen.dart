import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';
import '../../models/claim.dart';
import '../../providers/auth_provider.dart';
import '../../services/claim_service.dart';
import '../../services/api_service.dart';

/// Claim history screen — Component B (Member 2).
/// Policyholder-facing list of submitted claims with search, filtering, and pull-to-refresh.
class ClaimHistoryScreen extends StatefulWidget {
  final ClaimService? claimService;

  const ClaimHistoryScreen({super.key, this.claimService});

  @override
  State<ClaimHistoryScreen> createState() => _ClaimHistoryScreenState();
}

class _ClaimHistoryScreenState extends State<ClaimHistoryScreen> {
  late final ClaimService _claimService;
  final TextEditingController _searchController = TextEditingController();
  List<Claim>? _claims;
  bool _loading = true;
  String? _error;
  String _searchQuery = '';
  String _selectedStatus = 'All';

  static const List<String> _statusFilters = [
    'All',
    'Draft',
    'Submitted',
    'UnderReview',
    'Approved',
    'Withdrawn',
  ];

  @override
  void initState() {
    super.initState();
    _claimService = widget.claimService ?? ClaimService();
    _loadClaims();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadClaims() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final claims = await _claimService.getMyClaims();
      if (mounted) setState(() => _claims = claims);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  List<Claim> get _filteredClaims {
    if (_claims == null) return [];
    return _claims!.where((claim) {
      final query = _searchQuery.trim().toLowerCase();
      final matchesQuery = query.isEmpty ||
          claim.claimNumber.toLowerCase().contains(query) ||
          claim.description.toLowerCase().contains(query) ||
          claim.claimType.toLowerCase().contains(query) ||
          claim.incidentLocation.toLowerCase().contains(query);

      final matchesStatus = _selectedStatus == 'All' ||
          claim.status.toLowerCase() == _selectedStatus.toLowerCase();

      return matchesQuery && matchesStatus;
    }).toList();
  }

  Color _statusColor(String status) {
    return switch (status) {
      'Draft' => Colors.grey,
      'Submitted' => Colors.indigo,
      'UnderReview' || 'DocumentVerification' => Colors.blue,
      'AdditionalDocumentsRequired' => Colors.orange,
      'RiskAssessment' || 'PendingApproval' => Colors.amber.shade700,
      'Approved' || 'PayoutProcessing' => Colors.green,
      'Rejected' => Colors.red,
      'Withdrawn' || 'Closed' => Colors.blueGrey,
      _ => Colors.grey,
    };
  }

  String _formatStatus(String status) {
    return status.replaceAllMapped(
      RegExp(r'([A-Z])'),
      (match) => ' ${match.group(0)}',
    ).trim();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Claim History'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _loadClaims,
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Log out',
            onPressed: () {
              context.read<AuthProvider>().logout();
            },
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final result = await Navigator.pushNamed(context, '/claims/submit');
          if (result == true) _loadClaims();
        },
        icon: const Icon(Icons.add),
        label: const Text('New Claim'),
      ),
      body: _buildBody(theme),
    );
  }

  Widget _buildBody(ThemeData theme) {
    // Loading state
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    // Error state
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.error_outline, size: 48, color: theme.colorScheme.error),
              const SizedBox(height: 16),
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton.tonal(
                onPressed: _loadClaims,
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    // Overall empty state (no claims exist on account)
    if (_claims == null || _claims!.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.inbox_outlined, size: 64, color: theme.colorScheme.outline),
            const SizedBox(height: 16),
            Text(
              'No claims yet',
              style: theme.textTheme.titleMedium?.copyWith(
                color: theme.colorScheme.outline,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Tap the + button to submit a new claim.',
              style: TextStyle(color: theme.colorScheme.outline),
            ),
          ],
        ),
      );
    }

    final filtered = _filteredClaims;

    return Column(
      children: [
        // Search bar
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
          child: TextField(
            controller: _searchController,
            decoration: InputDecoration(
              hintText: 'Search claims (number, type, keyword)...',
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
                label: Text(status == 'All' ? 'All' : _formatStatus(status)),
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

        // Filtered claims list or empty search view
        Expanded(
          child: filtered.isEmpty
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.search_off, size: 54, color: theme.colorScheme.outline),
                        const SizedBox(height: 12),
                        Text(
                          'No matching claims',
                          style: theme.textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'No claims found matching your search or status filter.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: theme.colorScheme.outline),
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
                  onRefresh: _loadClaims,
                  child: ListView.builder(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    itemCount: filtered.length,
                    itemBuilder: (context, index) {
                      final claim = filtered[index];
                      final statusColor = _statusColor(claim.status);

                      return Card(
                        margin: const EdgeInsets.only(bottom: 12),
                        child: InkWell(
                          borderRadius: BorderRadius.circular(12),
                          onTap: () async {
                            await Navigator.pushNamed(
                              context,
                              '/claims/details',
                              arguments: claim.id,
                            );
                            _loadClaims(); // Refresh on return
                          },
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text(
                                      claim.claimNumber,
                                      style: theme.textTheme.titleSmall?.copyWith(
                                        fontWeight: FontWeight.w600,
                                        color: theme.colorScheme.primary,
                                      ),
                                    ),
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 10,
                                        vertical: 4,
                                      ),
                                      decoration: BoxDecoration(
                                        color: statusColor.withValues(alpha: 0.12),
                                        borderRadius: BorderRadius.circular(20),
                                      ),
                                      child: Text(
                                        _formatStatus(claim.status),
                                        style: TextStyle(
                                          fontSize: 11,
                                          fontWeight: FontWeight.w600,
                                          color: statusColor,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Text(
                                  claim.description,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(color: theme.colorScheme.onSurfaceVariant),
                                ),
                                const SizedBox(height: 12),
                                Row(
                                  children: [
                                    _infoChip(Icons.category, claim.claimType, theme),
                                    const SizedBox(width: 12),
                                    _infoChip(
                                      Icons.attach_money,
                                      NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2).format(claim.claimedAmount),
                                      theme,
                                    ),
                                    const SizedBox(width: 12),
                                    _infoChip(
                                      Icons.calendar_today,
                                      DateFormat.yMMMd().format(claim.incidentDate),
                                      theme,
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

  Widget _infoChip(IconData icon, String label, ThemeData theme) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: theme.colorScheme.outline),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(fontSize: 12, color: theme.colorScheme.outline),
        ),
      ],
    );
  }
}
