/// PayoutApproval model — policyholder-facing.
/// Only shows decision type, timestamp, and non-confidential summary.
/// Does NOT expose internal reviewer notes or reviewer identity.
class PayoutApproval {
  final String id;
  final String payoutId;
  final String decisionDisplay;
  final DateTime decisionTimestamp;

  PayoutApproval({
    required this.id,
    required this.payoutId,
    required this.decisionDisplay,
    required this.decisionTimestamp,
  });

  factory PayoutApproval.fromJson(Map<String, dynamic> json) {
    return PayoutApproval(
      id: json['id'] as String,
      payoutId: json['payoutId'] as String,
      decisionDisplay: json['decisionDisplay'] as String,
      decisionTimestamp: DateTime.parse(json['decisionTimestamp'] as String),
    );
  }
}
