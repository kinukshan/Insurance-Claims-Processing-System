import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../services/policy_service.dart';
import '../../services/claim_service.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Administrative Operations Command Center.
/// System-wide visibility across all insurance modules, staff workflows, and financial settlements.
class AdminCommandCenterScreen extends StatefulWidget {
  final void Function(int tabIndex)? onNavigateTab;

  const AdminCommandCenterScreen({super.key, this.onNavigateTab});

  @override
  State<AdminCommandCenterScreen> createState() => _AdminCommandCenterScreenState();
}

class _AdminCommandCenterScreenState extends State<AdminCommandCenterScreen> {
  final PolicyService _policyService = PolicyService();
  final ClaimService _claimService = ClaimService();
  final PayoutService _payoutService = PayoutService();

  int _totalPolicies = 0;
  int _draftPolicies = 0;
  int _activePolicies = 0;
  int _totalClaims = 0;
  int _pendingClaims = 0;
  int _readyPayouts = 0;
  double _totalDisbursed = 0.0;

  bool _loading = true;
  String? _error;

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 0);

  @override
  void initState() {
    super.initState();
    _loadMetrics();
  }

  Future<void> _loadMetrics() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final policiesFuture = _policyService.getAllPolicies();
      final claimsFuture = _claimService.getAllClaims();
      final payoutsFuture = _payoutService.getAllPayouts();

      final results = await Future.wait([policiesFuture, claimsFuture, payoutsFuture]);
      final policies = results[0] as List;
      final claims = results[1] as List;
      final payouts = results[2] as List;

      int draftP = 0;
      int activeP = 0;
      for (final p in policies) {
        if (p.status == 'Draft') draftP++;
        if (p.status == 'Active') activeP++;
      }

      int pendingC = 0;
      for (final c in claims) {
        if (c.status == 'Submitted' || c.status == 'UnderReview') pendingC++;
      }

      int readyP = 0;
      double disbursed = 0.0;
      for (final p in payouts) {
        if (p.statusDisplay == 'Approved') readyP++;
        if (p.statusDisplay == 'Paid') disbursed += p.finalPayout;
      }

      if (!mounted) return;
      setState(() {
        _totalPolicies = policies.length;
        _draftPolicies = draftP;
        _activePolicies = activeP;
        _totalClaims = claims.length;
        _pendingClaims = pendingC;
        _readyPayouts = readyP;
        _totalDisbursed = disbursed;
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Command Center'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadMetrics),
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
      return ErrorRetryView(message: _error!, onRetry: _loadMetrics);
    }

    return RefreshIndicator(
      onRefresh: _loadMetrics,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Hero Welcome Card
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  colors: [AppTheme.deepNavy, Color(0xFF1E3A8A)],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Operations Oversight',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          color: Colors.white,
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.amber.shade400,
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: const Text(
                          'ADMINISTRATOR',
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.w800,
                            color: Colors.black87,
                            letterSpacing: 0.5,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Full platform control: activate draft policies, monitor AI pipelines, and execute financial disbursements.',
                    style: TextStyle(fontSize: 12.5, color: Colors.white70, height: 1.35),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),

            // Top Alert for Draft Policies needing activation
            if (_draftPolicies > 0) ...[
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.amber.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.amber.shade300),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.pending_actions, color: Colors.amber, size: 24),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        '$_draftPolicies draft policy${_draftPolicies > 1 ? "ies" : ""} pending administrator activation.',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Colors.amber.shade900,
                        ),
                      ),
                    ),
                    TextButton(
                      onPressed: () => widget.onNavigateTab?.call(1),
                      child: const Text('Review'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Execution Alert for Approved Payouts
            if (_readyPayouts > 0) ...[
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.green.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.green.shade300),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.flash_on, color: Colors.green, size: 24),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        '$_readyPayouts approved payout${_readyPayouts > 1 ? "s" : ""} ready for execution.',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Colors.green.shade900,
                        ),
                      ),
                    ),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.green.shade700,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                      ),
                      onPressed: () => widget.onNavigateTab?.call(3),
                      child: const Text('Pay Now', style: TextStyle(fontSize: 12)),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Operations Metric Grid
            const Text(
              'System Performance & Pipeline',
              style: TextStyle(fontSize: 15.5, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
            ),
            const SizedBox(height: 12),
            GridView.count(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              crossAxisCount: 2,
              crossAxisSpacing: 12,
              mainAxisSpacing: 12,
              childAspectRatio: 0.98,
              children: [
                SummaryMetricCard(
                  title: 'Active Policies',
                  value: '$_activePolicies / $_totalPolicies',
                  subtitle: '$_draftPolicies drafts awaiting activation',
                  icon: Icons.shield_outlined,
                  color: AppTheme.primaryTeal,
                  onTap: () => widget.onNavigateTab?.call(1),
                ),
                SummaryMetricCard(
                  title: 'Pending Claims',
                  value: '$_pendingClaims / $_totalClaims',
                  subtitle: 'Investigation queue',
                  icon: Icons.assignment_outlined,
                  color: Colors.orange.shade700,
                  onTap: () => widget.onNavigateTab?.call(2),
                ),
                SummaryMetricCard(
                  title: 'Ready for Payment',
                  value: '$_readyPayouts',
                  subtitle: 'Approved payouts',
                  icon: Icons.payments_outlined,
                  color: Colors.green.shade700,
                  onTap: () => widget.onNavigateTab?.call(3),
                ),
                SummaryMetricCard(
                  title: 'Settled Disbursed',
                  value: _currencyFormat.format(_totalDisbursed),
                  subtitle: 'Total settled through gateway',
                  icon: Icons.account_balance,
                  color: Colors.indigo,
                  onTap: () => widget.onNavigateTab?.call(3),
                ),
              ],
            ),
            const SizedBox(height: 20),

            // AI Service Status Card
            Card(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              elevation: 1.5,
              child: const Padding(
                padding: EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(Icons.auto_awesome, color: Colors.purple, size: 20),
                        SizedBox(width: 8),
                        Text(
                          'Agentic AI Integration',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: AppTheme.deepNavy,
                          ),
                        ),
                      ],
                    ),
                    SizedBox(height: 8),
                    Text(
                      '• Agent 1: Coordinator / Planning Agent\n• Agent 2: Document Verification Agent (Gemini)\n• Agent 3: Fraud / Risk Assessment Agent (Gemini)\n• Agent 4: Validation / Safety Agent\n\nAll AI workflows are mediated by ASP.NET Core with automatic deterministic fallback if the internal service is unreachable.',
                      style: TextStyle(fontSize: 12.5, color: Colors.black87, height: 1.35),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }
}
