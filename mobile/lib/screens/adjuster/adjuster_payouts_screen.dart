import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import 'claim_investigation_screen.dart';

/// Payouts tracking screen for Claims Adjusters.
/// Shows prepared payout proposals and current review statuses.
class AdjusterPayoutsScreen extends StatefulWidget {
  const AdjusterPayoutsScreen({super.key});

  @override
  State<AdjusterPayoutsScreen> createState() => _AdjusterPayoutsScreenState();
}

class _AdjusterPayoutsScreenState extends State<AdjusterPayoutsScreen> {
  final PayoutService _payoutService = PayoutService();

  List<Payout> _allPayouts = [];
  List<Payout> _filteredPayouts = [];
  bool _loading = true;
  String? _error;
  String _selectedStatus = 'All';

  final List<String> _statuses = [
    'All',
    'PendingApproval',
    'Approved',
    'Processing',
    'Paid',
    'Rejected',
    'RevisionRequested',
  ];

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  @override
  void initState() {
    super.initState();
    _loadPayouts();
  }

  Future<void> _loadPayouts() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final payouts = await _payoutService.getAllPayouts();
      if (!mounted) return;
      setState(() {
        _allPayouts = payouts;
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
    setState(() {
      _filteredPayouts = _allPayouts.where((p) {
        if (_selectedStatus == 'All') return true;
        return p.statusDisplay.toLowerCase() == _selectedStatus.toLowerCase() ||
            p.status.toLowerCase() == _selectedStatus.toLowerCase();
      }).toList();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Payout Proposals'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadPayouts,
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
      return ErrorRetryView(message: _error!, onRetry: _loadPayouts);
    }

    final pendingCount = _allPayouts.where((p) => p.statusDisplay == 'PendingApproval').length;
    final paidCount = _allPayouts.where((p) => p.statusDisplay == 'Paid').length;

    return RefreshIndicator(
      onRefresh: _loadPayouts,
      child: Column(
        children: [
          // Metrics bar
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            color: Colors.grey.shade50,
            child: Row(
              children: [
                _miniMetric('Proposals', '${_allPayouts.length}', AppTheme.deepNavy),
                const SizedBox(width: 8),
                _miniMetric('Pending Review', '$pendingCount', Colors.orange.shade800),
                const SizedBox(width: 8),
                _miniMetric('Settled / Paid', '$paidCount', Colors.green.shade700),
              ],
            ),
          ),

          // Status Filter Chips
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
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

          // Payouts List
          Expanded(
            child: _filteredPayouts.isEmpty
                ? const EmptyStateView(
                    icon: Icons.payments_outlined,
                    title: 'No Payouts Found',
                    description: 'No payout proposals match the selected filter.',
                  )
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredPayouts.length,
                    itemBuilder: (context, index) {
                      final payout = _filteredPayouts[index];
                      return _buildPayoutCard(payout);
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

  Widget _buildPayoutCard(Payout payout) {
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
              builder: (_) => ClaimInvestigationScreen(claimId: payout.claimId),
            ),
          );
          _loadPayouts();
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
                    payout.claimNumber ?? 'Payout #${payout.id.substring(0, 8)}',
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: AppTheme.deepNavy),
                  ),
                  StatusBadge(status: payout.statusDisplay),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                'Calculated: ${_dateFormat.format(payout.createdAt)} • Claim ID: ${payout.claimId.substring(0, 8)}...',
                style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
              ),
              const Divider(height: 18),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Claimed', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                      Text(
                        _currencyFormat.format(payout.approvedClaimAmount),
                        style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        payout.deductiblePercentage != null
                            ? 'Ded (${payout.deductiblePercentage!.toStringAsFixed(payout.deductiblePercentage! % 1 == 0 ? 0 : 2)}%)'
                            : 'Deductible',
                        style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary),
                      ),
                      Text(
                        _currencyFormat.format(payout.deductible),
                        style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Colors.red),
                      ),
                    ],
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      const Text('Net Payout', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                      Text(
                        _currencyFormat.format(payout.finalPayout),
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w800,
                          color: Colors.green.shade700,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
              if (payout.approvedBy != null) ...[
                const SizedBox(height: 10),
                Text(
                  'Authorized By: ${payout.approvedBy}',
                  style: const TextStyle(fontSize: 11.5, color: Colors.blue, fontWeight: FontWeight.w500),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
