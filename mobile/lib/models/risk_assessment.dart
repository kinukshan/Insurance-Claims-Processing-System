/// Risk assessment model for policyholder-facing status.
///
/// IMPORTANT: This model must NOT expose confidential information:
/// - No internal fraud scores
/// - No internal fraud flags
/// - No detection rules
/// - No sensitive insurer reasoning
///
/// Only safe statuses are displayed:
/// - Additional Review Required
/// - Under Manual Review
/// - Review Completed
class RiskAssessment {
  final String id;
  final String claimId;
  final String reviewStatus;
  final DateTime? lastUpdated;

  RiskAssessment({
    required this.id,
    required this.claimId,
    required this.reviewStatus,
    this.lastUpdated,
  });

  // TODO: Add fromJson, toJson
}
