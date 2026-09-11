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

  /// Creates a RiskAssessment from a JSON map returned by the backend API.
  factory RiskAssessment.fromJson(Map<String, dynamic> json) {
    return RiskAssessment(
      id: json['claimId']?.toString() ?? '',
      claimId: json['claimId']?.toString() ?? '',
      reviewStatus: json['reviewStatus']?.toString() ?? 'Additional Review Required',
      lastUpdated: json['lastUpdated'] != null
          ? DateTime.tryParse(json['lastUpdated'].toString())
          : null,
    );
  }

  /// Converts to a JSON map.
  Map<String, dynamic> toJson() {
    return {
      'claimId': claimId,
      'reviewStatus': reviewStatus,
      'lastUpdated': lastUpdated?.toIso8601String(),
    };
  }

  /// Returns whether the review is still pending.
  bool get isPending =>
      reviewStatus == 'Additional Review Required' ||
      reviewStatus == 'Under Manual Review';

  /// Returns whether the review is complete.
  bool get isComplete => reviewStatus == 'Review Completed';
}
