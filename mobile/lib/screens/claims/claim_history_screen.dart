import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../services/claim_service.dart';
import '../../services/api_service.dart';

/// Claim history screen — Component B (Member 2).
/// Policyholder-facing list of submitted claims with pull-to-refresh.
class ClaimHistoryScreen extends StatefulWidget {
  const ClaimHistoryScreen({super.key});

  @override
  State<ClaimHistoryScreen> createState() => _ClaimHistoryScreenState();
}

class _ClaimHistoryScreenState extends State<ClaimHistoryScreen> {
  final _claimService = ClaimService();
  List<Claim>? _claims;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadClaims();
  }

  Future<void> _loadClaims() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final claims = await _claimService.getMyClaims();
      if (mounted) setState(() => _claims = claims);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Color _statusColor(String status) {
    return switch (status) {
      'Draft' => Colors.grey,
      'Submitted' => Colors.indigo,
      'UnderReview' || 'DocumentVerification' => Colors.blue,
      'AdditionalDocumentsRequired' => Colors.orange,
      'RiskAssessment' || 'PendingApproval' => Colors.amber.shade700,
      'Approved' || 'PayoutProcessing' => Colors.green,
      'Rejected' => Colors.red,
      'Withdrawn' || 'Closed' => Colors.blueGrey,
      _ => Colors.grey,
    };
  }

  String _formatStatus(String status) {
    return status.replaceAllMapped(
      RegExp(r'([A-Z])'),
      (match) => ' ${match.group(0)}',
    ).trim();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Claim History')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final result = await Navigator.pushNamed(context, '/claims/submit');
          if (result == true) _loadClaims();
        },
        icon: const Icon(Icons.add),
        label: const Text('New Claim'),
      ),
      body: _buildBody(theme),
    );
  }

  Widget _buildBody(ThemeData theme) {
    // Loading state
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    // Error state
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.error_outline, size: 48, color: theme.colorScheme.error),
              const SizedBox(height: 16),
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton.tonal(
                onPressed: _loadClaims,
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    // Empty state
    if (_claims == null || _claims!.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.inbox_outlined, size: 64, color: theme.colorScheme.outline),
            const SizedBox(height: 16),
            Text(
              'No claims yet',
              style: theme.textTheme.titleMedium?.copyWith(
                color: theme.colorScheme.outline,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Tap the + button to submit a new claim.',
              style: TextStyle(color: theme.colorScheme.outline),
            ),
          ],
        ),
      );
    }

    // Claims list with pull-to-refresh
    return RefreshIndicator(
      onRefresh: _loadClaims,
      child: ListView.builder(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        itemCount: _claims!.length,
        itemBuilder: (context, index) {
          final claim = _claims![index];
          final statusColor = _statusColor(claim.status);

          return Card(
            margin: const EdgeInsets.only(bottom: 12),
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () async {
                await Navigator.pushNamed(
                  context,
                  '/claims/details',
                  arguments: claim.id,
                );
                _loadClaims(); // Refresh on return
              },
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          claim.claimNumber,
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: theme.colorScheme.primary,
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: statusColor.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Text(
                            _formatStatus(claim.status),
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w600,
                              color: statusColor,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      claim.description,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: theme.colorScheme.onSurfaceVariant),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        _infoChip(Icons.category, claim.claimType, theme),
                        const SizedBox(width: 12),
                        _infoChip(
                          Icons.attach_money,
                          NumberFormat.currency(symbol: '\$').format(claim.claimedAmount),
                          theme,
                        ),
                        const SizedBox(width: 12),
                        _infoChip(
                          Icons.calendar_today,
                          DateFormat.yMMMd().format(claim.incidentDate),
                          theme,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _infoChip(IconData icon, String label, ThemeData theme) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: theme.colorScheme.outline),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(fontSize: 12, color: theme.colorScheme.outline),
        ),
      ],
    );
  }
}
