import 'ai_workflow_models.dart';
import 'payout_approval.dart';

/// Payout model — Component D (Kinukshan).
class Payout {
  final String id;
  final String claimId;
  final String? claimNumber;
  final double approvedClaimAmount;
  final double coverageLimit;
  final double deductible;
  final double? deductiblePercentage;
  final double? eligibleAmount;
  final double proposedPayout;
  final double finalPayout;
  final String? explanation;
  final String status;
  final String statusDisplay;
  final String? approvedBy;
  final DateTime? approvalTimestamp;
  final String? paymentReference;
  final String? paymentProvider;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<PayoutApproval> approvals;
  final PayoutValidationResult? validationResult;

  Payout({
    required this.id,
    required this.claimId,
    this.claimNumber,
    required this.approvedClaimAmount,
    required this.coverageLimit,
    required this.deductible,
    this.deductiblePercentage,
    this.eligibleAmount,
    required this.proposedPayout,
    required this.finalPayout,
    this.explanation,
    required this.status,
    required this.statusDisplay,
    this.approvedBy,
    this.approvalTimestamp,
    this.paymentReference,
    this.paymentProvider,
    required this.createdAt,
    required this.updatedAt,
    this.approvals = const [],
    this.validationResult,
  });

  factory Payout.fromJson(Map<String, dynamic> json) {
    return Payout(
      id: json['id'] as String,
      claimId: json['claimId'] as String,
      claimNumber: json['claimNumber'] as String?,
      approvedClaimAmount: (json['approvedClaimAmount'] as num).toDouble(),
      coverageLimit: (json['coverageLimit'] as num).toDouble(),
      deductible: (json['deductible'] as num).toDouble(),
      deductiblePercentage: (json['deductiblePercentage'] as num?)?.toDouble(),
      eligibleAmount: (json['eligibleAmount'] as num?)?.toDouble(),
      proposedPayout: (json['proposedPayout'] as num).toDouble(),
      finalPayout: (json['finalPayout'] as num).toDouble(),
      explanation: json['explanation'] as String?,
      status: json['status'].toString(),
      statusDisplay: json['statusDisplay'] as String? ?? json['status']?.toString() ?? '',
      approvedBy: json['approvedBy'] as String?,
      approvalTimestamp: json['approvalTimestamp'] != null
          ? DateTime.parse(json['approvalTimestamp'] as String)
          : null,
      paymentReference: json['paymentReference'] as String?,
      paymentProvider: json['paymentProvider'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
      approvals: (json['approvals'] as List<dynamic>?)
              ?.map((a) => PayoutApproval.fromJson(a as Map<String, dynamic>))
              .toList() ??
          [],
      validationResult: json['validationResult'] != null
          ? PayoutValidationResult.fromJson(
              json['validationResult'] as Map<String, dynamic>)
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'claimId': claimId,
      'claimNumber': claimNumber,
      'approvedClaimAmount': approvedClaimAmount,
      'coverageLimit': coverageLimit,
      'deductible': deductible,
      if (deductiblePercentage != null) 'deductiblePercentage': deductiblePercentage,
      if (eligibleAmount != null) 'eligibleAmount': eligibleAmount,
      'proposedPayout': proposedPayout,
      'finalPayout': finalPayout,
      'explanation': explanation,
      'status': status,
      'statusDisplay': statusDisplay,
      'approvedBy': approvedBy,
      'approvalTimestamp': approvalTimestamp?.toIso8601String(),
      'paymentReference': paymentReference,
      'paymentProvider': paymentProvider,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
    };
  }

  /// Authoritative eligible amount min(approvedClaimAmount, coverageLimit).
  double get effectiveEligibleAmount =>
      eligibleAmount ?? (approvedClaimAmount < coverageLimit ? approvedClaimAmount : coverageLimit);

  /// User-friendly formatted payout amount.
  String get formattedPayout => 'LKR ${finalPayout.toStringAsFixed(2)}';

  /// User-friendly formatted deductible amount.
  String get formattedDeductible => 'LKR ${deductible.toStringAsFixed(2)}';

  /// Whether this payout is in a terminal state.
  bool get isTerminal =>
      statusDisplay == 'Paid' ||
      statusDisplay == 'Rejected' ||
      statusDisplay == 'Failed';

  /// Whether this payout is actively processing.
  bool get isProcessing => statusDisplay == 'Processing';
}
