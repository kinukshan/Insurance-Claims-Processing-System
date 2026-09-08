/// Payout model.
class Payout {
  final String id;
  final String claimId;
  final double amount;
  final double deductibleApplied;
  final String status;

  Payout({
    required this.id,
    required this.claimId,
    required this.amount,
    required this.deductibleApplied,
    required this.status,
  });

  // TODO: Add fromJson, toJson
}
