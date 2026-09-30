import 'package:flutter/material.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';

/// Payout status screen — Component D (Kinukshan).
/// Policyholder-facing: shows payout status, approved amount, processing status.
/// Does NOT expose confidential internal reviewer notes, risk scores, or fraud indicators.
/// Never presents an approved payout as already paid.
class PayoutStatusScreen extends StatefulWidget {
  final String? claimId;
  final String? payoutId;
  final PayoutService? payoutService;

  const PayoutStatusScreen({
    super.key,
    this.claimId,
    this.payoutId,
    this.payoutService,
  });

  @override
  State<PayoutStatusScreen> createState() => _PayoutStatusScreenState();
}

class _PayoutStatusScreenState extends State<PayoutStatusScreen> {
  late final PayoutService _payoutService;
  Payout? _payout;
  bool _loading = true;
  String? _error;
  bool _initialized = false;
  String? _effectiveClaimId;
  String? _effectivePayoutId;

  @override
  void initState() {
    super.initState();
    _payoutService = widget.payoutService ?? PayoutService();
    _effectiveClaimId = widget.claimId;
    _effectivePayoutId = widget.payoutId;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_initialized) {
      _initialized = true;
      final routeArg = ModalRoute.of(context)?.settings.arguments;
      if (routeArg is String) {
        if (_effectiveClaimId == null && _effectivePayoutId == null) {
          _effectiveClaimId = routeArg;
        }
      } else if (routeArg is Map) {
        _effectiveClaimId ??= routeArg['claimId'] as String?;
        _effectivePayoutId ??= routeArg['payoutId'] as String?;
      }
      _loadPayout();
    }
  }

  Future<void> _loadPayout() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      Payout? payout;
      if (_effectivePayoutId != null) {
        payout = await _payoutService.getPayoutById(_effectivePayoutId!);
      } else if (_effectiveClaimId != null) {
        payout = await _payoutService.getPayoutByClaimId(_effectiveClaimId!);
      }

      setState(() {
        _payout = payout;
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
      case 'Draft':
        return Colors.blueGrey;
      case 'PendingApproval':
        return Colors.orange;
      case 'Approved':
        return Colors.indigo;
      case 'Rejected':
        return Colors.red;
      case 'RevisionRequested':
        return Colors.amber.shade800;
      case 'Processing':
        return Colors.blue;
      case 'Paid':
        return Colors.green;
      case 'Failed':
        return Colors.pink;
      default:
        return Colors.grey;
    }
  }

  String _statusDisplayTitle(String status) {
    switch (status) {
      case 'Draft':
        return 'Draft Proposal';
      case 'PendingApproval':
        return 'Pending Approval';
      case 'Approved':
        return 'Approved (Pending Disbursement)';
      case 'Processing':
        return 'Processing Disbursement';
      case 'Paid':
        return 'Disbursed / Paid';
      case 'Rejected':
        return 'Proposal Rejected';
      case 'RevisionRequested':
        return 'Revision Requested';
      case 'Failed':
        return 'Disbursement Failed';
      default:
        return status;
    }
  }

  String _statusSubtitle(String status) {
    switch (status) {
      case 'Draft':
        return 'Payout proposal is being drafted.';
      case 'PendingApproval':
        return 'Awaiting underwriter review and authorization.';
      case 'Approved':
        return 'Payout approved by underwriting. Funds have NOT been transferred yet; awaiting electronic disbursement.';
      case 'Processing':
        return 'Electronic disbursement is currently in flight with the payment provider.';
      case 'Paid':
        return 'Funds have been successfully disbursed to your account.';
      case 'Rejected':
        return 'This payout proposal was reviewed and rejected.';
      case 'RevisionRequested':
        return 'The underwriter requested adjustments to this calculation.';
      case 'Failed':
        return 'Payment execution attempt failed. Staff is reviewing the transaction.';
      default:
        return 'Current status: $status';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Payout Details'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh Payout',
            onPressed: _loadPayout,
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.error_outline, size: 48, color: Colors.red),
                        const SizedBox(height: 16),
                        Text('Error: $_error',
                            textAlign: TextAlign.center,
                            style: const TextStyle(color: Colors.red)),
                        const SizedBox(height: 16),
                        ElevatedButton(
                          onPressed: _loadPayout,
                          child: const Text('Retry'),
                        ),
                      ],
                    ),
                  ),
                )
              : _payout == null
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.payments_outlined, size: 48, color: Colors.grey),
                            const SizedBox(height: 16),
                            const Text(
                              'No payout has been prepared for this claim yet.',
                              textAlign: TextAlign.center,
                              style: TextStyle(fontSize: 16, color: Colors.grey),
                            ),
                            const SizedBox(height: 16),
                            FilledButton.tonal(
                              onPressed: _loadPayout,
                              child: const Text('Check Again'),
                            ),
                          ],
                        ),
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _loadPayout,
                      child: SingleChildScrollView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            // Status card
                            Card(
                              elevation: 1,
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Icon(
                                          _payout!.statusDisplay == 'Paid'
                                              ? Icons.check_circle
                                              : _payout!.isProcessing
                                                  ? Icons.sync
                                                  : _payout!.statusDisplay == 'Approved'
                                                      ? Icons.thumb_up
                                                      : Icons.hourglass_top,
                                          color: _statusColor(_payout!.statusDisplay),
                                          size: 30,
                                        ),
                                        const SizedBox(width: 12),
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment: CrossAxisAlignment.start,
                                            children: [
                                              const Text('Payout Status',
                                                  style: TextStyle(
                                                      fontSize: 12, color: Colors.grey)),
                                              Text(
                                                _statusDisplayTitle(_payout!.statusDisplay),
                                                style: TextStyle(
                                                  fontSize: 17,
                                                  fontWeight: FontWeight.bold,
                                                  color: _statusColor(_payout!.statusDisplay),
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 10),
                                    Container(
                                      padding: const EdgeInsets.all(10),
                                      decoration: BoxDecoration(
                                        color: _statusColor(_payout!.statusDisplay).withValues(alpha: 0.08),
                                        borderRadius: BorderRadius.circular(8),
                                      ),
                                      child: Text(
                                        _statusSubtitle(_payout!.statusDisplay),
                                        style: TextStyle(
                                          fontSize: 12,
                                          color: _statusColor(_payout!.statusDisplay),
                                          fontWeight: FontWeight.w500,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),

                            // Payout amount card
                            Card(
                              elevation: 1,
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Text('Authorized Payout Amount',
                                        style: TextStyle(
                                            fontSize: 14,
                                            fontWeight: FontWeight.w600,
                                            color: Colors.grey)),
                                    const SizedBox(height: 8),
                                    Text(
                                      _payout!.formattedPayout,
                                      style: TextStyle(
                                        fontSize: 28,
                                        fontWeight: FontWeight.bold,
                                        color: _payout!.statusDisplay == 'Paid'
                                            ? Colors.green.shade700
                                            : const Color(0xFF1976D2),
                                      ),
                                    ),
                                    const Divider(height: 24),
                                    _buildRow('Approved Claim Amount',
                                        'LKR ${_payout!.approvedClaimAmount.toStringAsFixed(2)}'),
                                    _buildRow('Coverage Limit',
                                        'LKR ${_payout!.coverageLimit.toStringAsFixed(2)}'),
                                    _buildRow('Eligible Amount',
                                        'LKR ${_payout!.effectiveEligibleAmount.toStringAsFixed(2)}'),
                                    _buildRow(
                                        _payout!.deductiblePercentage != null
                                            ? 'Policy Deductible (${_payout!.deductiblePercentage!.toStringAsFixed(_payout!.deductiblePercentage! % 1 == 0 ? 0 : 2)}%)'
                                            : 'Policy Deductible',
                                        '- LKR ${_payout!.deductible.toStringAsFixed(2)}'),
                                    _buildRow('Final Calculated Payout',
                                        'LKR ${_payout!.finalPayout.toStringAsFixed(2)}'),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),

                            // Payment reference if paid
                            if (_payout!.paymentReference != null &&
                                _payout!.paymentReference!.isNotEmpty)
                              Card(
                                elevation: 1,
                                child: ListTile(
                                  leading: const Icon(Icons.receipt_long, color: Colors.green),
                                  title: const Text('Payment Reference Number'),
                                  subtitle: Text(
                                    _payout!.paymentReference!,
                                    style: const TextStyle(
                                      fontFamily: 'monospace',
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                              ),

                            const SizedBox(height: 16),

                            // Timeline
                            Card(
                              elevation: 1,
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Text('Confirmed Milestones',
                                        style: TextStyle(
                                            fontSize: 14,
                                            fontWeight: FontWeight.w600)),
                                    const SizedBox(height: 12),
                                    _buildTimelineItem('Proposal Prepared',
                                        _payout!.createdAt, true),
                                    if (_payout!.approvalTimestamp != null)
                                      _buildTimelineItem('Underwriter Approved',
                                          _payout!.approvalTimestamp!, true),
                                    if (_payout!.statusDisplay == 'Paid')
                                      _buildTimelineItem('Disbursement Completed',
                                          _payout!.updatedAt, true)
                                    else
                                      _buildTimelineItem('Disbursement',
                                          _payout!.updatedAt, false),
                                  ],
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
    );
  }

  Widget _buildRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.grey, fontSize: 13)),
          Text(value,
              style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 13,
                  fontFamily: 'monospace')),
        ],
      ),
    );
  }

  Widget _buildTimelineItem(String label, DateTime date, bool completed) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Icon(
            completed ? Icons.check_circle : Icons.radio_button_unchecked,
            size: 16,
            color: completed ? Colors.green : Colors.grey,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(label, style: const TextStyle(fontSize: 13)),
                Text(
                  completed
                      ? '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}'
                      : 'Pending',
                  style: TextStyle(
                    fontSize: 12,
                    color: completed ? Colors.grey.shade700 : Colors.grey,
                    fontStyle: completed ? FontStyle.normal : FontStyle.italic,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
