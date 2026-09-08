/// Policy model.
class Policy {
  final String id;
  final String policyNumber;
  final String policyType;
  final DateTime startDate;
  final DateTime endDate;
  final double premiumAmount;
  final bool isActive;

  Policy({
    required this.id,
    required this.policyNumber,
    required this.policyType,
    required this.startDate,
    required this.endDate,
    required this.premiumAmount,
    required this.isActive,
  });

  // TODO: Add fromJson, toJson
}
