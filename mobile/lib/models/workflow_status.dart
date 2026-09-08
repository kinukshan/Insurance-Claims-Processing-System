/// Workflow status model for displaying AI processing status.
class WorkflowStatus {
  final String id;
  final String claimId;
  final String status;
  final String? currentStep;

  WorkflowStatus({
    required this.id,
    required this.claimId,
    required this.status,
    this.currentStep,
  });

  // TODO: Add fromJson, toJson
}
