import 'package:flutter/material.dart';
import '../models/ai_workflow_models.dart';
import '../utils/app_theme.dart';
import 'shared_widgets.dart';

/// Visual indicator showing whether an output came from live AI (Gemini)
/// or deterministic business rules / fallback.
class AiWorkflowBadge extends StatelessWidget {
  final bool aiUsed;
  final String? aiModel;
  final bool fallbackUsed;

  const AiWorkflowBadge({
    super.key,
    required this.aiUsed,
    this.aiModel,
    this.fallbackUsed = false,
  });

  @override
  Widget build(BuildContext context) {
    if (aiUsed && !fallbackUsed) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: Colors.purple.shade50,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.purple.shade300),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.auto_awesome, size: 14, color: Colors.purple.shade700),
            const SizedBox(width: 5),
            Text(
              aiModel != null ? 'AI Agent ($aiModel)' : 'AI Agent (Gemini)',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: Colors.purple.shade800,
              ),
            ),
          ],
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: AppTheme.primaryTeal.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppTheme.primaryTeal.withValues(alpha: 0.3)),
      ),
      child: const Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.rule_outlined, size: 14, color: AppTheme.primaryTeal),
          SizedBox(width: 5),
          Text(
            'Deterministic Rules',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w700,
              color: AppTheme.primaryTeal,
            ),
          ),
        ],
      ),
    );
  }
}

/// Card displaying AI Workflow 1: Coverage Validation Results.
class CoverageValidationCard extends StatelessWidget {
  final CoverageValidationResult result;

  const CoverageValidationCard({super.key, required this.result});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Row(
                  children: [
                    Icon(Icons.verified_user_outlined,
                        color: AppTheme.primaryTeal, size: 20),
                    SizedBox(width: 8),
                    Text(
                      'Policy Coverage Validation',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                  ],
                ),
                StatusBadge(
                  status: result.isValid && result.isCovered ? 'Active' : 'Rejected',
                  label: result.isValid && result.isCovered ? 'Covered' : 'Not Covered',
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _metric(
                  'Coverage Limit',
                  result.coverageLimit != null
                      ? 'LKR ${result.coverageLimit!.toStringAsFixed(0)}'
                      : 'N/A',
                ),
                _metric(
                  'Deductible',
                  result.deductibleAmount != null
                      ? 'LKR ${result.deductibleAmount!.toStringAsFixed(0)}'
                      : 'N/A',
                ),
                _metric(
                  'Coverage Type',
                  result.coverageType ?? 'Standard',
                ),
              ],
            ),
            if (result.issues.isNotEmpty) ...[
              const SizedBox(height: 12),
              const Divider(height: 1),
              const SizedBox(height: 10),
              const Text(
                'Validation Notes:',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 4),
              ...result.issues.map(
                (issue) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.circle, size: 6, color: Colors.orange),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          issue,
                          style: const TextStyle(fontSize: 12, color: Colors.black87),
                        ),
                      ),
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

  Widget _metric(String label, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
        const SizedBox(height: 2),
        Text(
          value,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w700,
            color: AppTheme.deepNavy,
          ),
        ),
      ],
    );
  }
}

/// Card displaying AI Workflow 2: Document Verification Agent Results.
class DocumentVerificationCard extends StatelessWidget {
  final DocumentVerificationResult result;

  const DocumentVerificationCard({super.key, required this.result});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Row(
                  children: [
                    Icon(Icons.document_scanner_outlined,
                        color: Colors.indigo, size: 20),
                    SizedBox(width: 8),
                    Text(
                      'AI Document Verification',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                  ],
                ),
                AiWorkflowBadge(
                  aiUsed: result.aiUsed,
                  aiModel: result.aiModel,
                  fallbackUsed: result.fallbackUsed,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Icon(
                  result.complete ? Icons.check_circle : Icons.warning_amber_rounded,
                  color: result.complete ? Colors.green : Colors.orange,
                  size: 18,
                ),
                const SizedBox(width: 6),
                Text(
                  result.complete
                      ? 'All Required Documents Verified'
                      : 'Missing or Incomplete Documentation',
                  style: TextStyle(
                    fontSize: 13.5,
                    fontWeight: FontWeight.w600,
                    color: result.complete ? Colors.green.shade800 : Colors.orange.shade800,
                  ),
                ),
              ],
            ),
            if (result.reasoningSummary != null &&
                result.reasoningSummary!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Colors.grey.shade50,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: Text(
                  result.reasoningSummary!,
                  style: const TextStyle(fontSize: 12.5, color: Colors.black87),
                ),
              ),
            ],
            if (result.missingItems.isNotEmpty) ...[
              const SizedBox(height: 10),
              const Text(
                'Missing Required Items:',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Colors.red),
              ),
              const SizedBox(height: 4),
              ...result.missingItems.map(
                (item) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      const Icon(Icons.close, size: 14, color: Colors.red),
                      const SizedBox(width: 6),
                      Text(item, style: const TextStyle(fontSize: 12, color: Colors.red)),
                    ],
                  ),
                ),
              ),
            ],
            if (result.inconsistencies.isNotEmpty) ...[
              const SizedBox(height: 10),
              const Text(
                'Identified Discrepancies:',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Colors.amber),
              ),
              const SizedBox(height: 4),
              ...result.inconsistencies.map(
                (inc) => Container(
                  margin: const EdgeInsets.symmetric(vertical: 3),
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: Colors.amber.shade50,
                    borderRadius: BorderRadius.circular(6),
                    border: Border.all(color: Colors.amber.shade200),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            inc.field,
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                              color: Colors.amber.shade900,
                            ),
                          ),
                          Text(
                            inc.severity,
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w600,
                              color: Colors.amber.shade900,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 2),
                      Text(inc.description, style: const TextStyle(fontSize: 11.5)),
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
}

/// Card displaying AI Workflow 3: Fraud & Risk Assessment Agent Results.
class RiskAssessmentCard extends StatelessWidget {
  final StaffRiskAssessment assessment;
  final VoidCallback? onEscalate;

  const RiskAssessmentCard({
    super.key,
    required this.assessment,
    this.onEscalate,
  });

  @override
  Widget build(BuildContext context) {
    final isHigh = assessment.riskLevel.toLowerCase() == 'high' ||
        assessment.riskScore >= 60;
    final isMedium = assessment.riskLevel.toLowerCase() == 'medium' ||
        (assessment.riskScore >= 30 && assessment.riskScore < 60);

    final color = isHigh
        ? Colors.red
        : isMedium
            ? Colors.orange
            : Colors.green;

    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Expanded(
                  child: Row(
                    children: [
                      Icon(Icons.shield_outlined, color: Colors.orange, size: 20),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Fraud & Risk Assessment',
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: AppTheme.deepNavy,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                AiWorkflowBadge(
                  aiUsed: assessment.aiUsed,
                  aiModel: assessment.aiModel,
                  fallbackUsed: assessment.fallbackUsed,
                ),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                // Score dial
                Container(
                  width: 58,
                  height: 58,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                    border: Border.all(color: color, width: 2.5),
                  ),
                  child: Center(
                    child: Text(
                      '${assessment.riskScore.toInt()}',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                        color: color,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          StatusBadge(
                            status: assessment.riskLevel,
                            customColor: color,
                          ),
                          const SizedBox(width: 8),
                          StatusBadge(
                            status: assessment.recommendation,
                            label: 'Rec: ${assessment.recommendation}',
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Flags: ${assessment.fraudFlagCount} detected',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppTheme.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                if (onEscalate != null && isHigh && !assessment.hasFraudCase)
                  ElevatedButton(
                    onPressed: onEscalate,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.red.shade600,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                    ),
                    child: const Text('Escalate', style: TextStyle(fontSize: 12)),
                  ),
              ],
            ),
            if (assessment.summary.isNotEmpty) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Colors.grey.shade50,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: Text(
                  assessment.summary,
                  style: const TextStyle(fontSize: 12.5, color: Colors.black87),
                ),
              ),
            ],
            if (assessment.flags.isNotEmpty) ...[
              const SizedBox(height: 12),
              const Text(
                'Identified Risk Indicators:',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 6),
              ...assessment.flags.map(
                (flag) => Container(
                  margin: const EdgeInsets.symmetric(vertical: 3),
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: Colors.red.withValues(alpha: 0.05),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: Colors.red.withValues(alpha: 0.2)),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.flag_outlined, size: 16, color: Colors.red),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text(
                                  flag.code,
                                  style: const TextStyle(
                                    fontSize: 12,
                                    fontWeight: FontWeight.w700,
                                    color: Colors.red,
                                  ),
                                ),
                                Text(
                                  flag.severity,
                                  style: const TextStyle(
                                    fontSize: 10.5,
                                    fontWeight: FontWeight.w600,
                                    color: AppTheme.textSecondary,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 2),
                            Text(
                              flag.description,
                              style: const TextStyle(fontSize: 11.5),
                            ),
                          ],
                        ),
                      ),
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
}

/// Card displaying AI Workflow 4: Validation / Safety Agent Results.
class PayoutValidationCard extends StatelessWidget {
  final PayoutValidationResult validation;

  const PayoutValidationCard({super.key, required this.validation});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Row(
                  children: [
                    Icon(Icons.safety_check, color: AppTheme.primaryTeal, size: 20),
                    SizedBox(width: 8),
                    Text(
                      'AI Safety & Validation Agent',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                  ],
                ),
                AiWorkflowBadge(
                  aiUsed: validation.aiUsed,
                  aiModel: validation.aiModel,
                  fallbackUsed: validation.fallbackUsed,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Icon(
                  validation.valid ? Icons.check_circle : Icons.error_outline,
                  color: validation.valid ? Colors.green : Colors.red,
                  size: 18,
                ),
                const SizedBox(width: 6),
                Text(
                  validation.valid
                      ? 'Payout Complies with Safety Policy'
                      : 'Rule Violations Detected',
                  style: TextStyle(
                    fontSize: 13.5,
                    fontWeight: FontWeight.w600,
                    color: validation.valid ? Colors.green.shade800 : Colors.red.shade800,
                  ),
                ),
              ],
            ),
            if (validation.summary.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(
                validation.summary,
                style: const TextStyle(fontSize: 12.5, color: Colors.black87),
              ),
            ],
            if (validation.violations.isNotEmpty) ...[
              const SizedBox(height: 10),
              ...validation.violations.map(
                (v) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.close, size: 14, color: Colors.red),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          v,
                          style: const TextStyle(fontSize: 12, color: Colors.red),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.blue.shade50,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: Colors.blue.shade200),
              ),
              child: const Row(
                children: [
                  Icon(Icons.person_pin, size: 16, color: Colors.blue),
                  SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      'Human Approval Required: Must be reviewed and authorized by an Underwriter or Administrator before payment execution.',
                      style: TextStyle(fontSize: 11.5, color: Colors.blue),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
