import '../models/risk_assessment.dart';
import 'api_service.dart';

/// Risk service — Component C (Member 3).
///
/// Only retrieves policyholder-safe review statuses.
/// Does NOT expose internal fraud scores, flags, or detection rules.
class RiskService {
  final ApiService _apiService;

  RiskService({ApiService? apiService})
      : _apiService = apiService ?? ApiService();

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
  /// Returns an empty list on error.
  Future<List<RiskAssessment>> getReviewHistory() async {
    try {
      // This would require a policyholder-specific endpoint
      // For now, returns empty — backend will provide this when auth is integrated
      return [];
    } on ApiException {
      return [];
    }
  }

  void dispose() {
    _apiService.dispose();
  }
}
