import 'package:flutter/material.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';

/// Payout history screen — Component D (Kinukshan).
/// Policyholder-facing list of payout history entries.
class PayoutHistoryScreen extends StatefulWidget {
  const PayoutHistoryScreen({super.key});

  @override
  State<PayoutHistoryScreen> createState() => _PayoutHistoryScreenState();
}

class _PayoutHistoryScreenState extends State<PayoutHistoryScreen> {
  final PayoutService _payoutService = PayoutService();
  List<Payout> _payouts = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadHistory();
  }

  Future<void> _loadHistory() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final payouts = await _payoutService.getPayoutHistory();
      setState(() {
        _payouts = payouts;
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
      case 'Paid':
        return Colors.teal;
      case 'Approved':
        return Colors.green;
      case 'Processing':
        return Colors.blue;
      case 'PendingApproval':
        return Colors.orange;
      case 'Rejected':
        return Colors.red;
      case 'Failed':
        return Colors.pink;
      default:
        return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Payout History')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.error_outline, size: 48, color: Colors.red),
                      const SizedBox(height: 16),
                      Text('Error: $_error', textAlign: TextAlign.center),
                      const SizedBox(height: 16),
                      ElevatedButton(
                        onPressed: _loadHistory,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                )
              : _payouts.isEmpty
                  ? const Center(child: Text('No payout history found.'))
                  : RefreshIndicator(
                      onRefresh: _loadHistory,
                      child: ListView.builder(
                        padding: const EdgeInsets.all(16),
                        itemCount: _payouts.length,
                        itemBuilder: (context, index) {
                          final payout = _payouts[index];
                          return Card(
                            margin: const EdgeInsets.only(bottom: 12),
                            child: ListTile(
                              leading: CircleAvatar(
                                backgroundColor:
                                    _statusColor(payout.statusDisplay)
                                        .withValues(alpha: 0.15),
                                child: Icon(
                                  payout.statusDisplay == 'Paid'
                                      ? Icons.check
                                      : payout.isProcessing
                                          ? Icons.sync
                                          : Icons.hourglass_top,
                                  color: _statusColor(payout.statusDisplay),
                                ),
                              ),
                              title: Text(
                                payout.formattedPayout,
                                style: const TextStyle(fontWeight: FontWeight.bold),
                              ),
                              subtitle: Text(
                                '${payout.statusDisplay} · ${payout.createdAt.day}/${payout.createdAt.month}/${payout.createdAt.year}',
                                style: const TextStyle(fontSize: 12),
                              ),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () {
                                Navigator.push(
                                  context,
                                  MaterialPageRoute(
                                    builder: (_) => PayoutDetailScreen(
                                        payoutId: payout.id),
                                  ),
                                );
                              },
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}

/// Payout detail screen — policyholder-facing.
/// Shows calculation breakdown and status without confidential data.
class PayoutDetailScreen extends StatefulWidget {
  final String payoutId;

  const PayoutDetailScreen({super.key, required this.payoutId});

  @override
  State<PayoutDetailScreen> createState() => _PayoutDetailScreenState();
}

class _PayoutDetailScreenState extends State<PayoutDetailScreen> {
  final PayoutService _payoutService = PayoutService();
  Payout? _payout;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadDetail();
  }

  Future<void> _loadDetail() async {
    setState(() { _loading = true; _error = null; });
    try {
      final payout = await _payoutService.getPayoutById(widget.payoutId);
      setState(() { _payout = payout; _loading = false; });
    } catch (e) {
      setState(() { _error = e.toString(); _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Payout Details')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text('Error: $_error'))
              : _payout == null
                  ? const Center(child: Text('Payout not found.'))
                  : SingleChildScrollView(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          // Amount header
                          Center(
                            child: Column(
                              children: [
                                Text(
                                  _payout!.formattedPayout,
                                  style: const TextStyle(
                                    fontSize: 32,
                                    fontWeight: FontWeight.bold,
                                    color: Color(0xFF1976D2),
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Chip(
                                  label: Text(_payout!.statusDisplay),
                                  backgroundColor: Colors.grey.shade200,
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 24),

                          // Breakdown card
                          Card(
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text('Calculation Breakdown',
                                      style: TextStyle(fontWeight: FontWeight.w600)),
                                  const Divider(),
                                  _row('Claim Amount',
                                      '\$${_payout!.approvedClaimAmount.toStringAsFixed(2)}'),
                                  _row('Coverage Limit',
                                      '\$${_payout!.coverageLimit.toStringAsFixed(2)}'),
                                  _row('Deductible',
                                      '- \$${_payout!.deductible.toStringAsFixed(2)}'),
                                  const Divider(),
                                  _row('Final Payout', _payout!.formattedPayout,
                                      bold: true),
                                ],
                              ),
                            ),
                          ),

                          // Payment reference
                          if (_payout!.paymentReference != null) ...[
                            const SizedBox(height: 16),
                            Card(
                              child: ListTile(
                                leading: const Icon(Icons.receipt, color: Colors.teal),
                                title: const Text('Payment Reference'),
                                subtitle: Text(_payout!.paymentReference!),
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
    );
  }

  Widget _row(String label, String value, {bool bold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.grey)),
          Text(value,
              style: TextStyle(
                fontWeight: bold ? FontWeight.bold : FontWeight.w500,
                fontFamily: 'monospace',
              )),
        ],
      ),
    );
  }
}
