/// Claim document model matching the backend ClaimDocumentDto.
class ClaimDocument {
  final String id;
  final String claimId;
  final String fileName;
  final String fileUrl;
  final String documentType;
  final String contentType;
  final int fileSize;
  final DateTime uploadedAt;
  final String verificationStatus;

  ClaimDocument({
    required this.id,
    required this.claimId,
    required this.fileName,
    required this.fileUrl,
    required this.documentType,
    required this.contentType,
    required this.fileSize,
    required this.uploadedAt,
    required this.verificationStatus,
  });

  factory ClaimDocument.fromJson(Map<String, dynamic> json) {
    return ClaimDocument(
      id: json['id'] as String,
      claimId: json['claimId'] as String,
      fileName: json['fileName'] as String,
      fileUrl: json['fileUrl'] as String,
      documentType: json['documentType'] as String,
      contentType: json['contentType'] as String? ?? 'application/octet-stream',
      fileSize: (json['fileSize'] as num?)?.toInt() ?? 0,
      uploadedAt: DateTime.parse(json['uploadedAt'] as String),
      verificationStatus: json['verificationStatus'] as String? ?? 'Pending',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'claimId': claimId,
      'fileName': fileName,
      'fileUrl': fileUrl,
      'documentType': documentType,
      'contentType': contentType,
      'fileSize': fileSize,
      'uploadedAt': uploadedAt.toIso8601String(),
      'verificationStatus': verificationStatus,
    };
  }

  /// Human-readable file size string.
  String get formattedSize {
    if (fileSize < 1024) return '$fileSize B';
    if (fileSize < 1024 * 1024) return '${(fileSize / 1024).toStringAsFixed(1)} KB';
    return '${(fileSize / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}
