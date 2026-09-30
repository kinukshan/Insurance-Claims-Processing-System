import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import '../../widgets/ai_workflow_widgets.dart';

/// Payout review and approval screen for Underwriters and Admins.
/// Allows inspecting AI Safety Agent validation results and submitting decision:
/// - Approve
/// - Reject
/// - Request Revision
class PayoutReviewDetailScreen extends StatefulWidget {
  final String payoutId;
  final bool isAdmin;

  const PayoutReviewDetailScreen({
    super.key,
    required this.payoutId,
    this.isAdmin = false,
  });

  @override
  State<PayoutReviewDetailScreen> createState() => _PayoutReviewDetailScreenState();
}

class _PayoutReviewDetailScreenState extends State<PayoutReviewDetailScreen> {
  final PayoutService _payoutService = PayoutService();

  Payout? _payout;
  bool _loading = true;
  String? _error;
  bool _actionInProgress = false;

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd HH:mm');

  @override
  void initState() {
    super.initState();
    _loadPayout();
  }

  Future<void> _loadPayout() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final payout = await _payoutService.getPayoutById(widget.payoutId);
      if (!mounted) return;
      setState(() {
        _payout = payout;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _loading = false;
      });
    }
  }

  Future<void> _submitDecision(String action) async {
    final commentsCtrl = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('$action Payout Proposal'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Are you sure you want to $action this payout proposal?'),
            const SizedBox(height: 12),
            TextField(
              controller: commentsCtrl,
              maxLines: 3,
              decoration: const InputDecoration(
                labelText: 'Reviewer Comments / Justification',
                hintText: 'Enter reason or notes for audit record...',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: action == 'Approve'
                  ? Colors.green
                  : (action == 'Reject' ? Colors.red : Colors.orange),
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(action),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _actionInProgress = true);
    try {
      final comments = commentsCtrl.text.trim();
      Payout updated;
      if (action == 'Approve') {
        updated = await _payoutService.approvePayout(widget.payoutId, comments: comments);
      } else if (action == 'Reject') {
        updated = await _payoutService.rejectPayout(widget.payoutId, comments: comments);
      } else {
        updated = await _payoutService.requestRevision(widget.payoutId, comments: comments);
      }

      if (!mounted) return;
      setState(() {
        _payout = updated;
        _actionInProgress = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Payout successfully ${action.toLowerCase()}d.'),
          backgroundColor: Colors.green,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _actionInProgress = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Action failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_payout != null ? 'Payout #${_payout!.id.substring(0, 8)}' : 'Payout Review'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadPayout),
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
      return ErrorRetryView(message: _error!, onRetry: _loadPayout);
    }
    if (_payout == null) {
      return const Center(child: Text('Payout not found.'));
    }

    final payout = _payout!;
    final isPending = payout.statusDisplay == 'PendingApproval';

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Banner about Underwriter vs Admin permissions
          if (!widget.isAdmin)
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.blue.shade50,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: Colors.blue.shade200),
              ),
              child: const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.shield_outlined, color: Colors.blue, size: 20),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Underwriter Approval Authority: You can Approve, Reject, or Request Revision. Payment execution requires Administrator authority.',
                      style: TextStyle(fontSize: 12, color: Colors.blue, height: 1.3),
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 14),

          // Payout Financial Breakdown Card
          Card(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            elevation: 2,
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
                          fontSize: 17,
                          fontWeight: FontWeight.w800,
                          color: AppTheme.deepNavy,
                        ),
                      ),
                      StatusBadge(status: payout.statusDisplay),
                    ],
                  ),
                  const Divider(height: 20),
                  _calcRow('Approved Claim Amount', _currencyFormat.format(payout.approvedClaimAmount)),
                  const SizedBox(height: 8),
                  _calcRow('Coverage Limit', _currencyFormat.format(payout.coverageLimit)),
                  const SizedBox(height: 8),
                  _calcRow('Eligible Amount', _currencyFormat.format(payout.effectiveEligibleAmount)),
                  const SizedBox(height: 8),
                  _calcRow(
                    payout.deductiblePercentage != null
                        ? 'Applicable Deductible (${payout.deductiblePercentage!.toStringAsFixed(payout.deductiblePercentage! % 1 == 0 ? 0 : 2)}%)'
                        : 'Applicable Deductible',
                    '-${_currencyFormat.format(payout.deductible)}',
                    isDeductible: true,
                  ),
                  const Divider(height: 20),
                  _calcRow('Final Net Payout', _currencyFormat.format(payout.finalPayout), isTotal: true),
                  if (payout.explanation != null && payout.explanation!.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.grey.shade50,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        payout.explanation!,
                        style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // AI Safety & Validation Agent Card
          if (payout.validationResult != null) ...[
            PayoutValidationCard(validation: payout.validationResult!),
            const SizedBox(height: 16),
          ],

          // Approval Decision History
          if (payout.approvals.isNotEmpty) ...[
            Card(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              elevation: 1.5,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Audit & Review History',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                    const SizedBox(height: 10),
                    ...payout.approvals.map(
                      (appr) => Container(
                        margin: const EdgeInsets.only(bottom: 8),
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: Colors.grey.shade50,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                StatusBadge(status: appr.decisionDisplay),
                                Text(
                                  _dateFormat.format(appr.decisionTimestamp),
                                  style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary),
                                ),
                              ],
                            ),
                            if (appr.reviewerName != null) ...[
                              const SizedBox(height: 4),
                              Text(
                                'Reviewer: ${appr.reviewerName}',
                                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                              ),
                            ],
                            if (appr.comments != null && appr.comments!.isNotEmpty) ...[
                              const SizedBox(height: 4),
                              Text(
                                appr.comments!,
                                style: const TextStyle(fontSize: 12, color: Colors.black87),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),
          ],

          // Decision Action Buttons (Visible when PendingApproval)
          if (isPending) ...[
            const Text(
              'Underwriting Determination',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.green.shade700,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    onPressed: _actionInProgress ? null : () => _submitDecision('Approve'),
                    icon: const Icon(Icons.check_circle_outline),
                    label: const Text('Approve', style: TextStyle(fontWeight: FontWeight.w700)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.orange.shade800,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    onPressed: _actionInProgress ? null : () => _submitDecision('Request Revision'),
                    icon: const Icon(Icons.history),
                    label: const Text('Revise', style: TextStyle(fontWeight: FontWeight.w700)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.red.shade700,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    onPressed: _actionInProgress ? null : () => _submitDecision('Reject'),
                    icon: const Icon(Icons.cancel_outlined),
                    label: const Text('Reject', style: TextStyle(fontWeight: FontWeight.w700)),
                  ),
                ),
              ],
            ),
          ] else ...[
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: Colors.grey.shade100,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Center(
                child: Text(
                  'Determination recorded: ${payout.statusDisplay}',
                  style: const TextStyle(fontWeight: FontWeight.w600, color: AppTheme.deepNavy),
                ),
              ),
            ),
          ],
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  Widget _calcRow(String label, String value, {bool isDeductible = false, bool isTotal = false}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: isTotal ? 15 : 13,
            fontWeight: isTotal ? FontWeight.w700 : FontWeight.w500,
            color: isTotal ? AppTheme.deepNavy : AppTheme.textSecondary,
          ),
        ),
        Text(
          value,
          style: TextStyle(
            fontSize: isTotal ? 18 : 14,
            fontWeight: isTotal ? FontWeight.w800 : FontWeight.w600,
            color: isTotal
                ? Colors.green.shade800
                : (isDeductible ? Colors.red : AppTheme.deepNavy),
          ),
        ),
      ],
    );
  }
}
