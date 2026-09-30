import '../models/risk_assessment.dart';
import '../models/ai_workflow_models.dart';
import 'api_service.dart';

/// Risk service — Component C (Member 3).
///
/// Supports both:
/// - Policyholder-safe review status (no confidential scores or flags)
/// - Authorized staff operations (full risk scores, flags, AI analysis, escalation)
class RiskService {
  final ApiService _apiService;

  RiskService({ApiService? apiService})
      : _apiService = apiService ?? ApiService.shared;

  /// Get the policyholder-safe review status for a specific claim.
  ///
  /// Returns a [RiskAssessment] with only safe fields:
  /// - "Additional Review Required"
  /// - "Under Manual Review"
  /// - "Review Completed"
  Future<RiskAssessment?> getReviewStatus(String claimId) async {
    try {
      final data = await _apiService.get('/riskassessments/$claimId/status');
      if (data == null) return null;
      return RiskAssessment.fromJson(data as Map<String, dynamic>);
    } on ApiException {
      return null;
    }
  }

  /// Get review statuses for all claims belonging to the current policyholder.
  Future<List<RiskAssessment>> getReviewHistory() async {
    try {
      return [];
    } on ApiException {
      return [];
    }
  }

  // ── Staff Operations (ClaimsAdjuster, Underwriter, Admin) ───────────────

  /// Trigger a risk assessment on a claim (AI Workflow 3).
  ///
  /// POST /api/riskassessments/{claimId}/assess
  Future<StaffRiskAssessment> assessClaim(
    String claimId, {
    bool includeAiAnalysis = true,
    String? notes,
  }) async {
    final body = <String, dynamic>{
      'includeAiAnalysis': includeAiAnalysis,
    };
    if (notes != null && notes.isNotEmpty) {
      body['notes'] = notes;
    }

    final data = await _apiService.post(
      '/riskassessments/$claimId/assess',
      body: body,
    );
    return StaffRiskAssessment.fromJson(data as Map<String, dynamic>);
  }

  /// Get the latest risk assessment for a specific claim.
  ///
  /// GET /api/riskassessments/{claimId}
  Future<StaffRiskAssessment?> getAssessment(String claimId) async {
    try {
      final data = await _apiService.get('/riskassessments/$claimId');
      if (data == null) return null;
      return StaffRiskAssessment.fromJson(data as Map<String, dynamic>);
    } on ApiException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  /// Get all risk assessments across all claims.
  ///
  /// GET /api/riskassessments
  Future<List<StaffRiskAssessment>> getAllAssessments() async {
    final data = await _apiService.get('/riskassessments');
    return (data as List<dynamic>)
        .map((a) => StaffRiskAssessment.fromJson(a as Map<String, dynamic>))
        .toList();
  }

  /// Get all claims that have unresolved fraud flags.
  ///
  /// GET /api/riskassessments/flagged
  Future<List<StaffRiskAssessment>> getFlaggedClaims() async {
    final data = await _apiService.get('/riskassessments/flagged');
    return (data as List<dynamic>)
        .map((a) => StaffRiskAssessment.fromJson(a as Map<String, dynamic>))
        .toList();
  }

  /// Escalate a risk assessment to a fraud case.
  ///
  /// POST /api/riskassessments/{id}/escalate
  Future<Map<String, dynamic>> escalateClaim(
    String assessmentId, {
    required String reason,
    required String priority,
    String? assignedReviewer,
  }) async {
    final body = <String, dynamic>{
      'reason': reason,
      'priority': priority,
    };
    if (assignedReviewer != null && assignedReviewer.isNotEmpty) {
      body['assignedReviewer'] = assignedReviewer;
    }

    final data = await _apiService.post(
      '/riskassessments/$assessmentId/escalate',
      body: body,
    );
    return data as Map<String, dynamic>;
  }

  void dispose() {
    _apiService.dispose();
  }
}
