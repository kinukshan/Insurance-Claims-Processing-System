/// PayoutApproval model — policyholder-facing.
/// Only shows decision type, timestamp, and non-confidential summary.
/// Does NOT expose internal reviewer notes or reviewer identity.
class PayoutApproval {
  final String id;
  final String payoutId;
  final String decisionDisplay;
  final DateTime decisionTimestamp;
  final String? reviewerName;
  final String? comments;

  PayoutApproval({
    required this.id,
    required this.payoutId,
    required this.decisionDisplay,
    required this.decisionTimestamp,
    this.reviewerName,
    this.comments,
  });

  factory PayoutApproval.fromJson(Map<String, dynamic> json) {
    return PayoutApproval(
      id: json['id'] as String,
      payoutId: json['payoutId'] as String,
      decisionDisplay: json['decisionDisplay'] as String? ?? json['decision']?.toString() ?? '',
      decisionTimestamp: DateTime.parse(json['decisionTimestamp'] as String),
      reviewerName: json['reviewerName'] as String?,
      comments: json['comments'] as String?,
    );
  }
}
