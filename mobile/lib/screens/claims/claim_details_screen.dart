import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../services/claim_service.dart';
import '../../services/api_service.dart';

/// Claim details screen — Component B (Member 2).
/// Policyholder-facing detail view with documents and evidence upload.
class ClaimDetailsScreen extends StatefulWidget {
  const ClaimDetailsScreen({super.key});

  @override
  State<ClaimDetailsScreen> createState() => _ClaimDetailsScreenState();
}

class _ClaimDetailsScreenState extends State<ClaimDetailsScreen> {
  final _claimService = ClaimService();
  final _picker = ImagePicker();

  Claim? _claim;
  bool _loading = true;
  String? _error;
  bool _uploading = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final claimId = ModalRoute.of(context)!.settings.arguments as String;
    _loadClaim(claimId);
  }

  Future<void> _loadClaim(String id) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final claim = await _claimService.getClaim(id);
      if (mounted) setState(() => _claim = claim);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _uploadEvidence() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Take Photo'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from Gallery'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );

    if (source == null || _claim == null) return;

    final image = await _picker.pickImage(source: source);
    if (image == null) return;

    setState(() => _uploading = true);
    try {
      await _claimService.uploadDocument(
        claimId: _claim!.id,
        filePath: image.path,
        documentType: 'Supporting Document',
      );
      await _loadClaim(_claim!.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Document uploaded successfully'),
            backgroundColor: Colors.green,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Upload failed: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  Color _statusColor(String status) {
    return switch (status) {
      'Draft' => Colors.grey,
      'Submitted' => Colors.indigo,
      'UnderReview' || 'DocumentVerification' => Colors.blue,
      'AdditionalDocumentsRequired' => Colors.orange,
      'RiskAssessment' || 'PendingApproval' => Colors.amber.shade700,
      'Approved' || 'PayoutProcessing' => Colors.green,
      'Rejected' => Colors.red,
      'Withdrawn' || 'Closed' => Colors.blueGrey,
      _ => Colors.grey,
    };
  }

  Color _verifStatusColor(String status) {
    return switch (status) {
      'Verified' => Colors.green,
      'Rejected' => Colors.red,
      'Flagged' => Colors.orange,
      _ => Colors.grey,
    };
  }

  String _formatStatus(String status) {
    return status.replaceAllMapped(
      RegExp(r'([A-Z])'),
      (match) => ' ${match.group(0)}',
    ).trim();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text(_claim?.claimNumber ?? 'Claim Details'),
        actions: [
          if (_claim != null)
            IconButton(
              icon: const Icon(Icons.timeline),
              tooltip: 'Status Timeline',
              onPressed: () {
                Navigator.pushNamed(
                  context,
                  '/claims/status',
                  arguments: _claim!.status,
                );
              },
            ),
        ],
      ),
      floatingActionButton: _claim != null &&
              !['Approved', 'Rejected', 'Withdrawn', 'Closed']
                  .contains(_claim!.status)
          ? FloatingActionButton.extended(
              onPressed: _uploading ? null : _uploadEvidence,
              icon: _uploading
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.add_a_photo),
              label: Text(_uploading ? 'Uploading…' : 'Add Evidence'),
            )
          : null,
      body: _buildBody(theme),
    );
  }

  Widget _buildBody(ThemeData theme) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.error_outline, size: 48, color: theme.colorScheme.error),
              const SizedBox(height: 16),
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton.tonal(
                onPressed: () => _loadClaim(
                  (ModalRoute.of(context)!.settings.arguments as String),
                ),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    if (_claim == null) return const SizedBox.shrink();

    final claim = _claim!;
    final statusColor = _statusColor(claim.status);

    return RefreshIndicator(
      onRefresh: () => _loadClaim(claim.id),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Status badge
          Center(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              decoration: BoxDecoration(
                color: statusColor.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(24),
              ),
              child: Text(
                _formatStatus(claim.status),
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  color: statusColor,
                ),
              ),
            ),
          ),
          const SizedBox(height: 20),

          // Claim info card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  _detailRow('Claim Type', claim.claimType, Icons.category, theme),
                  _detailRow(
                    'Amount',
                    NumberFormat.currency(symbol: '\$').format(claim.claimedAmount),
                    Icons.attach_money,
                    theme,
                  ),
                  _detailRow(
                    'Incident Date',
                    DateFormat.yMMMd().format(claim.incidentDate),
                    Icons.calendar_today,
                    theme,
                  ),
                  _detailRow('Location', claim.incidentLocation, Icons.location_on, theme),
                  if (claim.submittedAt != null)
                    _detailRow(
                      'Submitted',
                      DateFormat.yMMMd().add_jm().format(claim.submittedAt!),
                      Icons.send,
                      theme,
                    ),
                  _detailRow(
                    'Created',
                    DateFormat.yMMMd().add_jm().format(claim.createdAt),
                    Icons.access_time,
                    theme,
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Description
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Description', style: theme.textTheme.titleSmall),
                  const SizedBox(height: 8),
                  Text(
                    claim.description,
                    style: TextStyle(
                      color: theme.colorScheme.onSurfaceVariant,
                      height: 1.6,
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Documents
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Documents (${claim.documents.length})',
                    style: theme.textTheme.titleSmall,
                  ),
                  const SizedBox(height: 12),
                  if (claim.documents.isEmpty)
                    Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Text(
                          'No documents uploaded yet',
                          style: TextStyle(color: theme.colorScheme.outline),
                        ),
                      ),
                    )
                  else
                    ...claim.documents.map((doc) {
                      final verifColor = _verifStatusColor(doc.verificationStatus);
                      return ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: theme.colorScheme.primaryContainer,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Icon(
                            Icons.insert_drive_file,
                            color: theme.colorScheme.primary,
                          ),
                        ),
                        title: Text(
                          doc.fileName,
                          overflow: TextOverflow.ellipsis,
                        ),
                        subtitle: Text(
                          '${doc.documentType} • ${doc.formattedSize}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        trailing: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: verifColor.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            doc.verificationStatus,
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w600,
                              color: verifColor,
                            ),
                          ),
                        ),
                      );
                    }),
                ],
              ),
            ),
          ),
          const SizedBox(height: 80), // Space for FAB
        ],
      ),
    );
  }

  Widget _detailRow(String label, String value, IconData icon, ThemeData theme) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 18, color: theme.colorScheme.outline),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 11,
                    color: theme.colorScheme.outline,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                Text(value, style: theme.textTheme.bodyMedium),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
