import 'package:flutter/material.dart';

/// Claim status timeline screen — Component B (Member 2).
/// Visual step indicator showing claim lifecycle progression.
class ClaimStatusScreen extends StatelessWidget {
  const ClaimStatusScreen({super.key});

  static const _statusSteps = [
    _StatusStep('Draft', Icons.edit_note, 'Claim created as draft'),
    _StatusStep('Submitted', Icons.send, 'Claim submitted for review'),
    _StatusStep('Under Review', Icons.visibility, 'Staff reviewing the claim'),
    _StatusStep('Document Verification', Icons.fact_check, 'AI agent verifying documents'),
    _StatusStep('Risk Assessment', Icons.analytics, 'Risk assessment in progress'),
    _StatusStep('Pending Approval', Icons.hourglass_top, 'Awaiting final approval'),
    _StatusStep('Approved', Icons.check_circle, 'Claim approved for payout'),
    _StatusStep('Payout Processing', Icons.payments, 'Payout being processed'),
    _StatusStep('Closed', Icons.lock, 'Claim completed and closed'),
  ];

  static const _statusMap = {
    'Draft': 0,
    'Submitted': 1,
    'UnderReview': 2,
    'DocumentVerification': 3,
    'AdditionalDocumentsRequired': 3,
    'RiskAssessment': 4,
    'PendingApproval': 5,
    'Approved': 6,
    'PayoutProcessing': 7,
    'Closed': 8,
    'Rejected': -1,
    'Withdrawn': -2,
  };

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final currentStatus = ModalRoute.of(context)!.settings.arguments as String;
    final currentStep = _statusMap[currentStatus] ?? 0;
    final isTerminal = currentStep < 0;

    return Scaffold(
      appBar: AppBar(title: const Text('Claim Status')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          // Current status display
          Center(
            child: Column(
              children: [
                Icon(
                  isTerminal
                      ? (currentStatus == 'Rejected'
                          ? Icons.cancel
                          : Icons.undo)
                      : Icons.timeline,
                  size: 48,
                  color: isTerminal
                      ? (currentStatus == 'Rejected' ? Colors.red : Colors.blueGrey)
                      : theme.colorScheme.primary,
                ),
                const SizedBox(height: 12),
                Text(
                  currentStatus == 'Rejected'
                      ? 'Claim Rejected'
                      : currentStatus == 'Withdrawn'
                          ? 'Claim Withdrawn'
                          : 'Claim In Progress',
                  style: theme.textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  _statusDescription(currentStatus),
                  style: TextStyle(color: theme.colorScheme.outline),
                ),
              ],
            ),
          ),
          const SizedBox(height: 32),

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
                            ? 'This claim has been rejected. Contact support for details.'
                            : 'This claim has been withdrawn by the policyholder.',
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
            // Timeline steps
            ...List.generate(_statusSteps.length, (index) {
              final step = _statusSteps[index];
              final isCompleted = index < currentStep;
              final isCurrent = index == currentStep;
              final isFuture = index > currentStep;

              return _TimelineStepWidget(
                step: step,
                isCompleted: isCompleted,
                isCurrent: isCurrent,
                isFuture: isFuture,
                isLast: index == _statusSteps.length - 1,
              );
            }),
          ],
        ],
      ),
    );
  }

  String _statusDescription(String status) {
    return switch (status) {
      'Draft' => 'Your claim is saved as a draft',
      'Submitted' => 'Your claim has been submitted for processing',
      'UnderReview' => 'A claims adjuster is reviewing your claim',
      'DocumentVerification' => 'Documents are being verified by the AI agent',
      'AdditionalDocumentsRequired' => 'Please upload additional documents',
      'RiskAssessment' => 'Risk assessment is in progress',
      'PendingApproval' => 'Your claim is awaiting final approval',
      'Approved' => 'Congratulations! Your claim has been approved',
      'Rejected' => 'Unfortunately, your claim has been rejected',
      'Withdrawn' => 'This claim has been withdrawn',
      'PayoutProcessing' => 'Your payout is being processed',
      'Closed' => 'This claim has been completed',
      _ => 'Status: $status',
    };
  }
}

/// Data model for a timeline step.
class _StatusStep {
  final String label;
  final IconData icon;
  final String description;

  const _StatusStep(this.label, this.icon, this.description);
}

/// Widget for a single timeline step with connector line.
class _TimelineStepWidget extends StatelessWidget {
  final _StatusStep step;
  final bool isCompleted;
  final bool isCurrent;
  final bool isFuture;
  final bool isLast;

  const _TimelineStepWidget({
    required this.step,
    required this.isCompleted,
    required this.isCurrent,
    required this.isFuture,
    required this.isLast,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final color = isCompleted
        ? Colors.green
        : isCurrent
            ? theme.colorScheme.primary
            : theme.colorScheme.outlineVariant;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Timeline indicator column
          SizedBox(
            width: 40,
            child: Column(
              children: [
                // Circle indicator
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: isCompleted || isCurrent
                        ? color.withValues(alpha: 0.15)
                        : Colors.transparent,
                    border: Border.all(color: color, width: 2),
                  ),
                  child: Center(
                    child: isCompleted
                        ? const Icon(Icons.check, size: 16, color: Colors.green)
                        : isCurrent
                            ? Icon(step.icon, size: 16, color: color)
                            : Icon(step.icon, size: 14, color: color),
                  ),
                ),
                // Connector line
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin: const EdgeInsets.symmetric(vertical: 4),
                      color: isCompleted
                          ? Colors.green.withValues(alpha: 0.4)
                          : theme.colorScheme.outlineVariant.withValues(alpha: 0.3),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 12),

          // Step content
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    step.label,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: isCurrent ? FontWeight.w700 : FontWeight.w500,
                      color: isFuture
                          ? theme.colorScheme.outlineVariant
                          : theme.colorScheme.onSurface,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    step.description,
                    style: TextStyle(
                      fontSize: 12,
                      color: isFuture
                          ? theme.colorScheme.outlineVariant
                          : theme.colorScheme.outline,
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
