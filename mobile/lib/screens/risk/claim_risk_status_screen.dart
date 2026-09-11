import 'package:flutter/material.dart';
import '../../models/risk_assessment.dart';
import '../../services/risk_service.dart';

/// Claim risk status screen — Component C (Member 3).
///
/// Shows policyholder-safe review statuses only:
/// - Additional Review Required
/// - Under Manual Review
/// - Review Completed
///
/// Does NOT expose: fraud scores, fraud flags, detection rules,
/// or sensitive insurer reasoning.
class ClaimRiskStatusScreen extends StatefulWidget {
  final String claimId;

  const ClaimRiskStatusScreen({super.key, required this.claimId});

  @override
  State<ClaimRiskStatusScreen> createState() => _ClaimRiskStatusScreenState();
}

class _ClaimRiskStatusScreenState extends State<ClaimRiskStatusScreen> {
  final RiskService _riskService = RiskService();
  RiskAssessment? _assessment;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadStatus();
  }

  Future<void> _loadStatus() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final result = await _riskService.getReviewStatus(widget.claimId);
      setState(() {
        _assessment = result;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'Unable to load review status. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  void dispose() {
    _riskService.dispose();
    super.dispose();
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'Review Completed':
        return Colors.green;
      case 'Under Manual Review':
        return Colors.orange;
      case 'Additional Review Required':
        return Colors.blue;
      default:
        return Colors.grey;
    }
  }

  IconData _statusIcon(String status) {
    switch (status) {
      case 'Review Completed':
        return Icons.check_circle_outline;
      case 'Under Manual Review':
        return Icons.pending_outlined;
      case 'Additional Review Required':
        return Icons.info_outline;
      default:
        return Icons.help_outline;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Claim Review Status')),
      body: RefreshIndicator(
        onRefresh: _loadStatus,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_loading)
              const Center(
                child: Padding(
                  padding: EdgeInsets.all(48),
                  child: CircularProgressIndicator(),
                ),
              )
            else if (_error != null)
              Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline, size: 48, color: Colors.red),
                    const SizedBox(height: 12),
                    Text(_error!, textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                    ElevatedButton(
                      onPressed: _loadStatus,
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              )
            else if (_assessment == null)
              const Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.description_outlined, size: 48, color: Colors.grey),
                    SizedBox(height: 12),
                    Text(
                      'No review information available for this claim.',
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              )
            else ...[
              // Status Card
              Card(
                elevation: 2,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    children: [
                      Icon(
                        _statusIcon(_assessment!.reviewStatus),
                        size: 56,
                        color: _statusColor(_assessment!.reviewStatus),
                      ),
                      const SizedBox(height: 16),
                      Text(
                        _assessment!.reviewStatus,
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.bold,
                              color: _statusColor(_assessment!.reviewStatus),
                            ),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 8),
                      Text(
                        _statusDescription(_assessment!.reviewStatus),
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: Colors.grey[600],
                            ),
                        textAlign: TextAlign.center,
                      ),
                      if (_assessment!.lastUpdated != null) ...[
                        const SizedBox(height: 16),
                        const Divider(),
                        const SizedBox(height: 8),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(Icons.access_time, size: 16, color: Colors.grey[500]),
                            const SizedBox(width: 4),
                            Text(
                              'Last updated: ${_formatDate(_assessment!.lastUpdated!)}',
                              style: TextStyle(
                                fontSize: 13,
                                color: Colors.grey[500],
                              ),
                            ),
                          ],
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  String _statusDescription(String status) {
    switch (status) {
      case 'Review Completed':
        return 'Your claim has been reviewed and processed. No further action is required.';
      case 'Under Manual Review':
        return 'Your claim is currently being reviewed by our team. We will update you once the review is complete.';
      case 'Additional Review Required':
        return 'Your claim requires additional review. Our team will follow up with any required information.';
      default:
        return 'Your claim status will be updated shortly.';
    }
  }

  String _formatDate(DateTime date) {
    return '${date.day}/${date.month}/${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}
