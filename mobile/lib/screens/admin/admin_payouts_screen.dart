import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/payout.dart';
import '../../models/ai_workflow_models.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import '../underwriter/payout_review_detail_screen.dart';

/// Financial settlements and payment execution screen for Administrators.
/// Authorized to execute approved payouts via payment gateway.
class AdminPayoutsScreen extends StatefulWidget {
  const AdminPayoutsScreen({super.key});

  @override
  State<AdminPayoutsScreen> createState() => _AdminPayoutsScreenState();
}

class _AdminPayoutsScreenState extends State<AdminPayoutsScreen> {
  final PayoutService _payoutService = PayoutService();

  List<Payout> _allPayouts = [];
  List<Payout> _filteredPayouts = [];
  bool _loading = true;
  String? _error;
  String _selectedFilter = 'Approved'; // Default to Approved so Admin can execute immediately

  final List<String> _filters = [
    'Approved',
    'PendingApproval',
    'Paid',
    'Processing',
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

  Future<void> _executePayout(Payout payout) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Execute Payout Disbursement'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Disburse ${_currencyFormat.format(payout.finalPayout)} for Claim ${payout.claimNumber ?? payout.claimId.substring(0, 8)}?'),
            const SizedBox(height: 8),
            const Text(
              'Payment will be processed through the configured payment gateway (Mock Provider). This action will finalize the financial settlement and notify the policyholder.',
              style: TextStyle(fontSize: 12, color: AppTheme.textSecondary),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.green.shade700,
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Confirm & Disburse'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    try {
      final result = await _payoutService.executePayout(
        payout.id,
        fallbackAmount: payout.finalPayout,
      );
      if (!mounted) return;

      if (result.success) {
        _showExecutionReceipt(result, payout);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Payment failed: ${result.errorMessage ?? "Unknown error"}'),
            backgroundColor: Colors.red,
          ),
        );
      }
      _loadPayouts();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Payment execution error: $e'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }

  void _showExecutionReceipt(PaymentExecutionResult result, Payout payout) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.check_circle, color: Colors.green, size: 28),
            SizedBox(width: 10),
            Text('Payment Executed'),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Disbursement of ${_currencyFormat.format(result.amount)} successfully completed.',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            const Divider(height: 20),
            _receiptRow('Transaction ID', result.transactionId ?? 'N/A'),
            _receiptRow('Payment Status', result.status),
            _receiptRow('Provider', result.provider ?? 'Mock Payment Gateway'),
            _receiptRow('Claim', payout.claimNumber ?? payout.claimId.substring(0, 8)),
          ],
        ),
        actions: [
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  Widget _receiptRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary)),
          Text(value, style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Settlements & Disbursements'),
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

    return Column(
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            children: _filters.map((filter) {
              final isSelected = _selectedFilter == filter;
              return Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ChoiceChip(
                  label: Text(filter),
                  selected: isSelected,
                  selectedColor: AppTheme.primaryTeal.withValues(alpha: 0.15),
                  onSelected: (selected) {
                    if (selected) {
                      setState(() {
                        _selectedFilter = filter;
                        _applyFilter();
                      });
                    }
                  },
                ),
              );
            }).toList(),
          ),
        ),
        Expanded(
          child: _filteredPayouts.isEmpty
              ? EmptyStateView(
                  icon: Icons.payments_outlined,
                  title: _selectedFilter == 'Approved'
                      ? 'No Payouts Awaiting Execution'
                      : 'No Payouts in This Category',
                  description: _selectedFilter == 'Approved'
                      ? 'Approved payouts will appear here ready for one-click payment execution.'
                      : 'Select another filter tab to view records.',
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
    );
  }

  Widget _buildPayoutCard(Payout payout) {
    final isApproved = payout.statusDisplay == 'Approved';
    final isPaid = payout.statusDisplay == 'Paid';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: isApproved ? 3 : 1.5,
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
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: AppTheme.deepNavy),
                ),
                StatusBadge(status: payout.statusDisplay),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              'Date: ${_dateFormat.format(payout.createdAt)}',
              style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
            ),
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Claimed', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                    Text(_currencyFormat.format(payout.approvedClaimAmount), style: const TextStyle(fontSize: 13)),
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
                    Text(_currencyFormat.format(payout.deductible), style: const TextStyle(fontSize: 13, color: Colors.red)),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text('Net Payout', style: TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                    Text(
                      _currencyFormat.format(payout.finalPayout),
                      style: TextStyle(
                        fontSize: 17,
                        fontWeight: FontWeight.w800,
                        color: isPaid ? Colors.green.shade700 : AppTheme.deepNavy,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            if (payout.paymentReference != null) ...[
              const SizedBox(height: 8),
              Text(
                'Payment Ref: ${payout.paymentReference}',
                style: const TextStyle(fontSize: 11.5, color: Colors.green, fontWeight: FontWeight.w600),
              ),
            ],
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () async {
                    await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) => PayoutReviewDetailScreen(
                          payoutId: payout.id,
                          isAdmin: true,
                        ),
                      ),
                    );
                    _loadPayouts();
                  },
                  child: const Text('View Audit Details'),
                ),
                if (isApproved) ...[
                  const SizedBox(width: 8),
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.green.shade700,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    ),
                    onPressed: () => _executePayout(payout),
                    icon: const Icon(Icons.flash_on, size: 18),
                    label: const Text('Execute Payment', style: TextStyle(fontWeight: FontWeight.w700)),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}
