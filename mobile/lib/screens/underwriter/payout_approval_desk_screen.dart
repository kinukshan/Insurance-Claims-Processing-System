import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import 'payout_review_detail_screen.dart';

/// Payout approval desk for Underwriters and Admins.
/// Focuses on proposals requiring review and approval decisions.
class PayoutApprovalDeskScreen extends StatefulWidget {
  final bool isAdmin;

  const PayoutApprovalDeskScreen({super.key, this.isAdmin = false});

  @override
  State<PayoutApprovalDeskScreen> createState() => _PayoutApprovalDeskScreenState();
}

class _PayoutApprovalDeskScreenState extends State<PayoutApprovalDeskScreen> {
  final PayoutService _payoutService = PayoutService();

  List<Payout> _allPayouts = [];
  List<Payout> _filteredPayouts = [];
  bool _loading = true;
  String? _error;
  String _selectedFilter = 'PendingApproval';

  final List<String> _filters = [
    'PendingApproval',
    'Approved',
    'RevisionRequested',
    'Rejected',
    'All',
  ];

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd HH:mm');

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
        _applyFilter();
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _loading = false;
      });
    }
  }

  void _applyFilter() {
    setState(() {
      _filteredPayouts = _allPayouts.where((p) {
        if (_selectedFilter == 'All') return true;
        return p.statusDisplay.toLowerCase() == _selectedFilter.toLowerCase() ||
            p.status.toLowerCase() == _selectedFilter.toLowerCase();
      }).toList();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Payout Approval Desk'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadPayouts),
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

    return RefreshIndicator(
      onRefresh: _loadPayouts,
      child: Column(
        children: [
          // Filter Chips
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Row(
              children: _filters.map((filter) {
                final isSelected = _selectedFilter == filter;
                final count = filter == 'All'
                    ? _allPayouts.length
                    : _allPayouts.where((p) => p.statusDisplay.toLowerCase() == filter.toLowerCase()).length;

                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: FilterChip(
                    label: Text(
                      filter == 'PendingApproval'
                          ? 'Pending Review ($count)'
                          : '$filter ($count)',
                    ),
                    selected: isSelected,
                    selectedColor: filter == 'PendingApproval'
                        ? Colors.orange.shade100
                        : AppTheme.primaryTeal.withValues(alpha: 0.2),
                    checkmarkColor: AppTheme.primaryTeal,
                    onSelected: (_) {
                      setState(() => _selectedFilter = filter);
                      _applyFilter();
                    },
                  ),
                );
              }).toList(),
            ),
          ),

          // Payout List
          Expanded(
            child: _filteredPayouts.isEmpty
                ? EmptyStateView(
                    icon: Icons.assignment_turned_in_outlined,
                    title: _selectedFilter == 'PendingApproval'
                        ? 'Approval Desk Clear'
                        : 'No Payouts in This Category',
                    description: _selectedFilter == 'PendingApproval'
                        ? 'There are currently no payout proposals awaiting approval.'
                        : 'Select another filter tab to view decisions.',
                  )
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredPayouts.length,
                    itemBuilder: (context, index) {
                      final payout = _filteredPayouts[index];
                      return _buildDeskCard(payout);
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildDeskCard(Payout payout) {
    final isPending = payout.statusDisplay == 'PendingApproval';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: isPending ? 2.5 : 1,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          await Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => PayoutReviewDetailScreen(
                payoutId: payout.id,
                isAdmin: widget.isAdmin,
              ),
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
                    payout.claimNumber ?? 'Claim: ${payout.claimId.substring(0, 8)}...',
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  StatusBadge(status: payout.statusDisplay),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                'Calculated: ${_dateFormat.format(payout.createdAt)}',
                style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
              ),
              const Divider(height: 18),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Net Proposed', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                      Text(
                        _currencyFormat.format(payout.finalPayout),
                        style: TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.w800,
                          color: isPending ? Colors.orange.shade800 : AppTheme.deepNavy,
                        ),
                      ),
                    ],
                  ),
                  Row(
                    children: [
                      Text(
                        isPending ? 'Review & Decide' : 'View Audit',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: isPending ? Colors.orange.shade800 : AppTheme.primaryTeal,
                        ),
                      ),
                      Icon(
                        Icons.chevron_right,
                        size: 18,
                        color: isPending ? Colors.orange.shade800 : AppTheme.primaryTeal,
                      ),
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
