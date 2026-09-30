/// Models for AI-assisted workflows and staff operations.
///
/// Communicates exclusively through ASP.NET Core Web API.
/// Never calls Gemini or Python AI service directly.
library;

class CoverageValidationResult {
  final bool isValid;
  final bool isCovered;
  final double? coverageLimit;
  final double? deductibleAmount;
  final String? coverageType;
  final List<String> issues;

  CoverageValidationResult({
    required this.isValid,
    required this.isCovered,
    this.coverageLimit,
    this.deductibleAmount,
    this.coverageType,
    required this.issues,
  });

  factory CoverageValidationResult.fromJson(Map<String, dynamic> json) {
    return CoverageValidationResult(
      isValid: json['isValid'] as bool? ?? false,
      isCovered: json['isCovered'] as bool? ?? false,
      coverageLimit: (json['coverageLimit'] as num?)?.toDouble(),
      deductibleAmount: (json['deductibleAmount'] as num?)?.toDouble(),
      coverageType: json['coverageType'] as String?,
      issues: (json['issues'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
    );
  }
}

class DocumentInconsistency {
  final String field;
  final String description;
  final String severity;

  DocumentInconsistency({
    required this.field,
    required this.description,
    required this.severity,
  });

  factory DocumentInconsistency.fromJson(Map<String, dynamic> json) {
    String parseSeverity(dynamic raw, dynamic display) {
      if (display != null && display.toString().trim().isNotEmpty) {
        return display.toString().trim();
      }
      if (raw is int) {
        switch (raw) {
          case 0:
            return 'Low';
          case 1:
            return 'Medium';
          case 2:
            return 'High';
          default:
            return 'Medium';
        }
      }
      return raw?.toString() ?? 'Medium';
    }

    return DocumentInconsistency(
      field: json['field']?.toString() ?? '',
      description: json['description']?.toString() ?? '',
      severity: parseSeverity(json['severity'], json['severityDisplay']),
    );
  }
}

class DocumentVerificationResult {
  final bool complete;
  final List<String> missingItems;
  final List<DocumentInconsistency> inconsistencies;
  final List<String> warnings;
  final bool aiUsed;
  final String? aiProvider;
  final String? aiModel;
  final String? reasoningSummary;
  final bool fallbackUsed;
  final String? attemptId;

  DocumentVerificationResult({
    required this.complete,
    required this.missingItems,
    required this.inconsistencies,
    required this.warnings,
    this.aiUsed = false,
    this.aiProvider,
    this.aiModel,
    this.reasoningSummary,
    this.fallbackUsed = false,
    this.attemptId,
  });

  factory DocumentVerificationResult.fromJson(Map<String, dynamic> json) {
    return DocumentVerificationResult(
      complete: json['complete'] as bool? ?? false,
      missingItems: (json['missingItems'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
      inconsistencies: (json['inconsistencies'] as List<dynamic>?)
              ?.map((e) =>
                  DocumentInconsistency.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      warnings: (json['warnings'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
      aiUsed: json['aiUsed'] as bool? ?? false,
      aiProvider: json['aiProvider']?.toString(),
      aiModel: json['aiModel']?.toString(),
      reasoningSummary: json['reasoningSummary']?.toString(),
      fallbackUsed: json['fallbackUsed'] as bool? ?? false,
      attemptId: json['attemptId']?.toString(),
    );
  }
}

class FraudFlag {
  final String code;
  final String description;
  final String severity;

  FraudFlag({
    required this.code,
    required this.description,
    required this.severity,
  });

  factory FraudFlag.fromJson(Map<String, dynamic> json) {
    String parseSeverity(dynamic raw, dynamic display) {
      if (display != null && display.toString().trim().isNotEmpty) {
        return display.toString().trim();
      }
      if (raw is int) {
        switch (raw) {
          case 0:
            return 'Low';
          case 1:
            return 'Medium';
          case 2:
            return 'High';
          case 3:
            return 'Critical';
          default:
            return 'Medium';
        }
      }
      return raw?.toString() ?? 'Medium';
    }

    return FraudFlag(
      code: json['flagTypeDisplay']?.toString() ??
          json['flagType']?.toString() ??
          json['code']?.toString() ??
          '',
      description: json['description']?.toString() ?? '',
      severity: parseSeverity(json['severity'], json['severityDisplay']),
    );
  }
}

class StaffRiskAssessment {
  final String id;
  final String claimId;
  final String? claimNumber;
  final double riskScore;
  final String riskLevel;
  final String recommendation;
  final String summary;
  final int fraudFlagCount;
  final bool hasFraudCase;
  final bool aiUsed;
  final String? aiProvider;
  final String? aiModel;
  final String? reasoningSummary;
  final bool fallbackUsed;
  final List<FraudFlag> flags;
  final DateTime assessmentTimestamp;

  StaffRiskAssessment({
    required this.id,
    required this.claimId,
    this.claimNumber,
    required this.riskScore,
    required this.riskLevel,
    required this.recommendation,
    required this.summary,
    required this.fraudFlagCount,
    required this.hasFraudCase,
    required this.aiUsed,
    this.aiProvider,
    this.aiModel,
    this.reasoningSummary,
    required this.fallbackUsed,
    required this.flags,
    required this.assessmentTimestamp,
  });

  factory StaffRiskAssessment.fromJson(Map<String, dynamic> json) {
    String parseRiskLevel(dynamic raw, dynamic display) {
      if (display != null && display.toString().trim().isNotEmpty) {
        return display.toString().trim();
      }
      if (raw is int) {
        switch (raw) {
          case 0:
            return 'Low';
          case 1:
            return 'Medium';
          case 2:
            return 'High';
          case 3:
            return 'Critical';
          default:
            return 'Low';
        }
      }
      return raw?.toString() ?? 'Low';
    }

    String parseRecommendation(dynamic raw, dynamic display) {
      if (display != null && display.toString().trim().isNotEmpty) {
        return display.toString().trim();
      }
      if (raw is int) {
        switch (raw) {
          case 0:
            return 'Proceed';
          case 1:
            return 'FurtherInvestigation';
          case 2:
            return 'Reject';
          default:
            return 'Proceed';
        }
      }
      return raw?.toString() ?? 'Proceed';
    }

    return StaffRiskAssessment(
      id: json['id']?.toString() ?? '',
      claimId: json['claimId']?.toString() ?? '',
      claimNumber: json['claimNumber']?.toString(),
      riskScore: (json['riskScore'] as num?)?.toDouble() ?? 0.0,
      riskLevel: parseRiskLevel(json['riskLevel'], json['riskLevelDisplay']),
      recommendation: parseRecommendation(json['recommendation'], json['recommendationDisplay']),
      summary: json['summary']?.toString() ?? '',
      fraudFlagCount: (json['fraudFlagCount'] as num?)?.toInt() ?? 0,
      hasFraudCase: json['hasFraudCase'] as bool? ?? false,
      aiUsed: json['aiUsed'] as bool? ?? false,
      aiProvider: json['aiProvider']?.toString(),
      aiModel: json['aiModel']?.toString(),
      reasoningSummary: json['reasoningSummary']?.toString(),
      fallbackUsed: json['fallbackUsed'] as bool? ?? false,
      flags: (json['flags'] as List<dynamic>?)
              ?.map((e) => FraudFlag.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      assessmentTimestamp: json['assessmentTimestamp'] != null
          ? DateTime.tryParse(json['assessmentTimestamp'].toString()) ??
              DateTime.now()
          : DateTime.now(),
    );
  }
}

class PayoutValidationResult {
  final bool valid;
  final List<String> violations;
  final bool requiresHumanApproval;
  final String agentId;
  final String summary;
  final bool aiUsed;
  final String? aiProvider;
  final String? aiModel;
  final String? reasoningSummary;
  final bool fallbackUsed;

  PayoutValidationResult({
    required this.valid,
    required this.violations,
    required this.requiresHumanApproval,
    required this.agentId,
    required this.summary,
    this.aiUsed = false,
    this.aiProvider,
    this.aiModel,
    this.reasoningSummary,
    this.fallbackUsed = false,
  });

  factory PayoutValidationResult.fromJson(Map<String, dynamic> json) {
    return PayoutValidationResult(
      valid: json['valid'] as bool? ?? false,
      violations: (json['violations'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
      requiresHumanApproval: json['requiresHumanApproval'] as bool? ?? true,
      agentId: json['agentId']?.toString() ?? '',
      summary: json['summary']?.toString() ?? '',
      aiUsed: json['aiUsed'] as bool? ?? false,
      aiProvider: json['aiProvider']?.toString(),
      aiModel: json['aiModel']?.toString(),
      reasoningSummary: json['reasoningSummary']?.toString(),
      fallbackUsed: json['fallbackUsed'] as bool? ?? false,
    );
  }
}

class PaymentExecutionResult {
  final bool success;
  final String status;
  final String? transactionId;
  final String? provider;
  final double amount;
  final String? message;
  final String? errorMessage;

  PaymentExecutionResult({
    required this.success,
    required this.status,
    this.transactionId,
    this.provider,
    required this.amount,
    this.message,
    this.errorMessage,
  });

  factory PaymentExecutionResult.fromJson(Map<String, dynamic> json, {double fallbackAmount = 0.0}) {
    final status = json['status']?.toString() ?? '';
    final statusLower = status.toLowerCase();
    final bool isSuccess = json['success'] as bool? ??
        (statusLower == 'succeeded' ||
            statusLower == 'processing' ||
            statusLower == 'paid' ||
            statusLower == 'created');

    return PaymentExecutionResult(
      success: isSuccess,
      status: status.isNotEmpty ? status : (isSuccess ? 'Succeeded' : 'Failed'),
      transactionId: (json['transactionId'] ?? json['providerTransactionId'])?.toString(),
      provider: json['provider']?.toString(),
      amount: (json['amount'] as num?)?.toDouble() ?? fallbackAmount,
      message: json['message']?.toString(),
      errorMessage: json['errorMessage']?.toString() ??
          (isSuccess ? null : (json['error']?.toString() ?? json['message']?.toString())),
    );
  }
}
