import 'package:flutter/foundation.dart';
import 'api_service.dart';
import '../models/payout.dart';

/// Payout service — Component D (Kinukshan).
/// Policyholder-facing: read-only access to payout status and history.
/// All API calls go through ASP.NET Core — never directly to the AI service.
class PayoutService {
  final ApiService _api = ApiService();

  /// Get payout for a specific claim.
  Future<Payout?> getPayoutByClaimId(String claimId) async {
    try {
      final response = await _api.get('/payouts/claim/$claimId');
      if (response != null) {
        return Payout.fromJson(response as Map<String, dynamic>);
      }
      return null;
    } catch (e) {
      debugPrint('PayoutService.getPayoutByClaimId error: $e');
      rethrow;
    }
  }

  /// Get payout by its ID.
  Future<Payout?> getPayoutById(String id) async {
    try {
      final response = await _api.get('/payouts/$id');
      if (response != null) {
        return Payout.fromJson(response as Map<String, dynamic>);
      }
      return null;
    } catch (e) {
      debugPrint('PayoutService.getPayoutById error: $e');
      rethrow;
    }
  }

  /// Get paginated payout history.
  Future<List<Payout>> getPayoutHistory({int page = 1, int pageSize = 20}) async {
    try {
      final response = await _api.get(
        '/payouts/history?page=$page&pageSize=$pageSize',
      );
      if (response != null && response['items'] != null) {
        return (response['items'] as List)
            .map((json) => Payout.fromJson(json as Map<String, dynamic>))
            .toList();
      }
      return [];
    } catch (e) {
      debugPrint('PayoutService.getPayoutHistory error: $e');
      rethrow;
    }
  }
}
