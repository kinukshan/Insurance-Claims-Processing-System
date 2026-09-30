import 'package:flutter/foundation.dart';

import '../models/notification_item.dart';
import 'api_service.dart';

/// Notification service — interacts with ASP.NET Core NotificationsController.
///
/// Fetches policyholder-scoped notification history.
/// All API calls go through ASP.NET Core — never directly to external providers.
class NotificationService {
  final ApiService _api;

  NotificationService({ApiService? apiService})
      : _api = apiService ?? ApiService.shared;

  /// Get notifications for the authenticated user.
  ///
  /// GET /api/notifications?page=$page&pageSize=$pageSize
  Future<List<NotificationItem>> getNotifications({
    int page = 1,
    int pageSize = 50,
  }) async {
    try {
      final response = await _api.get(
        '/notifications?page=$page&pageSize=$pageSize',
      );
      if (response != null && response is List) {
        return response
            .map((json) =>
                NotificationItem.fromJson(json as Map<String, dynamic>))
            .toList();
      }
      return [];
    } catch (e) {
      debugPrint('NotificationService.getNotifications error: $e');
      rethrow;
    }
  }

  /// Get notification history for a specific claim.
  ///
  /// GET /api/notifications/claim/{claimId}
  Future<List<NotificationItem>> getClaimNotifications(String claimId) async {
    try {
      final response = await _api.get('/notifications/claim/$claimId');
      if (response != null && response is List) {
        return response
            .map((json) =>
                NotificationItem.fromJson(json as Map<String, dynamic>))
            .toList();
      }
      return [];
    } catch (e) {
      debugPrint('NotificationService.getClaimNotifications error: $e');
      rethrow;
    }
  }

  /// Get notification history for a specific payout.
  ///
  /// GET /api/notifications/payout/{payoutId}
  Future<List<NotificationItem>> getPayoutNotifications(String payoutId) async {
    try {
      final response = await _api.get('/notifications/payout/$payoutId');
      if (response != null && response is List) {
        return response
            .map((json) =>
                NotificationItem.fromJson(json as Map<String, dynamic>))
            .toList();
      }
      return [];
    } catch (e) {
      debugPrint('NotificationService.getPayoutNotifications error: $e');
      rethrow;
    }
  }
}
