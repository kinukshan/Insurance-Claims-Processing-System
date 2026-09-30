import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../services/claim_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import 'claim_investigation_screen.dart';

/// Claims queue for Claims Adjusters.
/// Displays portfolio-wide claims with status filtering and search.
class AdjusterClaimsQueueScreen extends StatefulWidget {
  const AdjusterClaimsQueueScreen({super.key});

  @override
  State<AdjusterClaimsQueueScreen> createState() => _AdjusterClaimsQueueScreenState();
}

class _AdjusterClaimsQueueScreenState extends State<AdjusterClaimsQueueScreen> {
  final ClaimService _claimService = ClaimService();
  final TextEditingController _searchController = TextEditingController();

  List<Claim> _allClaims = [];
  List<Claim> _filteredClaims = [];
  bool _loading = true;
  String? _error;
  String _selectedStatus = 'All';

  final List<String> _statuses = [
    'All',
    'Submitted',
    'UnderReview',
    'Approved',
    'Rejected',
    'Withdrawn',
  ];

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  @override
  void initState() {
    super.initState();
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
      final claims = await _claimService.getAllClaims();
      if (!mounted) return;
      setState(() {
        _allClaims = claims;
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
    final query = _searchController.text.trim().toLowerCase();
    setState(() {
      _filteredClaims = _allClaims.where((c) {
        final matchesStatus = _selectedStatus == 'All' ||
            c.status.toLowerCase() == _selectedStatus.toLowerCase();
        final matchesSearch = query.isEmpty ||
            c.claimNumber.toLowerCase().contains(query) ||
            c.description.toLowerCase().contains(query) ||
            c.claimType.toLowerCase().contains(query) ||
            (c.policyHolderId?.toLowerCase().contains(query) ?? false);
        return matchesStatus && matchesSearch;
      }).toList();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Claims Queue'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadClaims,
          ),
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
      return ErrorRetryView(message: _error!, onRetry: _loadClaims);
    }

    final pendingCount = _allClaims.where((c) => c.status == 'Submitted' || c.status == 'UnderReview').length;
    final approvedCount = _allClaims.where((c) => c.status == 'Approved').length;

    return RefreshIndicator(
      onRefresh: _loadClaims,
      child: Column(
        children: [
          // Metrics summary bar
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            color: Colors.grey.shade50,
            child: Row(
              children: [
                _miniMetric('Total Claims', '${_allClaims.length}', AppTheme.deepNavy),
                const SizedBox(width: 8),
                _miniMetric('Needs Review', '$pendingCount', Colors.orange.shade800),
                const SizedBox(width: 8),
                _miniMetric('Approved', '$approvedCount', Colors.green.shade700),
              ],
            ),
          ),

          // Search Field
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'Search by claim #, type, policyholder...',
                prefixIcon: const Icon(Icons.search, size: 20),
                suffixIcon: _searchController.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear, size: 18),
                        onPressed: () {
                          _searchController.clear();
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

          // Filter Chips
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

          // Claims List
          Expanded(
            child: _filteredClaims.isEmpty
                ? const EmptyStateView(
                    icon: Icons.assignment_outlined,
                    title: 'No Claims Found',
                    description: 'No claims match your selected filters.',
                  )
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredClaims.length,
                    itemBuilder: (context, index) {
                      final claim = _filteredClaims[index];
                      return _buildClaimCard(claim);
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
            Text(
              value,
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color),
            ),
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

  Widget _buildClaimCard(Claim claim) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 1.5,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          await Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => ClaimInvestigationScreen(claimId: claim.id),
            ),
          );
          _loadClaims();
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
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  StatusBadge(status: claim.status),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                'Type: ${claim.claimType} • Incident: ${_dateFormat.format(claim.incidentDate)}',
                style: const TextStyle(fontSize: 12.5, color: AppTheme.textSecondary),
              ),
              if (claim.description.isNotEmpty) ...[
                const SizedBox(height: 6),
                Text(
                  claim.description,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 13, color: Colors.black87),
                ),
              ],
              const Divider(height: 20),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    _currencyFormat.format(claim.claimedAmount),
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w800,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  const Row(
                    children: [
                      Text(
                        'Investigate',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: AppTheme.primaryTeal,
                        ),
                      ),
                      Icon(Icons.chevron_right, size: 18, color: AppTheme.primaryTeal),
                    ],
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
