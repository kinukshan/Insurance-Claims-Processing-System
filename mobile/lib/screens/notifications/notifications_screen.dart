import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../models/notification_item.dart';
import '../../services/notification_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Notification history screen — displays user notifications and delivery status.
///
/// Explicitly distinguishes between emails confirmed delivered by provider
/// and emails accepted by provider (e.g. Resend) where inbox delivery is pending.
class NotificationsScreen extends StatefulWidget {
  final NotificationService? notificationService;

  const NotificationsScreen({super.key, this.notificationService});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  late final NotificationService _notificationService;
  List<NotificationItem> _notifications = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _notificationService = widget.notificationService ?? NotificationService();
    _fetchNotifications();
  }

  Future<void> _fetchNotifications() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final items = await _notificationService.getNotifications();
      if (mounted) {
        setState(() {
          _notifications = items;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load notifications. Please try again.';
          _loading = false;
        });
      }
    }
  }

  IconData _channelIcon(String channel) {
    switch (channel.toLowerCase()) {
      case 'email':
        return Icons.email_outlined;
      case 'sms':
        return Icons.sms_outlined;
      case 'push':
        return Icons.notifications_active_outlined;
      default:
        return Icons.mark_email_read_outlined;
    }
  }

  void _showNotificationDetails(NotificationItem item) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: Colors.grey.shade300,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      item.subject,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: AppTheme.deepNavy,
                      ),
                    ),
                  ),
                  StatusBadge(status: item.status, label: item.statusLabel),
                ],
              ),
              const SizedBox(height: 16),
              const Divider(),
              const SizedBox(height: 12),
              _detailRow('Recipient', item.recipient),
              _detailRow('Channel', item.channel),
              _detailRow('Provider', item.provider.isEmpty ? 'Internal / Mock' : item.provider),
              _detailRow('Event Type', item.notificationType),
              _detailRow(
                'Date & Time',
                DateFormat('MMM dd, yyyy • hh:mm a').format(item.sentAt ?? item.createdAt),
              ),
              if (item.claimId != null)
                _detailRow('Related Claim ID', item.claimId!),
              if (item.payoutId != null)
                _detailRow('Related Payout ID', item.payoutId!),
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: item.isFailed
                      ? AppTheme.errorRed.withValues(alpha: 0.1)
                      : item.isMock
                          ? Colors.blue.withValues(alpha: 0.1)
                          : item.isAccepted
                              ? AppTheme.goldAccent.withValues(alpha: 0.1)
                              : Colors.green.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                  border: Border.all(
                    color: item.isFailed
                        ? AppTheme.errorRed.withValues(alpha: 0.3)
                        : item.isMock
                            ? Colors.blue.withValues(alpha: 0.3)
                            : item.isAccepted
                                ? AppTheme.goldAccent.withValues(alpha: 0.3)
                                : Colors.green.withValues(alpha: 0.3),
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      item.isFailed
                          ? Icons.error_outline
                          : item.isMock
                              ? Icons.developer_mode
                              : item.isAccepted
                                  ? Icons.info_outline
                                  : Icons.check_circle_outline,
                      size: 18,
                      color: item.isFailed
                          ? AppTheme.errorRed
                          : item.isMock
                              ? Colors.blue
                              : item.isAccepted
                                  ? AppTheme.goldAccent
                                  : Colors.green,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        item.semanticDescription,
                        style: TextStyle(
                          fontSize: 12,
                          color: item.isFailed ? AppTheme.errorRed : AppTheme.deepNavy,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              if (item.errorMessage != null && item.errorMessage!.isNotEmpty) ...[
                const SizedBox(height: 12),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppTheme.errorRed.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                    border: Border.all(color: AppTheme.errorRed.withValues(alpha: 0.3)),
                  ),
                  child: Row(
                    children: [
                      const Icon(Icons.error_outline, size: 18, color: AppTheme.errorRed),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          item.errorMessage!,
                          style: const TextStyle(fontSize: 12, color: AppTheme.errorRed),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }

  Widget _detailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              label,
              style: const TextStyle(
                color: AppTheme.textSecondary,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(
                color: AppTheme.deepNavy,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: _fetchNotifications,
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: AppTheme.primaryTeal),
      );
    }

    if (_error != null) {
      return ErrorRetryView(
        message: _error!,
        onRetry: _fetchNotifications,
      );
    }

    if (_notifications.isEmpty) {
      return const EmptyStateView(
        icon: Icons.notifications_none_outlined,
        title: 'No Notifications Yet',
        description: 'You will receive updates here when your claims or payouts change status.',
      );
    }

    return RefreshIndicator(
      color: AppTheme.primaryTeal,
      onRefresh: _fetchNotifications,
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: _notifications.length,
        separatorBuilder: (context, index) => const SizedBox(height: 10),
        itemBuilder: (context, index) {
          final item = _notifications[index];
          final timestamp = item.sentAt ?? item.createdAt;
          final timeStr = DateFormat('MMM dd, yyyy • hh:mm a').format(timestamp);

          return Card(
            elevation: 0.5,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
              side: BorderSide(
                color: item.isFailed
                    ? AppTheme.errorRed.withValues(alpha: 0.3)
                    : Colors.grey.shade200,
              ),
            ),
            child: InkWell(
              borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
              onTap: () => _showNotificationDetails(item),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: AppTheme.primaryTeal.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Icon(
                            _channelIcon(item.channel),
                            size: 20,
                            color: AppTheme.primaryTeal,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                item.subject,
                                style: const TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.w700,
                                  color: AppTheme.deepNavy,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                item.recipient,
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppTheme.textSecondary,
                                ),
                              ),
                            ],
                          ),
                        ),
                        StatusBadge(
                          status: item.status,
                          label: item.statusLabel,
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          timeStr,
                          style: TextStyle(
                            fontSize: 11,
                            color: AppTheme.textSecondary.withValues(alpha: 0.8),
                          ),
                        ),
                        Row(
                          children: [
                            Text(
                              item.provider.isEmpty ? 'System' : item.provider,
                              style: const TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w500,
                                color: AppTheme.textSecondary,
                              ),
                            ),
                            const Icon(
                              Icons.chevron_right,
                              size: 16,
                              color: AppTheme.textSecondary,
                            ),
                          ],
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
