import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/models/notification_item.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/notification_service.dart';
import 'package:insurance_claims_mobile/screens/notifications/notifications_screen.dart';
import 'package:insurance_claims_mobile/widgets/shared_widgets.dart';

void main() {
  final sampleNotificationAccepted = {
    'id': '11111111-1111-1111-1111-111111111111',
    'userId': '22222222-2222-2222-2222-222222222222',
    'claimId': '33333333-3333-3333-3333-333333333333',
    'payoutId': null,
    'notificationType': 'ClaimSubmitted',
    'channel': 'Email',
    'recipient': 'policyholder@test.com',
    'subject': 'Claim CLM-2026-001 Received',
    'success': true,
    'errorMessage': null,
    'provider': 'Resend',
    'providerMessageId': 'msg_resend_123',
    'sentAt': '2026-09-26T14:30:00Z',
    'createdAt': '2026-09-26T14:30:00Z',
    'notificationKey': 'claim:33333333:submitted',
    'status': 'Accepted',
  };

  final sampleNotificationDelivered = {
    'id': '44444444-4444-4444-4444-444444444444',
    'userId': '22222222-2222-2222-2222-222222222222',
    'claimId': null,
    'payoutId': '55555555-5555-5555-5555-555555555555',
    'notificationType': 'PayoutApproved',
    'channel': 'Email',
    'recipient': 'policyholder@test.com',
    'subject': 'Payout Approved for CLM-2026-001',
    'success': true,
    'errorMessage': null,
    'provider': 'Mock',
    'providerMessageId': 'mock_msg_456',
    'sentAt': '2026-09-26T15:00:00Z',
    'createdAt': '2026-09-26T15:00:00Z',
    'notificationKey': 'payout:55555555:approved',
    'status': 'Sent',
  };

  group('NotificationItem Model Tests', () {
    test('deserializes Accepted notification correctly', () {
      final item = NotificationItem.fromJson(sampleNotificationAccepted);

      expect(item.id, '11111111-1111-1111-1111-111111111111');
      expect(item.isAccepted, isTrue);
      expect(item.isDelivered, isFalse);
      expect(item.isFailed, isFalse);
      expect(item.statusLabel, 'Accepted by Provider');
      expect(item.provider, 'Resend');
      expect(item.claimId, '33333333-3333-3333-3333-333333333333');
    });

    test('deserializes Delivered (Sent) notification correctly', () {
      final item = NotificationItem.fromJson(sampleNotificationDelivered);

      expect(item.id, '44444444-4444-4444-4444-444444444444');
      expect(item.isDelivered, isTrue);
      expect(item.isAccepted, isFalse);
      expect(item.isFailed, isFalse);
      expect(item.statusLabel, 'Delivered');
      expect(item.payoutId, '55555555-5555-5555-5555-555555555555');
    });

    test('toJson produces expected structure', () {
      final item = NotificationItem.fromJson(sampleNotificationAccepted);
      final json = item.toJson();

      expect(json['id'], item.id);
      expect(json['status'], 'Accepted');
      expect(json['provider'], 'Resend');
      expect(json['recipient'], 'policyholder@test.com');
    });
  });

  group('NotificationService Tests', () {
    test('getNotifications calls /notifications and parses response', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/notifications'));
        expect(request.method, 'GET');
        return http.Response(
          jsonEncode([sampleNotificationAccepted, sampleNotificationDelivered]),
          200,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = NotificationService(apiService: api);

      final items = await service.getNotifications();
      expect(items.length, 2);
      expect(items[0].isAccepted, isTrue);
      expect(items[1].isDelivered, isTrue);
    });

    test('getClaimNotifications calls /notifications/claim/{id}', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, endsWith('/notifications/claim/test-claim-id'));
        return http.Response(jsonEncode([sampleNotificationAccepted]), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = NotificationService(apiService: api);

      final items = await service.getClaimNotifications('test-claim-id');
      expect(items.length, 1);
      expect(items.first.subject, 'Claim CLM-2026-001 Received');
    });
  });

  group('NotificationsScreen Widget Tests', () {
    testWidgets('renders notifications list with distinct delivery badges', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode([sampleNotificationAccepted, sampleNotificationDelivered]),
          200,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = NotificationService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: NotificationsScreen(notificationService: service),
        ),
      );

      // Loading state initially
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      await tester.pumpAndSettle();

      // Should show app bar title
      expect(find.text('Notifications'), findsOneWidget);

      // Should show subjects
      expect(find.text('Claim CLM-2026-001 Received'), findsOneWidget);
      expect(find.text('Payout Approved for CLM-2026-001'), findsOneWidget);

      // Should show delivery status badges
      expect(find.text('Accepted by Provider'), findsOneWidget);
      expect(find.text('Delivered'), findsOneWidget);

      // Verify StatusBadge widgets are present
      expect(find.byType(StatusBadge), findsNWidgets(2));
    });

    testWidgets('renders empty state when no notifications', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response('[]', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = NotificationService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: NotificationsScreen(notificationService: service),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.byType(EmptyStateView), findsOneWidget);
      expect(find.text('No Notifications Yet'), findsOneWidget);
    });

    testWidgets('renders error retry view on failure', (tester) async {
      final mockClient = MockClient((request) async {
        return http.Response('Server Error', 500);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final service = NotificationService(apiService: api);

      await tester.pumpWidget(
        MaterialApp(
          home: NotificationsScreen(notificationService: service),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.byType(ErrorRetryView), findsOneWidget);
      expect(find.text('Try Again'), findsOneWidget);
    });
  });
}
