import 'package:flutter/material.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Policy detail screen — Component A (Member 1).
/// Displays full policy details, coverage, premium, expiry, and renewal status.
class PolicyDetailsScreen extends StatefulWidget {
  final String policyId;
  final PolicyService? policyService;

  const PolicyDetailsScreen({
    super.key,
    required this.policyId,
    this.policyService,
  });

  @override
  State<PolicyDetailsScreen> createState() => _PolicyDetailsScreenState();
}

class _PolicyDetailsScreenState extends State<PolicyDetailsScreen> {
  late final PolicyService _policyService;
  Policy? _policy;
  List<PolicyCoverage> _coverages = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _policyService = widget.policyService ?? PolicyService();
    _fetchDetails();
  }

  Future<void> _fetchDetails() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final policy = await _policyService.getPolicyById(widget.policyId);
      List<PolicyCoverage> coverages = [];
      try {
        coverages = await _policyService.getCoverage(widget.policyId);
      } catch (_) {
        // Fallback gracefully if coverage endpoint returns 404 or empty
      }
      if (mounted) {
        setState(() {
          _policy = policy;
          _coverages = coverages;
          _loading = false;
        });
      }
    } catch (e, stack) {
      debugPrint('POLICY_DETAILS_ERROR: $e\n$stack');
      if (mounted) {
        setState(() {
          _error = 'Failed to load policy details: $e';
          _loading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Policy Details'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _fetchDetails,
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: AppTheme.primaryTeal),
      );
    }

    if (_error != null) {
      return ErrorRetryView(
        message: _error!,
        onRetry: _fetchDetails,
      );
    }

    if (_policy == null) {
      return const EmptyStateView(
        icon: Icons.search_off_outlined,
        title: 'Policy Not Found',
        description: 'Unable to locate policy details for the requested ID.',
      );
    }

    final policy = _policy!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Card
          Card(
            elevation: 0.5,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
              side: BorderSide(color: Colors.grey.shade200),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          policy.policyNumber,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                            color: AppTheme.deepNavy,
                          ),
                        ),
                      ),
                      StatusBadge(status: policy.status),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text(
                    policy.policyTypeName,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: AppTheme.primaryTeal,
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Draft Status Guidance Banner
          if (policy.status.toLowerCase() == 'draft') ...[
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFFEF3C7),
                borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                border: Border.all(color: const Color(0xFFF59E0B).withValues(alpha: 0.4)),
              ),
              child: const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.info_outline, color: Color(0xFFD97706), size: 22),
                  SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Policy Status: Draft',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF92400E),
                          ),
                        ),
                        SizedBox(height: 4),
                        Text(
                          'This policy has been submitted and is awaiting review and activation by an administrator. Claims cannot be filed against Draft policies until they are activated.',
                          style: TextStyle(
                            fontSize: 12.5,
                            color: Color(0xFF92400E),
                            height: 1.35,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
          ],

          // Policy Information Card
          Card(
            elevation: 0.5,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
              side: BorderSide(color: Colors.grey.shade200),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Policy Information',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  const Divider(height: 20),
                  _infoRow('Coverage Limit', 'LKR ${policy.coverageLimit.toStringAsFixed(2)}'),
                  _infoRow('Premium', 'LKR ${policy.premium.toStringAsFixed(2)}'),
                  _infoRow('Deductible', policy.formattedDeductible),
                  _infoRow(
                    'Start Date',
                    '${policy.startDate.day}/${policy.startDate.month}/${policy.startDate.year}',
                  ),
                  _infoRow(
                    'Expiry Date',
                    '${policy.expiryDate.day}/${policy.expiryDate.month}/${policy.expiryDate.year}',
                  ),
                  _infoRow('Renewal Status', policy.renewalStatus),
                  _infoRow('Expired', policy.isExpired ? 'Yes' : 'No'),
                  if (policy.exclusions != null && policy.exclusions!.isNotEmpty)
                    _infoRow('Exclusions', policy.exclusions!),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Coverage Details Card
          Card(
            elevation: 0.5,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
              side: BorderSide(color: Colors.grey.shade200),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Coverage Details',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  const Divider(height: 20),
                  if (_coverages.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Text(
                        'Standard coverage active under policy terms.',
                        style: TextStyle(color: AppTheme.textSecondary, fontSize: 13),
                      ),
                    )
                  else
                    ..._coverages.map(
                      (cov) => Padding(
                        padding: const EdgeInsets.symmetric(vertical: 8),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              cov.coverageType,
                              style: const TextStyle(
                                fontWeight: FontWeight.w600,
                                fontSize: 14,
                                color: AppTheme.deepNavy,
                              ),
                            ),
                            if (cov.description != null) ...[
                              const SizedBox(height: 2),
                              Text(
                                cov.description!,
                                style: const TextStyle(
                                  color: AppTheme.textSecondary,
                                  fontSize: 12,
                                ),
                              ),
                            ],
                            const SizedBox(height: 4),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text(
                                  'Limit: LKR ${cov.coverageLimit.toStringAsFixed(0)}',
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppTheme.textSecondary,
                                  ),
                                ),
                                Text(
                                  'Deductible: LKR ${cov.deductibleAmount.toStringAsFixed(0)}',
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppTheme.textSecondary,
                                  ),
                                ),
                              ],
                            ),
                            Text(
                              'Coverage: ${cov.percentageOfCoverage}%',
                              style: const TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w500,
                                color: AppTheme.primaryTeal,
                              ),
                            ),
                            const Divider(height: 16),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(
              label,
              style: const TextStyle(
                color: AppTheme.textSecondary,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(
                color: AppTheme.deepNavy,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
