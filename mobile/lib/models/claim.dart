/// Claim model.
class Claim {
  final String id;
  final String claimNumber;
  final String policyId;
  final String description;
  final double claimAmount;
  final DateTime incidentDate;
  final String incidentLocation;
  final String status;

  Claim({
    required this.id,
    required this.claimNumber,
    required this.policyId,
    required this.description,
    required this.claimAmount,
    required this.incidentDate,
    required this.incidentLocation,
    required this.status,
  });

  // TODO: Add fromJson, toJson
}
