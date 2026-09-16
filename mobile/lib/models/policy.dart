/// Policy model with full properties and JSON serialization.
class Policy {
  final String id;
  final String policyNumber;
  final String policyholderId;
  final String policyTypeId;
  final String policyTypeName;
  final double coverageLimit;
  final double premium;
  final double deductible;
  final DateTime startDate;
  final DateTime expiryDate;
  final String status;
  final String renewalStatus;
  final String? exclusions;
  final bool isExpired;
  final bool canRenew;
  final List<PolicyCoverage> coverages;
  final DateTime createdAt;
  final DateTime updatedAt;

  Policy({
    required this.id,
    required this.policyNumber,
    required this.policyholderId,
    required this.policyTypeId,
    required this.policyTypeName,
    required this.coverageLimit,
    required this.premium,
    required this.deductible,
    required this.startDate,
    required this.expiryDate,
    required this.status,
    required this.renewalStatus,
    this.exclusions,
    required this.isExpired,
    required this.canRenew,
    this.coverages = const [],
    required this.createdAt,
    required this.updatedAt,
  });

  factory Policy.fromJson(Map<String, dynamic> json) {
    return Policy(
      id: json['id'] as String,
      policyNumber: json['policyNumber'] as String,
      policyholderId: json['policyholderId'] as String,
      policyTypeId: json['policyTypeId'] as String,
      policyTypeName: json['policyTypeName'] as String? ?? '',
      coverageLimit: (json['coverageLimit'] as num).toDouble(),
      premium: (json['premium'] as num).toDouble(),
      deductible: (json['deductible'] as num).toDouble(),
      startDate: DateTime.parse(json['startDate'] as String),
      expiryDate: DateTime.parse(json['expiryDate'] as String),
      status: json['status'] as String,
      renewalStatus: json['renewalStatus'] as String,
      exclusions: json['exclusions'] as String?,
      isExpired: json['isExpired'] as bool? ?? false,
      canRenew: json['canRenew'] as bool? ?? false,
      coverages: (json['coverages'] as List<dynamic>?)
              ?.map((c) => PolicyCoverage.fromJson(c as Map<String, dynamic>))
              .toList() ??
          [],
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'policyNumber': policyNumber,
      'policyholderId': policyholderId,
      'policyTypeId': policyTypeId,
      'policyTypeName': policyTypeName,
      'coverageLimit': coverageLimit,
      'premium': premium,
      'deductible': deductible,
      'startDate': startDate.toIso8601String(),
      'expiryDate': expiryDate.toIso8601String(),
      'status': status,
      'renewalStatus': renewalStatus,
      'exclusions': exclusions,
      'isExpired': isExpired,
      'canRenew': canRenew,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
    };
  }
}

/// Policy coverage model.
class PolicyCoverage {
  final String id;
  final String policyId;
  final String coverageType;
  final String? description;
  final double coverageLimit;
  final double deductibleAmount;
  final double percentageOfCoverage;
  final bool isActive;

  PolicyCoverage({
    required this.id,
    required this.policyId,
    required this.coverageType,
    this.description,
    required this.coverageLimit,
    required this.deductibleAmount,
    required this.percentageOfCoverage,
    required this.isActive,
  });

  factory PolicyCoverage.fromJson(Map<String, dynamic> json) {
    return PolicyCoverage(
      id: json['id'] as String,
      policyId: json['policyId'] as String,
      coverageType: json['coverageType'] as String,
      description: json['description'] as String?,
      coverageLimit: (json['coverageLimit'] as num).toDouble(),
      deductibleAmount: (json['deductibleAmount'] as num).toDouble(),
      percentageOfCoverage: (json['percentageOfCoverage'] as num).toDouble(),
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'policyId': policyId,
      'coverageType': coverageType,
      'description': description,
      'coverageLimit': coverageLimit,
      'deductibleAmount': deductibleAmount,
      'percentageOfCoverage': percentageOfCoverage,
      'isActive': isActive,
    };
  }
}
