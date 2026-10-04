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
  final double? deductiblePercentage;
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
    this.deductiblePercentage,
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

  String get formattedDeductible {
    if (deductiblePercentage != null) {
      final isInt = deductiblePercentage! % 1 == 0;
      return '${deductiblePercentage!.toStringAsFixed(isInt ? 0 : 2)}% deductible';
    }
    return 'LKR ${deductible.toStringAsFixed(2)}';
  }

  factory Policy.fromJson(Map<String, dynamic> json) {
    return Policy(
      id: json['id'] as String,
      policyNumber: json['policyNumber'] as String,
      policyholderId: (json['policyholderId'] ?? json['policyHolderId']) as String? ?? '',
      policyTypeId: json['policyTypeId'] as String? ?? '',
      policyTypeName: json['policyTypeName'] as String? ?? '',
      coverageLimit: (json['coverageLimit'] as num?)?.toDouble() ?? 0.0,
      premium: (json['premium'] as num?)?.toDouble() ?? 0.0,
      deductible: (json['deductible'] as num?)?.toDouble() ?? 0.0,
      deductiblePercentage: (json['deductiblePercentage'] as num?)?.toDouble(),
      startDate: _parseDateOnly(json['startDate']),
      expiryDate: _parseDateOnly(json['expiryDate']),
      status: json['status'] as String? ?? 'Draft',
      renewalStatus: json['renewalStatus'] as String? ?? 'None',
      exclusions: json['exclusions'] as String?,
      isExpired: json['isExpired'] as bool? ?? false,
      canRenew: json['canRenew'] as bool? ?? false,
      coverages: (json['coverages'] as List<dynamic>?)
              ?.map((c) => PolicyCoverage.fromJson(c as Map<String, dynamic>))
              .toList() ??
          [],
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
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
      'deductiblePercentage': deductiblePercentage,
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

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is Policy && runtimeType == other.runtimeType && id == other.id;

  @override
  int get hashCode => id.hashCode;
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

/// Policy type reference model from GET /api/policytypes.
class PolicyType {
  final String id;
  final String name;
  final String description;
  final double defaultCoverageLimit;
  final double defaultDeductible;
  final double? defaultDeductiblePercentage;
  final int insuranceClass;
  final String insuranceClassCode;
  final String insuranceClassName;

  const PolicyType({
    required this.id,
    required this.name,
    required this.description,
    required this.defaultCoverageLimit,
    required this.defaultDeductible,
    this.defaultDeductiblePercentage,
    required this.insuranceClass,
    required this.insuranceClassCode,
    required this.insuranceClassName,
  });

  factory PolicyType.fromJson(Map<String, dynamic> json) {
    return PolicyType(
      id: json['id'] as String? ?? '',
      name: json['name'] as String? ?? '',
      description: json['description'] as String? ?? '',
      defaultCoverageLimit:
          (json['defaultCoverageLimit'] as num?)?.toDouble() ?? 0.0,
      defaultDeductible:
          (json['defaultDeductible'] as num?)?.toDouble() ?? 0.0,
      defaultDeductiblePercentage:
          (json['defaultDeductiblePercentage'] as num?)?.toDouble(),
      insuranceClass: json['insuranceClass'] as int? ?? 0,
      insuranceClassCode: json['insuranceClassCode'] as String? ?? 'General',
      insuranceClassName:
          json['insuranceClassName'] as String? ?? 'General Insurance',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'description': description,
      'defaultCoverageLimit': defaultCoverageLimit,
      'defaultDeductible': defaultDeductible,
      'defaultDeductiblePercentage': defaultDeductiblePercentage,
      'insuranceClass': insuranceClass,
      'insuranceClassCode': insuranceClassCode,
      'insuranceClassName': insuranceClassName,
    };
  }

  /// Canonical percentage deductible matching backend PolicyClaimCompatibility.
  double get deductiblePercentage {
    if (defaultDeductiblePercentage != null) return defaultDeductiblePercentage!;
    final lower = name.toLowerCase().trim();
    if (lower.contains('motor') || lower.contains('auto')) return 5.0;
    if (lower.contains('health')) return 10.0;
    if (lower.contains('home') || lower.contains('property')) return 10.0;
    if (lower.contains('life')) return 0.0;
    return 0.0;
  }

  /// Alias for deductible percentage.
  double get effectiveDeductiblePercentage => deductiblePercentage;

  /// Canonical fixed deductible matching backend PolicyClaimCompatibility.
  double get fixedDeductible {
    final lower = name.toLowerCase().trim();
    if (lower.contains('motor') || lower.contains('auto')) return 10000.0;
    if (lower.contains('health')) return 5000.0;
    if (lower.contains('home') || lower.contains('property')) return 15000.0;
    if (lower.contains('life')) return 0.0;
    return defaultDeductible;
  }

  /// Whether this is Life Insurance (deductible is fixed at $0 / 0%).
  bool get isLife => name.toLowerCase().contains('life');
}

/// Request DTO for creating a new policy matching backend CreatePolicyDto.
class CreatePolicyRequest {
  final String policyholderId;
  final String policyTypeId;
  final double coverageLimit;
  final double deductible;
  final double? deductiblePercentage;
  final DateTime startDate;
  final DateTime expiryDate;
  final String? exclusions;

  CreatePolicyRequest({
    required this.policyholderId,
    required this.policyTypeId,
    required this.coverageLimit,
    required this.deductible,
    this.deductiblePercentage,
    required this.startDate,
    required this.expiryDate,
    this.exclusions,
  });

  Map<String, dynamic> toJson() {
    return {
      if (policyholderId.isNotEmpty)
        'policyholderId': policyholderId,
      'policyTypeId': policyTypeId,
      'coverageLimit': coverageLimit,
      'deductible': deductible,
      if (deductiblePercentage != null)
        'deductiblePercentage': deductiblePercentage,
      'startDate': _formatDateOnly(startDate),
      'expiryDate': _formatDateOnly(expiryDate),
      if (exclusions != null && exclusions!.trim().isNotEmpty)
        'exclusions': exclusions!.trim(),
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
