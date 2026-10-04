import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../models/claim_document.dart';
import '../../models/document_requirement.dart';
import '../../models/payout.dart';
import '../../services/claim_service.dart';
import '../../services/payout_service.dart';
import '../../services/api_service.dart';

/// Claim details screen — Component B (Member 2).
/// Policyholder-facing detail view with documents, evidence upload,
/// ownership-scoped payout details, and policyholder operations (withdraw, delete draft/document).
class ClaimDetailsScreen extends StatefulWidget {
  final String? claimId;
  final ClaimService? claimService;
  final PayoutService? payoutService;

  const ClaimDetailsScreen({
    super.key,
    this.claimId,
    this.claimService,
    this.payoutService,
  });

  @override
  State<ClaimDetailsScreen> createState() => _ClaimDetailsScreenState();
}

class _ClaimDetailsScreenState extends State<ClaimDetailsScreen> {
  late final ClaimService _claimService;
  late final PayoutService _payoutService;
  final _picker = ImagePicker();

  Claim? _claim;
  Payout? _payout;
  DocumentRequirements? _docRequirements;
  bool _loading = true;
  String? _error;
  bool _uploading = false;
  bool _actionInProgress = false;
  bool _initialized = false;
  String? _effectiveClaimId;

  @override
  void initState() {
    super.initState();
    _claimService = widget.claimService ?? ClaimService();
    _payoutService = widget.payoutService ?? PayoutService();
    _effectiveClaimId = widget.claimId;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_initialized) {
      _initialized = true;
      final routeArg = ModalRoute.of(context)?.settings.arguments;
      if (routeArg is String) {
        _effectiveClaimId ??= routeArg;
      } else if (routeArg is Map) {
        _effectiveClaimId ??= routeArg['claimId'] as String? ?? routeArg['id'] as String?;
      }

      if (_effectiveClaimId != null) {
        _loadClaim(_effectiveClaimId!);
      } else {
        setState(() {
          _loading = false;
          _error = 'No claim ID provided.';
        });
      }
    }
  }

  Future<void> _loadClaim(String id) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final claim = await _claimService.getClaim(id);

      // Attempt to load associated payout if one exists
      Payout? payout;
      try {
        payout = await _payoutService.getPayoutByClaimId(id);
      } catch (_) {
        // Payout may not exist yet or user may not have permission; ignore
      }

      DocumentRequirements? docRequirements;
      try {
        docRequirements = await _claimService.getDocumentRequirements(id);
      } catch (_) {
        // Requirements may not exist yet or non-blocking
      }

      if (mounted) {
        setState(() {
          _claim = claim;
          _payout = payout;
          _docRequirements = docRequirements;
          _loading = false;
        });
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _uploadEvidence({String? preferredDocType}) async {
    if (_uploading || _actionInProgress || _claim == null) return;

    // Determine document type to upload
    String? selectedDocType = preferredDocType;
    if (selectedDocType == null) {
      final availableTypes = ClaimService.getRequiredDocuments(_claim!.claimType);
      final allOptions = {...availableTypes, 'Supporting Document', 'Other'}.toList();
      String tempSelected = allOptions.first;

      selectedDocType = await showDialog<String>(
        context: context,
        builder: (ctx) => StatefulBuilder(
          builder: (dlgCtx, setDlgState) => AlertDialog(
            title: const Text('Select Document Type'),
            content: DropdownButton<String>(
              isExpanded: true,
              value: tempSelected,
              items: allOptions.map((t) => DropdownMenuItem(value: t, child: Text(t))).toList(),
              onChanged: (val) {
                if (val != null) setDlgState(() => tempSelected = val);
              },
            ),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
              FilledButton(onPressed: () => Navigator.pop(ctx, tempSelected), child: const Text('Continue')),
            ],
          ),
        ),
      );
    }

    if (selectedDocType == null || !mounted) return;

    // Pick file source
    final choice = await showModalBottomSheet<String>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                'Upload $selectedDocType',
                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
            ),
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Take Photo'),
              onTap: () => Navigator.pop(ctx, 'camera'),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from Gallery'),
              onTap: () => Navigator.pop(ctx, 'gallery'),
            ),
            ListTile(
              leading: const Icon(Icons.attach_file),
              title: const Text('Browse Files (PDF / Documents)'),
              onTap: () => Navigator.pop(ctx, 'file'),
            ),
          ],
        ),
      ),
    );

    if (choice == null || !mounted) return;

    String? pickedFilePath;
    try {
      if (choice == 'camera') {
        final photo = await _picker.pickImage(source: ImageSource.camera);
        if (photo != null) pickedFilePath = photo.path;
      } else if (choice == 'gallery') {
        final photo = await _picker.pickImage(source: ImageSource.gallery);
        if (photo != null) pickedFilePath = photo.path;
      } else if (choice == 'file') {
        final result = await FilePicker.pickFiles(
          type: FileType.custom,
          allowedExtensions: ['pdf', 'jpg', 'jpeg', 'png', 'webp', 'gif', 'bmp'],
        );
        if (result != null && result.files.isNotEmpty && result.files.first.path != null) {
          pickedFilePath = result.files.first.path;
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('File selection failed: $e'), backgroundColor: Colors.red),
        );
      }
      return;
    }

    if (pickedFilePath == null || !mounted) return;

    // Validate size (10 MB)
    final file = File(pickedFilePath);
    if (await file.exists()) {
      final size = await file.length();
      if (size > 10 * 1024 * 1024) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('File exceeds 10 MB limit.'),
              backgroundColor: Colors.red,
            ),
          );
        }
        return;
      }
    }

    setState(() => _uploading = true);
    try {
      await _claimService.uploadDocument(
        claimId: _claim!.id,
        filePath: pickedFilePath,
        documentType: selectedDocType,
      );
      await FilePicker.clearTemporaryFiles();
      await _loadClaim(_claim!.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('$selectedDocType uploaded successfully'),
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

  Future<void> _confirmWithdrawClaim() async {
    if (_claim == null) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Withdraw Claim?'),
        content: Text(
          'Are you sure you want to withdraw claim ${_claim!.claimNumber}? Processing will be halted and the claim marked as Withdrawn.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Withdraw'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _actionInProgress = true);
    try {
      final updated = await _claimService.withdrawClaim(_claim!.id);
      if (mounted) {
        setState(() => _claim = updated);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Claim withdrawn successfully'),
            backgroundColor: Colors.blueGrey,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Withdrawal failed: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      if (mounted) setState(() => _actionInProgress = false);
    }
  }

  Future<void> _confirmDeleteDraft() async {
    if (_claim == null) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Draft Claim?'),
        content: Text(
          'Are you sure you want to permanently delete draft claim ${_claim!.claimNumber}? This action cannot be undone.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _actionInProgress = true);
    try {
      await _claimService.deleteClaim(_claim!.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Draft claim deleted'),
            backgroundColor: Colors.black87,
          ),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _actionInProgress = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Delete failed: $e'), backgroundColor: Colors.red),
        );
      }
    }
  }

  Future<void> _confirmDeleteDocument(ClaimDocument doc) async {
    if (_claim == null) return;

    final isFinalStatus =
        ['Approved', 'Rejected', 'Withdrawn', 'Closed'].contains(_claim!.status);
    if (isFinalStatus) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Cannot delete documents from a finalized claim.'),
          backgroundColor: Colors.red,
        ),
      );
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Document?'),
        content: Text('Are you sure you want to delete "${doc.fileName}"?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _actionInProgress = true);
    try {
      await _claimService.deleteDocument(_claim!.id, doc.id);
      await FilePicker.clearTemporaryFiles();
      await _loadClaim(_claim!.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Document deleted'),
            backgroundColor: Colors.black87,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Document deletion failed: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      if (mounted) setState(() => _actionInProgress = false);
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
      'Mismatch' => Colors.red,
      'Unreadable' => Colors.red,
      'Flagged' => Colors.orange,
      'NeedsReview' => Colors.orange,
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
    final isDraft = _claim?.status == 'Draft';
    final canWithdraw = _claim != null &&
        (_claim!.status == 'Submitted' || _claim!.status == 'UnderReview');
    final isFinalStatus = _claim != null &&
        ['Approved', 'Rejected', 'Withdrawn', 'Closed'].contains(_claim!.status);

    return Scaffold(
      appBar: AppBar(
        title: Text(_claim?.claimNumber ?? 'Claim Details'),
        actions: [
          if (_claim != null) ...[
            IconButton(
              icon: const Icon(Icons.refresh),
              tooltip: 'Refresh',
              onPressed: () => _loadClaim(_claim!.id),
            ),
            IconButton(
              icon: const Icon(Icons.timeline),
              tooltip: 'Status Timeline',
              onPressed: () {
                Navigator.pushNamed(
                  context,
                  '/claims/status',
                  arguments: {
                    'claimId': _claim!.id,
                    'status': _claim!.status,
                  },
                );
              },
            ),
          ],
        ],
      ),
      floatingActionButton: _claim != null && !isFinalStatus
          ? FloatingActionButton.extended(
              onPressed: _uploading || _actionInProgress ? null : _uploadEvidence,
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
      body: _buildBody(theme, isDraft, canWithdraw, isFinalStatus),
    );
  }

  Widget _buildBody(
    ThemeData theme,
    bool isDraft,
    bool canWithdraw,
    bool isFinalStatus,
  ) {
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
                onPressed: () {
                  if (_effectiveClaimId != null) {
                    _loadClaim(_effectiveClaimId!);
                  }
                },
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
          const SizedBox(height: 16),

          // Action Buttons Bar (Withdraw / Delete Draft)
          if (canWithdraw || isDraft) ...[
            Row(
              children: [
                if (canWithdraw)
                  Expanded(
                    child: OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: Colors.red.shade700,
                        side: BorderSide(color: Colors.red.shade300),
                      ),
                      onPressed: _actionInProgress ? null : _confirmWithdrawClaim,
                      icon: const Icon(Icons.undo, size: 18),
                      label: const Text('Withdraw Claim'),
                    ),
                  ),
                if (isDraft)
                  Expanded(
                    child: OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: Colors.red.shade700,
                        side: BorderSide(color: Colors.red.shade300),
                      ),
                      onPressed: _actionInProgress ? null : _confirmDeleteDraft,
                      icon: const Icon(Icons.delete_outline, size: 18),
                      label: const Text('Delete Draft'),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 16),
          ],

          // Payout Card if payout exists
          if (_payout != null) ...[
            _buildPayoutCard(theme),
            const SizedBox(height: 16),
          ],

          // Claim info card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  _detailRow('Claim Type', claim.claimType, Icons.category, theme),
                  _detailRow(
                    'Claimed Amount',
                    NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2).format(claim.claimedAmount),
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

          // Document Requirements Checklist
          _buildDocumentRequirementsCard(theme),
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
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Container(
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
                            if (!isFinalStatus)
                              IconButton(
                                icon: const Icon(Icons.delete_outline, size: 20, color: Colors.grey),
                                tooltip: 'Delete Document',
                                onPressed: _actionInProgress
                                    ? null
                                    : () => _confirmDeleteDocument(doc),
                              ),
                          ],
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

  Widget _buildDocumentRequirementsCard(ThemeData theme) {
    if (_docRequirements == null || _docRequirements!.requiredDocuments.isEmpty) {
      return const SizedBox.shrink();
    }
    final reqs = _docRequirements!;
    final isComplete = reqs.complete;
    final isFinalStatus = _claim != null &&
        ['Approved', 'Rejected', 'Withdrawn', 'Closed'].contains(_claim!.status);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Icon(
                      isComplete ? Icons.check_circle : Icons.pending_actions,
                      color: isComplete ? Colors.green : Colors.orange,
                      size: 20,
                    ),
                    const SizedBox(width: 8),
                    Text(
                      'Required Documents',
                      style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: (isComplete ? Colors.green : Colors.orange).withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    '${reqs.uploadedRequiredCount}/${reqs.requiredCount} Uploaded',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: isComplete ? Colors.green : Colors.orange,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            ...reqs.requiredDocuments.map((item) {
              return Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    Icon(
                      item.uploaded ? Icons.check_circle : Icons.radio_button_unchecked,
                      size: 18,
                      color: item.uploaded ? Colors.green : Colors.grey,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        item.type,
                        style: TextStyle(
                          fontSize: 13,
                          decoration: item.uploaded ? TextDecoration.lineThrough : null,
                          color: item.uploaded ? Colors.grey : theme.colorScheme.onSurface,
                        ),
                      ),
                    ),
                    if (!item.uploaded && !isFinalStatus)
                      TextButton.icon(
                        style: TextButton.styleFrom(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                          visualDensity: VisualDensity.compact,
                        ),
                        icon: const Icon(Icons.upload_file, size: 16),
                        label: const Text('Upload', style: TextStyle(fontSize: 12)),
                        onPressed: _uploading || _actionInProgress
                            ? null
                            : () => _uploadEvidence(preferredDocType: item.type),
                      ),
                  ],
                ),
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _buildPayoutCard(ThemeData theme) {
    final payout = _payout!;
    final isPaid = payout.statusDisplay == 'Paid';
    final isApproved = payout.statusDisplay == 'Approved';

    final badgeColor = isPaid
        ? Colors.green
        : isApproved
            ? Colors.indigo
            : payout.statusDisplay == 'Processing'
                ? Colors.blue
                : Colors.orange;

    final badgeLabel = isPaid
        ? 'Paid / Disbursed'
        : isApproved
            ? 'Approved (Pending Disbursement)'
            : payout.statusDisplay == 'PendingApproval'
                ? 'Pending Approval'
                : payout.statusDisplay;

    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: badgeColor.withValues(alpha: 0.3)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.payments_outlined, color: badgeColor, size: 20),
                    const SizedBox(width: 8),
                    Text(
                      'Claim Payout',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
                const SizedBox(width: 8),
                Flexible(
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: badgeColor.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Text(
                      badgeLabel,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: badgeColor,
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Approved Claim Amount:', style: TextStyle(color: Colors.grey, fontSize: 13)),
                Text(
                  NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2).format(payout.approvedClaimAmount),
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                ),
              ],
            ),
            if (payout.coverageLimit > 0) ...[
              const SizedBox(height: 6),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Policy Coverage Limit:', style: TextStyle(color: Colors.grey, fontSize: 13)),
                  Text(
                    NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2).format(payout.coverageLimit),
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 6),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Eligible Amount:', style: TextStyle(color: Colors.grey, fontSize: 13)),
                Text(
                  NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2).format(payout.effectiveEligibleAmount),
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  payout.deductiblePercentage != null
                      ? 'Deductible (${payout.deductiblePercentage!.toStringAsFixed(payout.deductiblePercentage! % 1 == 0 ? 0 : 2)}%):'
                      : 'Deductible:',
                  style: const TextStyle(color: Colors.grey, fontSize: 13),
                ),
                Text(
                  payout.formattedDeductible,
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                ),
              ],
            ),
            const Divider(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Authorized Payout:', style: TextStyle(fontWeight: FontWeight.bold)),
                Text(
                  payout.formattedPayout,
                  style: TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.bold,
                    color: isPaid ? Colors.green.shade700 : theme.colorScheme.primary,
                  ),
                ),
              ],
            ),
            if (payout.paymentReference != null && payout.paymentReference!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Payment Ref:', style: TextStyle(color: Colors.grey, fontSize: 12)),
                  Text(
                    payout.paymentReference!,
                    style: const TextStyle(
                      fontFamily: 'monospace',
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: FilledButton.tonal(
                onPressed: () {
                  Navigator.pushNamed(
                    context,
                    '/payout/status',
                    arguments: {
                      'claimId': _claim!.id,
                      'payoutId': payout.id,
                    },
                  );
                },
                child: const Text('View Payout Details'),
              ),
            ),
          ],
        ),
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
