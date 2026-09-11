import 'package:flutter/material.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';

/// Payout status screen — Component D (Kinukshan).
/// Policyholder-facing: shows payout status, approved amount, processing status.
/// Does NOT expose confidential internal reviewer notes.
class PayoutStatusScreen extends StatefulWidget {
  final String? claimId;
  final String? payoutId;

  const PayoutStatusScreen({super.key, this.claimId, this.payoutId});

  @override
  State<PayoutStatusScreen> createState() => _PayoutStatusScreenState();
}

class _PayoutStatusScreenState extends State<PayoutStatusScreen> {
  final PayoutService _payoutService = PayoutService();
  Payout? _payout;
  bool _loading = true;
  String? _error;

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
      Payout? payout;
      if (widget.payoutId != null) {
        payout = await _payoutService.getPayoutById(widget.payoutId!);
      } else if (widget.claimId != null) {
        payout = await _payoutService.getPayoutByClaimId(widget.claimId!);
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
        return Colors.green;
      case 'Rejected':
        return Colors.red;
      case 'RevisionRequested':
        return Colors.amber;
      case 'Processing':
        return Colors.blue;
      case 'Paid':
        return Colors.teal;
      case 'Failed':
        return Colors.pink;
      default:
        return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Payout Status')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
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
                )
              : _payout == null
                  ? const Center(child: Text('No payout found for this claim.'))
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
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Row(
                                  children: [
                                    Icon(
                                      _payout!.isTerminal
                                          ? Icons.check_circle
                                          : _payout!.isProcessing
                                              ? Icons.sync
                                              : Icons.hourglass_top,
                                      color: _statusColor(_payout!.statusDisplay),
                                      size: 32,
                                    ),
                                    const SizedBox(width: 12),
                                    Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        const Text('Current Status',
                                            style: TextStyle(
                                                fontSize: 12, color: Colors.grey)),
                                        Text(
                                          _payout!.statusDisplay,
                                          style: TextStyle(
                                            fontSize: 20,
                                            fontWeight: FontWeight.bold,
                                            color: _statusColor(_payout!.statusDisplay),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),

                            // Payout amount
                            Card(
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Text('Payout Amount',
                                        style: TextStyle(
                                            fontSize: 14,
                                            fontWeight: FontWeight.w600,
                                            color: Colors.grey)),
                                    const SizedBox(height: 8),
                                    Text(
                                      _payout!.formattedPayout,
                                      style: const TextStyle(
                                        fontSize: 28,
                                        fontWeight: FontWeight.bold,
                                        color: Color(0xFF1976D2),
                                      ),
                                    ),
                                    const Divider(height: 24),
                                    _buildRow('Approved Claim',
                                        '\$${_payout!.approvedClaimAmount.toStringAsFixed(2)}'),
                                    _buildRow('Coverage Limit',
                                        '\$${_payout!.coverageLimit.toStringAsFixed(2)}'),
                                    _buildRow('Deductible',
                                        '- \$${_payout!.deductible.toStringAsFixed(2)}'),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),

                            // Payment reference if paid
                            if (_payout!.paymentReference != null)
                              Card(
                                child: ListTile(
                                  leading: const Icon(Icons.receipt, color: Colors.teal),
                                  title: const Text('Payment Reference'),
                                  subtitle: Text(_payout!.paymentReference!),
                                ),
                              ),

                            const SizedBox(height: 16),

                            // Timeline
                            Card(
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Text('Timeline',
                                        style: TextStyle(
                                            fontSize: 14,
                                            fontWeight: FontWeight.w600)),
                                    const SizedBox(height: 12),
                                    _buildTimelineItem('Created',
                                        _payout!.createdAt, true),
                                    if (_payout!.approvalTimestamp != null)
                                      _buildTimelineItem('Approved',
                                          _payout!.approvalTimestamp!, true),
                                    _buildTimelineItem('Last Updated',
                                        _payout!.updatedAt, true),
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
          Text(label, style: const TextStyle(color: Colors.grey)),
          Text(value,
              style: const TextStyle(
                  fontWeight: FontWeight.w600, fontFamily: 'monospace')),
        ],
      ),
    );
  }

  Widget _buildTimelineItem(String label, DateTime date, bool completed) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
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
                Text(label),
                Text(
                  '${date.day}/${date.month}/${date.year} ${date.hour}:${date.minute.toString().padLeft(2, '0')}',
                  style: const TextStyle(fontSize: 12, color: Colors.grey),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
