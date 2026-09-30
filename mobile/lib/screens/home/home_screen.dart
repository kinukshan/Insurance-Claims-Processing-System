import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../models/claim.dart';
import '../../models/policy.dart';
import '../../providers/auth_provider.dart';
import '../../services/claim_service.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import '../claims/claim_details_screen.dart';
import '../claims/submit_claim_screen.dart';
import '../policy/policy_details_screen.dart';

/// Policyholder Home Dashboard — Component A/B/D overview.
///
/// Features:
/// - Branded Sri Lankan insurance theme (Teal, Navy, Gold).
/// - Authoritative policy & claim summaries from backend APIs.
/// - Quick action shortcuts.
/// - Recent claims and active policies with status badges.
/// - Loading, empty, and error retry states.
/// - Pull-to-refresh.
class HomeScreen extends StatefulWidget {
  final PolicyService? policyService;
  final ClaimService? claimService;
  final ValueChanged<int>? onNavigateTab;

  const HomeScreen({
    super.key,
    this.policyService,
    this.claimService,
    this.onNavigateTab,
  });

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  late final PolicyService _policyService;
  late final ClaimService _claimService;

  List<Policy> _policies = [];
  List<Claim> _claims = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _policyService = widget.policyService ?? PolicyService();
    _claimService = widget.claimService ?? ClaimService();
    _loadDashboardData();
  }

  Future<void> _loadDashboardData() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final results = await Future.wait([
        _policyService.getMyPolicies().catchError((e) {
          debugPrint('Error loading policies: $e');
          return <Policy>[];
        }),
        _claimService.getMyClaims().catchError((e) {
          debugPrint('Error loading claims: $e');
          return <Claim>[];
        }),
      ]);

      if (mounted) {
        setState(() {
          _policies = results[0] as List<Policy>;
          _claims = results[1] as List<Claim>;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load dashboard data. Please try again.';
          _loading = false;
        });
      }
    }
  }

  int get _activePolicyCount =>
      _policies.where((p) => p.status.toLowerCase() == 'active').length;

  int get _openClaimCount => _claims
      .where((c) =>
          !['approved', 'rejected', 'closed', 'withdrawn']
              .contains(c.status.toLowerCase()))
      .length;

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final userName = user?.firstName ?? 'Policyholder';

    return Scaffold(
      backgroundColor: AppTheme.backgroundGrey,
      appBar: AppBar(
        title: const Text('Dashboard'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _loadDashboardData,
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Log out',
            onPressed: () {
              context.read<AuthProvider>().logout();
            },
          ),
        ],
      ),
      body: _buildBody(userName),
    );
  }

  Widget _buildBody(String userName) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: AppTheme.primaryTeal),
      );
    }

    if (_error != null) {
      return ErrorRetryView(
        message: _error!,
        onRetry: _loadDashboardData,
      );
    }

    return RefreshIndicator(
      color: AppTheme.primaryTeal,
      onRefresh: _loadDashboardData,
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Welcome Hero Card
            _buildWelcomeBanner(userName),
            const SizedBox(height: 16),

            // Summary Metrics
            _buildSummaryMetrics(),
            const SizedBox(height: 20),

            // Quick Actions
            _buildQuickActions(),
            const SizedBox(height: 24),

            // Recent Claims Section
            _buildRecentClaimsSection(),
            const SizedBox(height: 24),

            // Active Policies Section
            _buildActivePoliciesSection(),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }

  Widget _buildWelcomeBanner(String userName) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppTheme.deepNavy, Color(0xFF0F3B39)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(AppTheme.radiusLarge),
        boxShadow: [
          BoxShadow(
            color: AppTheme.deepNavy.withValues(alpha: 0.15),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppTheme.goldAccent.withValues(alpha: 0.2),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: AppTheme.goldAccent.withValues(alpha: 0.4),
                  ),
                ),
                child: const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.shield, size: 12, color: AppTheme.goldAccent),
                    SizedBox(width: 4),
                    Text(
                      'POLICYHOLDER',
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.w700,
                        letterSpacing: 0.8,
                        color: AppTheme.goldAccent,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            'Welcome back, $userName',
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w700,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Manage your insurance coverage and track claims securely.',
            style: TextStyle(
              fontSize: 13,
              color: Colors.white.withValues(alpha: 0.8),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSummaryMetrics() {
    return Row(
      children: [
        Expanded(
          child: SummaryMetricCard(
            title: 'Active Policies',
            value: '$_activePolicyCount',
            subtitle: '${_policies.length} total',
            icon: Icons.shield_outlined,
            color: AppTheme.primaryTeal,
            onTap: () {
              if (widget.onNavigateTab != null) {
                widget.onNavigateTab!(1); // Policies tab
              }
            },
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: SummaryMetricCard(
            title: 'Active Claims',
            value: '$_openClaimCount',
            subtitle: '${_claims.length} total',
            icon: Icons.assignment_outlined,
            color: AppTheme.goldAccent,
            onTap: () {
              if (widget.onNavigateTab != null) {
                widget.onNavigateTab!(2); // Claims tab
              }
            },
          ),
        ),
      ],
    );
  }

  Widget _buildQuickActions() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Quick Actions',
          style: TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: AppTheme.deepNavy,
          ),
        ),
        const SizedBox(height: 12),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _actionButton(
              icon: Icons.add_circle_outline,
              label: 'File Claim',
              color: AppTheme.primaryTeal,
              onTap: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const SubmitClaimScreen()),
                ).then((_) => _loadDashboardData());
              },
            ),
            _actionButton(
              icon: Icons.shield_outlined,
              label: 'Policies',
              color: AppTheme.deepNavy,
              onTap: () {
                if (widget.onNavigateTab != null) {
                  widget.onNavigateTab!(1);
                }
              },
            ),
            _actionButton(
              icon: Icons.history,
              label: 'Claims',
              color: AppTheme.deepNavy,
              onTap: () {
                if (widget.onNavigateTab != null) {
                  widget.onNavigateTab!(2);
                }
              },
            ),
            _actionButton(
              icon: Icons.payments_outlined,
              label: 'Payouts',
              color: AppTheme.goldAccent,
              onTap: () {
                if (widget.onNavigateTab != null) {
                  widget.onNavigateTab!(3);
                }
              },
            ),
          ],
        ),
      ],
    );
  }

  Widget _actionButton({
    required IconData icon,
    required String label,
    required Color color,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
      child: Container(
        width: 80,
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.03),
              blurRadius: 6,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Column(
          children: [
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, color: color, size: 22),
            ),
            const SizedBox(height: 8),
            Text(
              label,
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: AppTheme.deepNavy,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildRecentClaimsSection() {
    final recentClaims = _claims.take(3).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Recent Claims',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: AppTheme.deepNavy,
              ),
            ),
            if (_claims.isNotEmpty)
              TextButton(
                onPressed: () {
                  if (widget.onNavigateTab != null) {
                    widget.onNavigateTab!(2);
                  }
                },
                child: const Text('View All'),
              ),
          ],
        ),
        const SizedBox(height: 8),
        if (recentClaims.isEmpty)
          Card(
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
            ),
            child: const Padding(
              padding: EdgeInsets.all(24),
              child: Center(
                child: Column(
                  children: [
                    Icon(
                      Icons.assignment_outlined,
                      size: 40,
                      color: AppTheme.textSecondary,
                    ),
                    SizedBox(height: 8),
                    Text(
                      'No claims filed yet',
                      style: TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                    SizedBox(height: 4),
                    Text(
                      'Submit a claim using the button above.',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppTheme.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          )
        else
          ...recentClaims.map((claim) {
            final dateStr = DateFormat('MMM dd, yyyy').format(claim.incidentDate);
            return Card(
              margin: const EdgeInsets.only(bottom: 10),
              elevation: 0.5,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                side: BorderSide(color: Colors.grey.shade200),
              ),
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                title: Text(
                  claim.claimNumber,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppTheme.deepNavy,
                  ),
                ),
                subtitle: Text(
                  '${claim.claimType} • LKR ${claim.claimedAmount.toStringAsFixed(0)} • $dateStr',
                  style: const TextStyle(fontSize: 12),
                ),
                trailing: StatusBadge(status: claim.status),
                onTap: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => ClaimDetailsScreen(claimId: claim.id),
                    ),
                  ).then((_) => _loadDashboardData());
                },
              ),
            );
          }),
      ],
    );
  }

  Widget _buildActivePoliciesSection() {
    final activePolicies = _policies
        .where((p) => p.status.toLowerCase() == 'active')
        .take(2)
        .toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Active Policies',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: AppTheme.deepNavy,
              ),
            ),
            if (_policies.isNotEmpty)
              TextButton(
                onPressed: () {
                  if (widget.onNavigateTab != null) {
                    widget.onNavigateTab!(1);
                  }
                },
                child: const Text('View All'),
              ),
          ],
        ),
        const SizedBox(height: 8),
        if (activePolicies.isEmpty)
          Card(
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
            ),
            child: const Padding(
              padding: EdgeInsets.all(24),
              child: Center(
                child: Column(
                  children: [
                    Icon(
                      Icons.shield_outlined,
                      size: 40,
                      color: AppTheme.textSecondary,
                    ),
                    SizedBox(height: 8),
                    Text(
                      'No active policies',
                      style: TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          )
        else
          ...activePolicies.map((policy) {
            return Card(
              margin: const EdgeInsets.only(bottom: 10),
              elevation: 0.5,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                side: BorderSide(color: Colors.grey.shade200),
              ),
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                title: Text(
                  policy.policyNumber,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppTheme.deepNavy,
                  ),
                ),
                subtitle: Text(
                  '${policy.policyTypeName} • Coverage: LKR ${policy.coverageLimit.toStringAsFixed(0)}',
                  style: const TextStyle(fontSize: 12),
                ),
                trailing: StatusBadge(status: policy.status),
                onTap: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => PolicyDetailsScreen(policyId: policy.id),
                    ),
                  );
                },
              ),
            );
          }),
      ],
    );
  }
}
