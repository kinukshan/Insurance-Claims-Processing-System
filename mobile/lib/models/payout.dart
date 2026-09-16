/// Payout model — Component D (Kinukshan).
/// Policyholder-facing: no confidential internal reviewer notes exposed.
class Payout {
  final String id;
  final String claimId;
  final double approvedClaimAmount;
  final double coverageLimit;
  final double deductible;
  final double proposedPayout;
  final double finalPayout;
  final String status;
  final String statusDisplay;
  final String? approvedBy;
  final DateTime? approvalTimestamp;
  final String? paymentReference;
  final DateTime createdAt;
  final DateTime updatedAt;

  Payout({
    required this.id,
    required this.claimId,
    required this.approvedClaimAmount,
    required this.coverageLimit,
    required this.deductible,
    required this.proposedPayout,
    required this.finalPayout,
    required this.status,
    required this.statusDisplay,
    this.approvedBy,
    this.approvalTimestamp,
    this.paymentReference,
    required this.createdAt,
    required this.updatedAt,
  });

  factory Payout.fromJson(Map<String, dynamic> json) {
    return Payout(
      id: json['id'] as String,
      claimId: json['claimId'] as String,
      approvedClaimAmount: (json['approvedClaimAmount'] as num).toDouble(),
      coverageLimit: (json['coverageLimit'] as num).toDouble(),
      deductible: (json['deductible'] as num).toDouble(),
      proposedPayout: (json['proposedPayout'] as num).toDouble(),
      finalPayout: (json['finalPayout'] as num).toDouble(),
      status: json['status'].toString(),
      statusDisplay: json['statusDisplay'] as String,
      approvedBy: json['approvedBy'] as String?,
      approvalTimestamp: json['approvalTimestamp'] != null
          ? DateTime.parse(json['approvalTimestamp'] as String)
          : null,
      paymentReference: json['paymentReference'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'claimId': claimId,
      'approvedClaimAmount': approvedClaimAmount,
      'coverageLimit': coverageLimit,
      'deductible': deductible,
      'proposedPayout': proposedPayout,
      'finalPayout': finalPayout,
      'status': status,
      'statusDisplay': statusDisplay,
      'approvedBy': approvedBy,
      'approvalTimestamp': approvalTimestamp?.toIso8601String(),
      'paymentReference': paymentReference,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
    };
  }

  /// User-friendly formatted payout amount.
  String get formattedPayout => '\$${finalPayout.toStringAsFixed(2)}';

  /// Whether this payout is in a terminal state.
  bool get isTerminal =>
      statusDisplay == 'Paid' ||
      statusDisplay == 'Rejected' ||
      statusDisplay == 'Failed';

  /// Whether this payout is actively processing.
  bool get isProcessing => statusDisplay == 'Processing';
}
