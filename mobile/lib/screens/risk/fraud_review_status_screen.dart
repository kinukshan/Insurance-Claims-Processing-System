import 'package:flutter/material.dart';
import '../../models/risk_assessment.dart';
import '../../services/risk_service.dart';
import 'claim_risk_status_screen.dart';

/// Fraud review status screen — Component C (Member 3).
///
/// Shows policyholder-safe review statuses only:
/// - Additional Review Required
/// - Under Manual Review
/// - Review Completed
///
/// Does NOT expose: fraud scores, fraud flags, detection rules,
/// or sensitive insurer reasoning.
class FraudReviewStatusScreen extends StatefulWidget {
  const FraudReviewStatusScreen({super.key});

  @override
  State<FraudReviewStatusScreen> createState() => _FraudReviewStatusScreenState();
}

class _FraudReviewStatusScreenState extends State<FraudReviewStatusScreen> {
  final RiskService _riskService = RiskService();
  List<RiskAssessment> _reviews = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadReviews();
  }

  Future<void> _loadReviews() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final results = await _riskService.getReviewHistory();
      setState(() {
        _reviews = results;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'Unable to load review statuses. Please try again.';
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
        return Icons.check_circle;
      case 'Under Manual Review':
        return Icons.pending;
      case 'Additional Review Required':
        return Icons.info;
      default:
        return Icons.help;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Review Status')),
      body: RefreshIndicator(
        onRefresh: _loadReviews,
        child: _buildBody(),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 12),
            Text(_error!, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _loadReviews,
              child: const Text('Retry'),
            ),
          ],
        ),
      );
    }

    if (_reviews.isEmpty) {
      return ListView(
        children: const [
          SizedBox(height: 100),
          Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.fact_check_outlined, size: 48, color: Colors.grey),
                SizedBox(height: 12),
                Text(
                  'No claims under review.',
                  style: TextStyle(fontSize: 16, color: Colors.grey),
                ),
                SizedBox(height: 4),
                Text(
                  'Your claims will appear here once submitted.',
                  style: TextStyle(fontSize: 14, color: Colors.grey),
                ),
              ],
            ),
          ),
        ],
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.all(12),
      itemCount: _reviews.length,
      itemBuilder: (context, index) {
        final review = _reviews[index];
        return Card(
          margin: const EdgeInsets.only(bottom: 8),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          child: ListTile(
            leading: Icon(
              _statusIcon(review.reviewStatus),
              color: _statusColor(review.reviewStatus),
              size: 32,
            ),
            title: Text(
              'Claim ${review.claimId.length > 8 ? '${review.claimId.substring(0, 8)}...' : review.claimId}',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(height: 4),
                Chip(
                  label: Text(
                    review.reviewStatus,
                    style: TextStyle(
                      fontSize: 12,
                      color: _statusColor(review.reviewStatus),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  backgroundColor:
                      _statusColor(review.reviewStatus).withValues(alpha: 0.1),
                  side: BorderSide.none,
                  padding: EdgeInsets.zero,
                  visualDensity: VisualDensity.compact,
                ),
                if (review.lastUpdated != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    'Updated: ${_formatDate(review.lastUpdated!)}',
                    style: TextStyle(fontSize: 12, color: Colors.grey[500]),
                  ),
                ],
              ],
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () {
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => ClaimRiskStatusScreen(claimId: review.claimId),
                ),
              );
            },
          ),
        );
      },
    );
  }

  String _formatDate(DateTime date) {
    return '${date.day}/${date.month}/${date.year}';
  }
}
