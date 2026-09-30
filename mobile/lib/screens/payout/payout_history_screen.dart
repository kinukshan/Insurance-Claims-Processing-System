import 'package:flutter/material.dart';
import '../../models/payout.dart';
import '../../services/payout_service.dart';

/// Payout history screen — Component D (Kinukshan).
/// Policyholder-facing list of payout history entries with search, filtering, and pull-to-refresh.
class PayoutHistoryScreen extends StatefulWidget {
  final PayoutService? payoutService;

  const PayoutHistoryScreen({super.key, this.payoutService});

  @override
  State<PayoutHistoryScreen> createState() => _PayoutHistoryScreenState();
}

class _PayoutHistoryScreenState extends State<PayoutHistoryScreen> {
  late final PayoutService _payoutService;
  final TextEditingController _searchController = TextEditingController();
  List<Payout> _payouts = [];
  bool _loading = true;
  String? _error;
  String _searchQuery = '';
  String _selectedStatus = 'All';

  static const List<String> _statusFilters = [
    'All',
    'PendingApproval',
    'Approved',
    'Processing',
    'Paid',
    'Failed',
  ];

  @override
  void initState() {
    super.initState();
    _payoutService = widget.payoutService ?? PayoutService();
    _loadHistory();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadHistory() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final payouts = await _payoutService.getPayoutHistory();
      if (mounted) {
        setState(() {
          _payouts = payouts;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = e.toString();
          _loading = false;
        });
      }
    }
  }

  List<Payout> get _filteredPayouts {
    return _payouts.where((payout) {
      final query = _searchQuery.trim().toLowerCase();
      final matchesQuery = query.isEmpty ||
          (payout.paymentReference?.toLowerCase().contains(query) ?? false) ||
          payout.claimId.toLowerCase().contains(query) ||
          payout.formattedPayout.toLowerCase().contains(query);

      final matchesStatus = _selectedStatus == 'All' ||
          payout.status.toLowerCase() == _selectedStatus.toLowerCase() ||
          payout.statusDisplay.toLowerCase() == _selectedStatus.toLowerCase();

      return matchesQuery && matchesStatus;
    }).toList();
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

  String _formatStatusLabel(String status) {
    return switch (status) {
      'PendingApproval' => 'Pending Approval',
      _ => status,
    };
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Payout History'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _loadHistory,
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
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
        ),
      );
    }

    if (_payouts.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.payments_outlined, size: 54, color: Colors.grey),
              SizedBox(height: 12),
              Text(
                'No payout history found.',
                style: TextStyle(fontSize: 16, color: Colors.grey),
              ),
            ],
          ),
        ),
      );
    }

    final filtered = _filteredPayouts;

    return Column(
      children: [
        // Search bar
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
          child: TextField(
            controller: _searchController,
            decoration: InputDecoration(
              hintText: 'Search payouts (amount, reference, claim ID)...',
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
                label: Text(status == 'All' ? 'All' : _formatStatusLabel(status)),
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
                        const Icon(Icons.search_off, size: 54, color: Colors.grey),
                        const SizedBox(height: 12),
                        const Text(
                          'No matching payouts',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'No payouts found matching your search or status filter.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: Colors.grey),
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
                  onRefresh: _loadHistory,
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: filtered.length,
                    itemBuilder: (context, index) {
                      final payout = filtered[index];
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
                            '${payout.statusDisplay == "Approved" ? "Approved (Pending Disbursement)" : payout.statusDisplay} · ${payout.createdAt.day}/${payout.createdAt.month}/${payout.createdAt.year}',
                            style: const TextStyle(fontSize: 12),
                          ),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () {
                            Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (_) => PayoutDetailScreen(
                                  payoutId: payout.id,
                                  payoutService: _payoutService,
                                ),
                              ),
                            );
                          },
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

/// Payout detail screen — policyholder-facing.
/// Shows calculation breakdown and status without confidential data.
class PayoutDetailScreen extends StatefulWidget {
  final String payoutId;
  final PayoutService? payoutService;

  const PayoutDetailScreen({
    super.key,
    required this.payoutId,
    this.payoutService,
  });

  @override
  State<PayoutDetailScreen> createState() => _PayoutDetailScreenState();
}

class _PayoutDetailScreenState extends State<PayoutDetailScreen> {
  late final PayoutService _payoutService;
  Payout? _payout;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _payoutService = widget.payoutService ?? PayoutService();
    _loadDetail();
  }

  Future<void> _loadDetail() async {
    setState(() { _loading = true; _error = null; });
    try {
      final payout = await _payoutService.getPayoutById(widget.payoutId);
      if (mounted) setState(() { _payout = payout; _loading = false; });
    } catch (e) {
      if (mounted) setState(() { _error = e.toString(); _loading = false; });
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
                                  label: Text(
                                    _payout!.statusDisplay == 'Approved'
                                        ? 'Approved (Pending Disbursement)'
                                        : _payout!.statusDisplay == 'Paid'
                                            ? 'Paid / Disbursed'
                                            : _payout!.statusDisplay,
                                  ),
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
                                      'LKR ${_payout!.approvedClaimAmount.toStringAsFixed(2)}'),
                                  _row('Coverage Limit',
                                      'LKR ${_payout!.coverageLimit.toStringAsFixed(2)}'),
                                  _row('Deductible',
                                      '- LKR ${_payout!.deductible.toStringAsFixed(2)}'),
                                  const Divider(),
                                  _row('Final Payout', _payout!.formattedPayout,
                                      bold: true),
                                ],
                              ),
                            ),
                          ),

                          // Payment reference
                          if (_payout!.paymentReference != null && _payout!.paymentReference!.isNotEmpty) ...[
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
