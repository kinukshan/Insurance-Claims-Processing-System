/// Claim document model for evidence attachments.
class ClaimDocument {
  final String id;
  final String claimId;
  final String fileName;
  final String fileUrl;
  final String documentType;

  ClaimDocument({
    required this.id,
    required this.claimId,
    required this.fileName,
    required this.fileUrl,
    required this.documentType,
  });

  // TODO: Add fromJson, toJson
}
