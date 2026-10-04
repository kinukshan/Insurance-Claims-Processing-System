import 'claim_document.dart';

/// Claim model matching the backend ClaimResponseDto / ClaimSummaryDto.
class Claim {
  final String id;
  final String? policyId;
  final String? policyHolderId;
  final String claimNumber;
  final String claimType;
  final String description;
  final double claimedAmount;
  final DateTime incidentDate;
  final String incidentLocation;
  final String status;
  final DateTime? submittedAt;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final List<ClaimDocument> documents;

  Claim({
    required this.id,
    this.policyId,
    this.policyHolderId,
    required this.claimNumber,
    required this.claimType,
    required this.description,
    required this.claimedAmount,
    required this.incidentDate,
    required this.incidentLocation,
    required this.status,
    this.submittedAt,
    required this.createdAt,
    this.updatedAt,
    this.documents = const [],
  });

  factory Claim.fromJson(Map<String, dynamic> json) {
    return Claim(
      id: json['id'] as String,
      policyId: json['policyId'] as String?,
      policyHolderId: json['policyHolderId'] as String?,
      claimNumber: json['claimNumber'] as String,
      claimType: json['claimType'] as String,
      description: json['description'] as String? ?? '',
      claimedAmount: (json['claimedAmount'] as num).toDouble(),
      incidentDate: _parseDateOnly(json['incidentDate']),
      incidentLocation: json['incidentLocation'] as String? ?? '',
      status: json['status'] as String,
      submittedAt: json['submittedAt'] != null
          ? DateTime.parse(json['submittedAt'] as String)
          : null,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : null,
      documents: (json['documents'] as List<dynamic>?)
              ?.map((d) => ClaimDocument.fromJson(d as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'policyId': policyId,
      'policyHolderId': policyHolderId,
      'claimNumber': claimNumber,
      'claimType': claimType,
      'description': description,
      'claimedAmount': claimedAmount,
      'incidentDate': _formatDateOnly(incidentDate),
      'incidentLocation': incidentLocation,
      'status': status,
      'submittedAt': submittedAt?.toIso8601String(),
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt?.toIso8601String(),
    };
  }
}

DateTime _parseDateOnly(dynamic value) {
  if (value == null) return DateTime.now();
  if (value is DateTime) return DateTime(value.year, value.month, value.day);
  final str = value.toString().trim();
  final datePart = str.contains('T') ? str.split('T')[0] : (str.contains(' ') ? str.split(' ')[0] : str);
  final parts = datePart.split('-');
  if (parts.length == 3) {
    final y = int.tryParse(parts[0]);
    final m = int.tryParse(parts[1]);
    final d = int.tryParse(parts[2]);
    if (y != null && m != null && d != null) {
      return DateTime(y, m, d);
    }
  }
  final parsed = DateTime.parse(str);
  return DateTime(parsed.year, parsed.month, parsed.day);
}

String _formatDateOnly(DateTime date) {
  final y = date.year.toString().padLeft(4, '0');
  final m = date.month.toString().padLeft(2, '0');
  final d = date.day.toString().padLeft(2, '0');
  return '$y-$m-$d';
}
