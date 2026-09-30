/// Model representing a notification log item from the backend.
///
/// Matches backend `NotificationLogDto` from `NotificationsController`.
class NotificationItem {
  final String id;
  final String userId;
  final String? claimId;
  final String? payoutId;
  final String notificationType;
  final String channel;
  final String recipient;
  final String subject;
  final bool success;
  final String? errorMessage;
  final String provider;
  final String? providerMessageId;
  final DateTime? sentAt;
  final DateTime createdAt;
  final String? notificationKey;
  final String status;

  const NotificationItem({
    required this.id,
    required this.userId,
    this.claimId,
    this.payoutId,
    required this.notificationType,
    required this.channel,
    required this.recipient,
    required this.subject,
    required this.success,
    this.errorMessage,
    required this.provider,
    this.providerMessageId,
    this.sentAt,
    required this.createdAt,
    this.notificationKey,
    required this.status,
  });

  factory NotificationItem.fromJson(Map<String, dynamic> json) {
    return NotificationItem(
      id: json['id'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
      claimId: json['claimId'] as String?,
      payoutId: json['payoutId'] as String?,
      notificationType: json['notificationType'] as String? ?? 'General',
      channel: json['channel'] as String? ?? 'Email',
      recipient: json['recipient'] as String? ?? '',
      subject: json['subject'] as String? ?? 'Notification',
      success: json['success'] as bool? ?? false,
      errorMessage: json['errorMessage'] as String?,
      provider: json['provider'] as String? ?? '',
      providerMessageId: json['providerMessageId'] as String?,
      sentAt: json['sentAt'] != null
          ? DateTime.tryParse(json['sentAt'] as String)
          : null,
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      notificationKey: json['notificationKey'] as String?,
      status: json['status'] as String? ??
          (json['success'] == true ? 'Sent' : 'Failed'),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'userId': userId,
      'claimId': claimId,
      'payoutId': payoutId,
      'notificationType': notificationType,
      'channel': channel,
      'recipient': recipient,
      'subject': subject,
      'success': success,
      'errorMessage': errorMessage,
      'provider': provider,
      'providerMessageId': providerMessageId,
      'sentAt': sentAt?.toIso8601String(),
      'createdAt': createdAt.toIso8601String(),
      'notificationKey': notificationKey,
      'status': status,
    };
  }

  /// Whether the delivery was confirmed delivered by provider.
  bool get isDelivered => status == 'Sent';

  /// Whether provider (e.g. Resend) accepted the message but delivery is in-flight.
  bool get isAccepted => status == 'Accepted';

  /// Whether notification attempt failed.
  bool get isFailed => status == 'Failed' || (!success && status != 'Accepted');

  /// Whether notification is processing / in-flight.
  bool get isProcessing => status == 'Processing';

  /// Whether notification was handled by mock provider.
  bool get isMock => provider.toLowerCase().contains('mock');

  /// Human-readable label for delivery status.
  String get statusLabel {
    switch (status) {
      case 'Sent':
        return 'Delivered';
      case 'Accepted':
        return 'Accepted by Provider';
      case 'Processing':
        return 'In Flight';
      case 'Failed':
        return 'Failed';
      case 'Pending':
        return 'Pending';
      default:
        return status;
    }
  }

  /// Provider-aware semantic explanation for delivery status.
  String get semanticDescription {
    if (isFailed) {
      return 'The notification attempt failed.';
    }
    if (isMock) {
      return 'Mock Sent: Successful mock processing, not real email delivery.';
    }
    if (isAccepted || provider.toLowerCase().contains('resend')) {
      return 'Resend Accepted: Provider accepted the email request; final mailbox delivery has not been confirmed.';
    }
    if (isDelivered) {
      return 'Confirmed delivered by provider.';
    }
    return 'Status: $status';
  }
}
