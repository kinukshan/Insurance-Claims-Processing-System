import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../models/payout.dart';
import '../../services/claim_service.dart';
import '../../services/payout_service.dart';

/// Claim status timeline screen — Component B (Member 2).
/// Visual step indicator showing authoritative claim lifecycle progression.
/// Removes misleading hardcoded progression and verifies backend state for every milestone.
/// Displays confirmed timestamps where available without inventing dates.
class ClaimStatusScreen extends StatefulWidget {
  final String? claimId;
  final String? status;
  final ClaimService? claimService;
  final PayoutService? payoutService;

  const ClaimStatusScreen({
    super.key,
    this.claimId,
    this.status,
    this.claimService,
    this.payoutService,
  });

  @override
  State<ClaimStatusScreen> createState() => _ClaimStatusScreenState();
}

class _ClaimStatusScreenState extends State<ClaimStatusScreen> {
  late final ClaimService _claimService;
  late final PayoutService _payoutService;

  Claim? _claim;
  Payout? _payout;
  bool _loading = false;
  String? _error;
  String? _effectiveClaimId;
  String _fallbackStatus = 'Submitted';
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    _claimService = widget.claimService ?? ClaimService();
    _payoutService = widget.payoutService ?? PayoutService();
    _effectiveClaimId = widget.claimId;
    if (widget.status != null) {
      _fallbackStatus = widget.status!;
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_initialized) {
      _initialized = true;
      final routeArgs = ModalRoute.of(context)?.settings.arguments;
      if (routeArgs is String) {
        // Can be claimId or status
        if (routeArgs.contains('-') && routeArgs.length > 20) {
          _effectiveClaimId = routeArgs;
        } else {
          _fallbackStatus = routeArgs;
        }
      } else if (routeArgs is Map) {
        _effectiveClaimId = routeArgs['claimId'] as String?;
        if (routeArgs['status'] != null) {
          _fallbackStatus = routeArgs['status'] as String;
        }
      }

      if (_effectiveClaimId != null) {
        _loadAuthoritativeState();
      }
    }
  }

  Future<void> _loadAuthoritativeState() async {
    if (_effectiveClaimId == null) return;

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final claim = await _claimService.getClaim(_effectiveClaimId!);
      Payout? payout;
      try {
        payout = await _payoutService.getPayoutByClaimId(_effectiveClaimId!);
      } catch (_) {
        // Payout may not exist yet
      }

      if (mounted) {
        setState(() {
          _claim = claim;
          _payout = payout;
          _fallbackStatus = claim.status;
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

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final currentStatus = _claim?.status ?? _fallbackStatus;
    final isTerminal = currentStatus == 'Rejected' || currentStatus == 'Withdrawn';

    return Scaffold(
      appBar: AppBar(
        title: Text(_claim != null ? '${_claim!.claimNumber} Status' : 'Claim Status'),
        actions: [
          if (_effectiveClaimId != null)
            IconButton(
              icon: const Icon(Icons.refresh),
              tooltip: 'Refresh Status',
              onPressed: _loadAuthoritativeState,
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
                        FilledButton.tonal(
                          onPressed: _loadAuthoritativeState,
                          child: const Text('Retry'),
                        ),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
              onRefresh: _effectiveClaimId != null
                  ? _loadAuthoritativeState
                  : () async {},
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: [
                  // Status Header
                  Center(
                    child: Column(
                      children: [
                        Icon(
                          isTerminal
                              ? (currentStatus == 'Rejected' ? Icons.cancel : Icons.undo)
                              : (_payout?.statusDisplay == 'Paid'
                                  ? Icons.check_circle
                                  : Icons.timeline),
                          size: 48,
                          color: isTerminal
                              ? (currentStatus == 'Rejected' ? Colors.red : Colors.blueGrey)
                              : (_payout?.statusDisplay == 'Paid'
                                  ? Colors.green
                                  : theme.colorScheme.primary),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          currentStatus == 'Rejected'
                              ? 'Claim Rejected'
                              : currentStatus == 'Withdrawn'
                                  ? 'Claim Withdrawn'
                                  : (_payout?.statusDisplay == 'Paid'
                                      ? 'Claim Fully Settled'
                                      : 'Claim In Progress'),
                          style: theme.textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _statusDescription(currentStatus, _payout),
                          textAlign: TextAlign.center,
                          style: TextStyle(color: theme.colorScheme.outline),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 28),

                  // Terminal status notice
                  if (isTerminal)
                    Card(
                      color: currentStatus == 'Rejected'
                          ? Colors.red.withValues(alpha: 0.08)
                          : Colors.blueGrey.withValues(alpha: 0.08),
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Row(
                          children: [
                            Icon(
                              currentStatus == 'Rejected' ? Icons.info : Icons.info_outline,
                              color: currentStatus == 'Rejected' ? Colors.red : Colors.blueGrey,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                currentStatus == 'Rejected'
                                    ? 'This claim was reviewed and rejected. Please contact your claims adjuster for assistance.'
                                    : 'This claim was withdrawn by the policyholder.',
                                style: TextStyle(
                                  color: currentStatus == 'Rejected' ? Colors.red : Colors.blueGrey,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),

                  if (!isTerminal) ...[
                    Text(
                      'Authoritative Progression',
                      style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 16),
                    ..._buildMilestoneSteps(theme, currentStatus),
                  ],
                ],
              ),
            ),
    );
  }

  List<Widget> _buildMilestoneSteps(ThemeData theme, String claimStatus) {
    // 1. Claim Created: confirmed if claim exists
    final isCreatedConfirmed = _claim != null;
    final createdDate = _claim?.createdAt;

    // 2. Claim Submitted: confirmed if submittedAt is non-null or status is not Draft
    final isSubmittedConfirmed = _claim?.submittedAt != null ||
        (claimStatus != 'Draft' && _claim != null);
    final submittedDate = _claim?.submittedAt;

    // 3. Document Verification: confirmed if all documents exist and are verified
    final docs = _claim?.documents ?? [];
    final isDocVerified = docs.isNotEmpty &&
        docs.every((d) => d.verificationStatus == 'Verified');
    final isDocPending = isSubmittedConfirmed && !isDocVerified;

    // 4. Payout Proposal: confirmed if payout exists
    final isPayoutProposed = _payout != null;
    final payoutCreatedDate = _payout?.createdAt;

    // 5. Payout Approved: confirmed if payout status is Approved, Processing, or Paid
    final isPayoutApproved = _payout != null &&
        (_payout!.statusDisplay == 'Approved' ||
            _payout!.statusDisplay == 'Processing' ||
            _payout!.statusDisplay == 'Paid');
    final payoutApprovalDate = _payout?.approvalTimestamp;

    // 6. Payment Disbursed: confirmed ONLY if payout status is Paid
    final isPaymentDisbursed = _payout != null && _payout!.statusDisplay == 'Paid';
    final disbursementDate = isPaymentDisbursed ? _payout!.updatedAt : null;

    final steps = [
      _MilestoneStepData(
        title: 'Claim Created',
        description: 'Claim registered in the system',
        icon: Icons.edit_note,
        isConfirmed: isCreatedConfirmed,
        isCurrent: isCreatedConfirmed && !isSubmittedConfirmed,
        timestamp: createdDate,
      ),
      _MilestoneStepData(
        title: 'Claim Submitted',
        description: isSubmittedConfirmed
            ? 'Submitted for adjuster review'
            : 'Awaiting policyholder submission',
        icon: Icons.send,
        isConfirmed: isSubmittedConfirmed,
        isCurrent: isSubmittedConfirmed && !isDocVerified && !isPayoutProposed,
        timestamp: submittedDate,
      ),
      _MilestoneStepData(
        title: 'Document Verification',
        description: isDocVerified
            ? 'All documents verified by staff/AI'
            : isDocPending
                ? 'Documents under review'
                : 'Pending claim submission',
        icon: Icons.fact_check,
        isConfirmed: isDocVerified,
        isCurrent: isDocPending && !isPayoutProposed,
        timestamp: null, // No single backend document verification timestamp
      ),
      _MilestoneStepData(
        title: 'Payout Proposal Prepared',
        description: isPayoutProposed
            ? 'Payout of ${_payout!.formattedPayout} calculated'
            : 'Awaiting adjuster calculation',
        icon: Icons.calculate_outlined,
        isConfirmed: isPayoutProposed,
        isCurrent: isPayoutProposed && !isPayoutApproved,
        timestamp: payoutCreatedDate,
      ),
      _MilestoneStepData(
        title: 'Underwriter Approval',
        description: isPayoutApproved
            ? (_payout?.approvedBy != null
                ? 'Approved by ${_payout!.approvedBy}'
                : 'Payout approved for disbursement')
            : 'Awaiting underwriter authorization',
        icon: Icons.thumb_up_outlined,
        isConfirmed: isPayoutApproved,
        isCurrent: isPayoutApproved && !isPaymentDisbursed,
        timestamp: payoutApprovalDate,
      ),
      _MilestoneStepData(
        title: 'Payment Disbursed',
        description: isPaymentDisbursed
            ? 'Payment processed (${_payout?.paymentReference ?? 'Confirmed'})'
            : isPayoutApproved
                ? 'Approved — pending electronic disbursement'
                : 'Pending final disbursement',
        icon: Icons.payments_outlined,
        isConfirmed: isPaymentDisbursed,
        isCurrent: isPaymentDisbursed,
        timestamp: disbursementDate,
      ),
    ];

    return List.generate(steps.length, (index) {
      final s = steps[index];
      return _TimelineRow(
        data: s,
        isLast: index == steps.length - 1,
      );
    });
  }

  String _statusDescription(String status, Payout? payout) {
    if (payout != null && payout.statusDisplay == 'Paid') {
      return 'Payment of ${payout.formattedPayout} has been fully disbursed.';
    }
    if (payout != null && payout.statusDisplay == 'Approved') {
      return 'Payout of ${payout.formattedPayout} approved by underwriting; awaiting disbursement.';
    }
    return switch (status) {
      'Draft' => 'Claim saved as draft. Upload documents to submit.',
      'Submitted' => 'Claim submitted. Adjuster review and document verification in progress.',
      'UnderReview' => 'Claims adjuster is reviewing incident details.',
      'DocumentVerification' => 'Documents are undergoing integrity verification.',
      'AdditionalDocumentsRequired' => 'Please upload additional requested documents.',
      'RiskAssessment' => 'Risk assessment in progress.',
      'PendingApproval' => 'Claim is awaiting underwriter approval.',
      'Approved' => 'Claim approved for payout.',
      'Rejected' => 'Unfortunately, your claim has been rejected.',
      'Withdrawn' => 'This claim has been withdrawn.',
      'PayoutProcessing' => 'Your payout is currently being processed.',
      'Closed' => 'This claim has been completed.',
      _ => 'Status: $status',
    };
  }
}

class _MilestoneStepData {
  final String title;
  final String description;
  final IconData icon;
  final bool isConfirmed;
  final bool isCurrent;
  final DateTime? timestamp;

  const _MilestoneStepData({
    required this.title,
    required this.description,
    required this.icon,
    required this.isConfirmed,
    required this.isCurrent,
    this.timestamp,
  });
}

class _TimelineRow extends StatelessWidget {
  final _MilestoneStepData data;
  final bool isLast;

  const _TimelineRow({
    required this.data,
    required this.isLast,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = data.isConfirmed
        ? Colors.green
        : data.isCurrent
            ? theme.colorScheme.primary
            : Colors.grey.shade400;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 36,
            child: Column(
              children: [
                Container(
                  width: 30,
                  height: 30,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: data.isConfirmed
                        ? Colors.green.withValues(alpha: 0.15)
                        : data.isCurrent
                            ? theme.colorScheme.primary.withValues(alpha: 0.15)
                            : Colors.grey.shade100,
                    border: Border.all(color: color, width: 2),
                  ),
                  child: Center(
                    child: data.isConfirmed
                        ? const Icon(Icons.check, size: 16, color: Colors.green)
                        : Icon(data.icon, size: 14, color: color),
                  ),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin: const EdgeInsets.symmetric(vertical: 4),
                      color: data.isConfirmed
                          ? Colors.green.withValues(alpha: 0.4)
                          : Colors.grey.shade300,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        data.title,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: data.isCurrent || data.isConfirmed
                              ? FontWeight.w700
                              : FontWeight.w500,
                          color: data.isConfirmed || data.isCurrent
                              ? theme.colorScheme.onSurface
                              : Colors.grey.shade600,
                        ),
                      ),
                      if (data.timestamp != null)
                        Text(
                          DateFormat('MMM dd, hh:mm a').format(data.timestamp!),
                          style: TextStyle(
                            fontSize: 11,
                            color: Colors.grey.shade600,
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(
                    data.description,
                    style: TextStyle(
                      fontSize: 12,
                      color: data.isConfirmed
                          ? Colors.grey.shade700
                          : Colors.grey.shade500,
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
}
