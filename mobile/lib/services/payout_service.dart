import 'package:flutter/foundation.dart';
import 'api_service.dart';
import '../models/payout.dart';
import '../models/ai_workflow_models.dart';

/// Payout service — Component D (Kinukshan).
///
/// Supports both:
/// - Policyholder-facing: read-only access to payout status and history.
/// - Staff-facing: calculate proposals, approve/reject/request revision, execute payments.
/// All API calls go through ASP.NET Core — never directly to the AI service.
class PayoutService {
  final ApiService _api;

  PayoutService({ApiService? apiService})
      : _api = apiService ?? ApiService.shared;

  /// Get payout for a specific claim.
  ///
  /// GET /api/payouts/claim/{claimId}
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
  ///
  /// GET /api/payouts/{id}
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

  /// Get paginated payout history for the authenticated Policyholder.
  ///
  /// GET /api/payouts/my?page=$page&pageSize=$pageSize
  Future<List<Payout>> getMyPayouts({
    int page = 1,
    int pageSize = 20,
    String? status,
  }) async {
    try {
      final queryParams = StringBuffer('page=$page&pageSize=$pageSize');
      if (status != null && status.isNotEmpty) {
        queryParams.write('&status=$status');
      }
      final response = await _api.get('/payouts/my?$queryParams');
      if (response != null) {
        if (response is Map && response['items'] != null) {
          return (response['items'] as List)
              .map((json) => Payout.fromJson(json as Map<String, dynamic>))
              .toList();
        } else if (response is List) {
          return response
              .map((json) => Payout.fromJson(json as Map<String, dynamic>))
              .toList();
        }
      }
      return [];
    } catch (e) {
      debugPrint('PayoutService.getMyPayouts error: $e');
      rethrow;
    }
  }

  /// Get paginated payout history (routes to policyholder-safe /api/payouts/my).
  Future<List<Payout>> getPayoutHistory({int page = 1, int pageSize = 20}) async {
    return getMyPayouts(page: page, pageSize: pageSize);
  }

  // ── Staff Operations (ClaimsAdjuster, Underwriter, Admin) ───────────────

  /// Calculate and create a payout proposal for a claim (AI Workflow 4: Validation / Safety Agent).
  ///
  /// POST /api/payouts/calculate/{claimId}
  /// Authorized: ClaimsAdjuster, Underwriter, Admin.
  Future<Payout> calculatePayout(String claimId) async {
    final response = await _api.post('/payouts/calculate/$claimId');
    return Payout.fromJson(response as Map<String, dynamic>);
  }

  /// Get paginated payout history for staff across all claims.
  ///
  /// GET /api/payouts/history?page=$page&pageSize=$pageSize&status=$status
  Future<List<Payout>> getAllPayouts({
    int page = 1,
    int pageSize = 50,
    String? status,
  }) async {
    final queryParams = StringBuffer('page=$page&pageSize=$pageSize');
    if (status != null && status.isNotEmpty) {
      queryParams.write('&statusFilter=$status');
    }
    final response = await _api.get('/payouts/history?$queryParams');
    if (response != null) {
      if (response is Map && response['items'] != null) {
        return (response['items'] as List)
            .map((json) => Payout.fromJson(json as Map<String, dynamic>))
            .toList();
      } else if (response is List) {
        return response
            .map((json) => Payout.fromJson(json as Map<String, dynamic>))
            .toList();
      }
    }
    return [];
  }

  /// Approve a payout proposal.
  ///
  /// POST /api/payouts/{id}/approve
  /// Authorized: Underwriter, Admin (ClaimsAdjuster cannot approve).
  Future<Payout> approvePayout(String payoutId, {String comments = ''}) async {
    final response = await _api.post(
      '/payouts/$payoutId/approve',
      body: {'comments': comments},
    );
    return Payout.fromJson(response as Map<String, dynamic>);
  }

  /// Reject a payout proposal.
  ///
  /// POST /api/payouts/{id}/reject
  /// Authorized: Underwriter, Admin.
  Future<Payout> rejectPayout(String payoutId, {String comments = ''}) async {
    final response = await _api.post(
      '/payouts/$payoutId/reject',
      body: {'comments': comments},
    );
    return Payout.fromJson(response as Map<String, dynamic>);
  }

  /// Request revision on a payout proposal.
  ///
  /// POST /api/payouts/{id}/request-revision
  /// Authorized: Underwriter, Admin.
  Future<Payout> requestRevision(String payoutId, {String comments = ''}) async {
    final response = await _api.post(
      '/payouts/$payoutId/request-revision',
      body: {'comments': comments},
    );
    return Payout.fromJson(response as Map<String, dynamic>);
  }

  /// Execute an approved payout via payment gateway (Mock or PayPal Sandbox).
  ///
  /// POST /api/payouts/{id}/execute
  /// Authorized: Admin alone.
  Future<PaymentExecutionResult> executePayout(String payoutId, {double fallbackAmount = 0.0}) async {
    final response = await _api.post('/payouts/$payoutId/execute');
    return PaymentExecutionResult.fromJson(
      response as Map<String, dynamic>,
      fallbackAmount: fallbackAmount,
    );
  }

  /// Get payment transactions for a payout.
  ///
  /// GET /api/payouts/{payoutId}/payments
  Future<List<Map<String, dynamic>>> getPaymentTransactions(String payoutId) async {
    final response = await _api.get('/payouts/$payoutId/payments');
    if (response is List) {
      return response.cast<Map<String, dynamic>>();
    }
    return [];
  }
}
