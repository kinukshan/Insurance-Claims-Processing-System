/// Model matching the backend ClaimDocumentRequirementItemDto.
class DocumentRequirementItem {
  final String type;
  final bool required;
  final bool uploaded;

  const DocumentRequirementItem({
    required this.type,
    required this.required,
    required this.uploaded,
  });

  factory DocumentRequirementItem.fromJson(Map<String, dynamic> json) {
    return DocumentRequirementItem(
      type: json['type'] as String,
      required: json['required'] as bool? ?? true,
      uploaded: json['uploaded'] as bool? ?? false,
    );
  }
}

/// Model matching the backend ClaimDocumentRequirementsDto.
class DocumentRequirements {
  final String claimId;
  final String claimType;
  final List<DocumentRequirementItem> requiredDocuments;
  final int requiredCount;
  final int uploadedRequiredCount;
  final int missingCount;
  final bool complete;

  const DocumentRequirements({
    required this.claimId,
    required this.claimType,
    required this.requiredDocuments,
    required this.requiredCount,
    required this.uploadedRequiredCount,
    required this.missingCount,
    required this.complete,
  });

  factory DocumentRequirements.fromJson(Map<String, dynamic> json) {
    return DocumentRequirements(
      claimId: json['claimId'] as String,
      claimType: json['claimType'] as String,
      requiredDocuments: (json['requiredDocuments'] as List<dynamic>?)
              ?.map((d) =>
                  DocumentRequirementItem.fromJson(d as Map<String, dynamic>))
              .toList() ??
          [],
      requiredCount: (json['requiredCount'] as num?)?.toInt() ?? 0,
      uploadedRequiredCount:
          (json['uploadedRequiredCount'] as num?)?.toInt() ?? 0,
      missingCount: (json['missingCount'] as num?)?.toInt() ?? 0,
      complete: json['complete'] as bool? ?? false,
    );
  }

  /// Return list of required document types that have not been uploaded.
  List<String> get missingDocumentTypes => requiredDocuments
      .where((d) => d.required && !d.uploaded)
      .map((d) => d.type)
      .toList();
}
