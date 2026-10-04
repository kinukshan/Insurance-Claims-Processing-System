import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'dart:io';

import '../../models/claim.dart';
import '../../models/policy.dart';
import '../../services/claim_service.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Holds an attached document and its assigned metadata.
class AttachedDocument {
  final String? backendId;
  final String filePath;
  final String fileName;
  final int fileSize;
  final bool isImage;
  String documentType;

  AttachedDocument({
    this.backendId,
    required this.filePath,
    required this.fileName,
    required this.fileSize,
    required this.isImage,
    required this.documentType,
  });

  String get formattedSize {
    if (fileSize < 1024) return '$fileSize B';
    if (fileSize < 1024 * 1024) {
      return '${(fileSize / 1024).toStringAsFixed(1)} KB';
    }
    return '${(fileSize / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}

/// Submit claim screen — Component B (Member 2).
///
/// Policyholder-facing claim submission form with:
/// - User-friendly policy selector populated from authenticated Policyholder's policies
/// - Policy and claim type compatibility filtering
/// - Incident information with clear validation
/// - Evidence attachment supporting camera, photo gallery, and PDF/document files
/// - Backend document checklist and per-document type classification
/// - Draft preservation and recovery
/// - Pre-submission review summary
class SubmitClaimScreen extends StatefulWidget {
  final String? draftClaimId;
  final String? initialPolicyId;
  final ClaimService? claimService;
  final PolicyService? policyService;

  const SubmitClaimScreen({
    super.key,
    this.draftClaimId,
    this.initialPolicyId,
    this.claimService,
    this.policyService,
  });

  @override
  State<SubmitClaimScreen> createState() => SubmitClaimScreenState();
}

class SubmitClaimScreenState extends State<SubmitClaimScreen> {
  late final ClaimService _claimService;
  late final PolicyService _policyService;
  final _formKey = GlobalKey<FormState>();
  final _picker = ImagePicker();

  // Form field controllers
  final _policyIdController = TextEditingController();
  final _locationController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _amountController = TextEditingController();

  // Form state
  String _claimType = 'Auto';
  DateTime _incidentDate = DateTime(DateTime.now().year, DateTime.now().month, DateTime.now().day);
  List<Policy> _policies = [];
  Policy? _selectedPolicy;

  // Evidence state
  final List<AttachedDocument> _attachedDocuments = [];
  List<String> _requiredDocTypes = [];

  // Draft recovery state
  String? _draftClaimId;
  Claim? _draftClaim;
  bool _draftCreated = false;

  // UI state
  bool _submitting = false;
  String? _error;
  String? _uploadProgress;

  /// Max file size: 10 MB per backend limits.
  static const int _maxFileSizeBytes = 10 * 1024 * 1024;

  /// Allowed file extensions.
  static const List<String> _allowedExtensions = [
    'jpg',
    'jpeg',
    'png',
    'gif',
    'bmp',
    'webp',
    'pdf',
  ];

  /// Standard claim types available when no policy filters apply.
  static const List<String> _allClaimTypes = [
    'Auto',
    'Home',
    'Health',
    'Life',
    'Travel',
    'Property',
    'Liability',
    'Other',
  ];

  @override
  void initState() {
    super.initState();
    _claimService = widget.claimService ?? ClaimService();
    _policyService = widget.policyService ?? PolicyService();
    _draftClaimId = widget.draftClaimId;
    if (widget.initialPolicyId != null) {
      _policyIdController.text = widget.initialPolicyId!;
    }
    _updateRequiredDocuments();
    _loadPolicies();
  }

  @override
  void dispose() {
    _policyIdController.dispose();
    _locationController.dispose();
    _descriptionController.dispose();
    _amountController.dispose();
    super.dispose();
  }

  // ─────────────────────── Data Loading ───────────────────────

  Future<void> _loadPolicies() async {
    try {
      final policies = await _policyService.getMyPolicies();
      debugPrint('LOAD_POLICIES_SUCCESS: found ${policies.length} policies');
      if (!mounted) return;
      Policy? toSelect;
      if (widget.initialPolicyId != null) {
        final matching =
            policies.where((p) => p.id == widget.initialPolicyId).toList();
        if (matching.isNotEmpty) toSelect = matching.first;
      } else {
        final eligible = policies
            .where((p) => p.status.toLowerCase() == 'active' && !p.isExpired)
            .toList();
        if (eligible.isNotEmpty) toSelect = eligible.first;
      }
      setState(() {
        _policies = policies;
        if (toSelect != null && _selectedPolicy == null) {
          _selectedPolicy = toSelect;
          _policyIdController.text = toSelect.id;
          final compatible =
              ClaimService.getCompatibleClaimTypes(toSelect.policyTypeName);
          if (compatible.isNotEmpty && !compatible.contains(_claimType)) {
            _claimType = compatible.first;
          }
          _updateRequiredDocuments();
        }
      });
      if (_draftClaimId != null) {
        await _loadDraft(_draftClaimId!);
      }
    } catch (e, st) {
      debugPrint('LOAD_POLICIES_FAILED: $e\n$st');
      // In offline or testing mode without backend, allow manual entry
      if (!mounted) return;
      if (_draftClaimId != null) {
        await _loadDraft(_draftClaimId!);
      }
    }
  }

  Future<void> _loadDraft(String claimId) async {
    try {
      final claim = await _claimService.getClaim(claimId);
      if (!mounted) return;
      setState(() {
        _draftClaim = claim;
        _draftClaimId = claim.id;
        _draftCreated = true;
        _policyIdController.text = claim.policyId ?? '';
        _descriptionController.text = claim.description;
        _locationController.text = claim.incidentLocation;
        _amountController.text = claim.claimedAmount.toStringAsFixed(2);
        _incidentDate = claim.incidentDate;
        _claimType = claim.claimType;
        if (_policies.isNotEmpty && claim.policyId != null) {
          final matching =
              _policies.where((p) => p.id == claim.policyId).toList();
          if (matching.isNotEmpty) {
            _selectedPolicy = matching.first;
          }
        }
        _updateRequiredDocuments();
      });

      // Load existing documents if any
      try {
        final docs = await _claimService.getDocuments(claimId);
        if (!mounted) return;
        setState(() {
          for (final doc in docs) {
            if (!_attachedDocuments.any((d) => d.fileName == doc.fileName)) {
              _attachedDocuments.add(AttachedDocument(
                backendId: doc.id,
                filePath: doc.fileUrl,
                fileName: doc.fileName,
                fileSize: doc.fileSize,
                isImage: doc.contentType.startsWith('image/'),
                documentType: doc.documentType,
              ));
            }
          }
        });
      } catch (_) {}
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load draft claim: ${_friendlyError(e)}';
        _draftClaimId = null;
      });
    }
  }

  // ─────────────────────── Policy & Claim Type ───────────────────────

  List<Policy> get _eligiblePolicies {
    return _policies.where((p) {
      final isActive = p.status.toLowerCase() == 'active';
      final notExpired = !p.isExpired;
      return isActive && notExpired;
    }).toList();
  }

  List<String> get _availableClaimTypes {
    if (_selectedPolicy != null) {
      final compatible =
          ClaimService.getCompatibleClaimTypes(_selectedPolicy!.policyTypeName);
      if (compatible.isNotEmpty) return compatible;
    }
    return _allClaimTypes;
  }

  void _onPolicySelected(Policy? policy) {
    setState(() {
      _selectedPolicy = policy;
      if (policy != null) {
        _policyIdController.text = policy.id;
        final compatible =
            ClaimService.getCompatibleClaimTypes(policy.policyTypeName);
        if (compatible.isNotEmpty && !compatible.contains(_claimType)) {
          _claimType = compatible.first;
        }
      }
      _updateRequiredDocuments();
    });
  }

  void _onClaimTypeChanged(String? type) {
    if (type == null) return;
    setState(() {
      _claimType = type;
      _updateRequiredDocuments();
    });
  }

  void _updateRequiredDocuments() {
    _requiredDocTypes = ClaimService.getRequiredDocuments(_claimType);
  }

  // ─────────────────────── Date Picker ───────────────────────

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final picked = await showDatePicker(
      context: context,
      initialDate: _incidentDate.isAfter(today) ? today : _incidentDate,
      firstDate: DateTime(today.year - 10, today.month, today.day),
      lastDate: today,
    );
    if (picked != null) {
      setState(() => _incidentDate = DateTime(picked.year, picked.month, picked.day));
    }
  }

  // ─────────────────────── File Attachment ───────────────────────

  Future<void> _addFile(String filePath, String fileName, bool isImage) async {
    final ext = fileName.split('.').last.toLowerCase();
    if (!_allowedExtensions.contains(ext)) {
      _showErrorSnackBar(
        'File type ".$ext" is not supported. Allowed: ${_allowedExtensions.join(", ")}',
      );
      return;
    }

    final file = File(filePath);
    int size = 0;
    if (file.existsSync()) {
      size = await file.length();
      if (size > _maxFileSizeBytes) {
        _showErrorSnackBar(
          'File "$fileName" exceeds the 10 MB limit (${(size / (1024 * 1024)).toStringAsFixed(1)} MB).',
        );
        return;
      }
    }

    if (_attachedDocuments.any((d) => d.filePath == filePath)) {
      _showErrorSnackBar('File "$fileName" is already attached.');
      return;
    }

    // Default to the first missing required document type if available
    final attachedTypes = _attachedDocuments.map((d) => d.documentType).toSet();
    final missing =
        _requiredDocTypes.where((t) => !attachedTypes.contains(t)).toList();
    final defaultType =
        missing.isNotEmpty ? missing.first : 'Supporting Document';

    setState(() {
      _attachedDocuments.add(AttachedDocument(
        filePath: filePath,
        fileName: fileName,
        fileSize: size,
        isImage: isImage,
        documentType: defaultType,
      ));
    });
  }

  Future<void> _pickImage(ImageSource source) async {
    try {
      if (source == ImageSource.camera) {
        final photo = await _picker.pickImage(source: source);
        if (photo != null) await _addFile(photo.path, photo.name, true);
      } else {
        final images = await _picker.pickMultiImage();
        for (final image in images) {
          await _addFile(image.path, image.name, true);
        }
      }
    } catch (_) {}
  }

  Future<void> _pickFiles() async {
    try {
      final result = await FilePicker.pickFiles(
        allowMultiple: true,
        type: FileType.custom,
        allowedExtensions: _allowedExtensions,
      );
      if (result != null && result.files.isNotEmpty) {
        for (final file in result.files) {
          if (file.path != null) {
            final isImage = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp']
                .contains(file.extension?.toLowerCase());
            await _addFile(file.path!, file.name, isImage);
          }
        }
      }
    } catch (_) {}
  }

  void _showPickOptions() {
    showModalBottomSheet(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'Add Supporting Documents',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w600,
                  color: AppTheme.deepNavy,
                ),
              ),
            ),
            ListTile(
              leading: Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: AppTheme.primaryTeal.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Icon(Icons.camera_alt, color: AppTheme.primaryTeal),
              ),
              title: const Text('Take Photo'),
              subtitle: const Text('Capture evidence using camera'),
              onTap: () {
                Navigator.pop(ctx);
                _pickImage(ImageSource.camera);
              },
            ),
            ListTile(
              leading: Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: AppTheme.primaryTeal.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child:
                    const Icon(Icons.photo_library, color: AppTheme.primaryTeal),
              ),
              title: const Text('Choose from Gallery'),
              subtitle: const Text('Select images from your device'),
              onTap: () {
                Navigator.pop(ctx);
                _pickImage(ImageSource.gallery);
              },
            ),
            ListTile(
              leading: Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: AppTheme.primaryTeal.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child:
                    const Icon(Icons.attach_file, color: AppTheme.primaryTeal),
              ),
              title: const Text('Browse Files (PDF / Documents)'),
              subtitle: const Text('Select PDF or image files'),
              onTap: () {
                Navigator.pop(ctx);
                _pickFiles();
              },
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }

  Future<void> _showDocumentTypeDialog(int index) async {
    final doc = _attachedDocuments[index];
    final allTypes = {
      ..._requiredDocTypes,
      'Police Report',
      'Photos of Damage',
      'Repair Estimate',
      'Driver License',
      'Medical Report',
      'Hospital Bills',
      'Prescription',
      'Doctor Referral',
      'Death Certificate',
      'Policy Document',
      'Beneficiary / Nominee Identification',
      'Claim Form',
      'Property Deed',
      'Property Valuation',
      'Travel Itinerary',
      'Receipts',
      'Incident Report',
      'Third Party Claim',
      'Legal Notice',
      'Supporting Document',
    };

    final ordered = <String>[
      ..._requiredDocTypes,
      ...allTypes.where((t) => !_requiredDocTypes.contains(t)),
    ];

    final selected = await showDialog<String>(
      context: context,
      builder: (ctx) => SimpleDialog(
        title: Text('Document Type for "${doc.fileName}"'),
        children: ordered.map((type) {
          final isRequired = _requiredDocTypes.contains(type);
          return SimpleDialogOption(
            onPressed: () => Navigator.pop(ctx, type),
            child: Row(
              children: [
                Expanded(child: Text(type)),
                if (isRequired)
                  const StatusBadge(status: 'required', label: 'Required'),
                if (type == doc.documentType)
                  const Icon(Icons.check,
                      color: AppTheme.primaryTeal, size: 18),
              ],
            ),
          );
        }).toList(),
      ),
    );

    if (selected != null) {
      setState(() => _attachedDocuments[index].documentType = selected);
    }
  }

  // ─────────────────────── Draft & Submission Flow ───────────────────────

  Future<void> _saveDraftOnly() async {
    if (_submitting) return;
    if (_policyIdController.text.trim().isEmpty) {
      _showErrorSnackBar('Please select or enter a Policy ID to save a draft.');
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
      _uploadProgress = 'Saving draft...';
    });

    try {
      final policyId = _policyIdController.text.trim();
      final location = _locationController.text.trim().isEmpty
          ? 'Unspecified Location'
          : _locationController.text.trim();
      final description = _descriptionController.text.trim().isEmpty
          ? 'Draft claim'
          : _descriptionController.text.trim();
      final amount = double.tryParse(_amountController.text.trim()) ?? 1.0;

      if (_draftCreated && _draftClaimId != null) {
        final updated = await _claimService.updateClaim(
          _draftClaimId!,
          description: description,
          incidentLocation: location,
          claimedAmount: amount,
          incidentDate: _incidentDate,
        );
        _draftClaim = updated;
      } else {
        final draft = await _claimService.createClaim(
          policyId: policyId,
          claimType: _claimType,
          incidentDate: _incidentDate,
          incidentLocation: location,
          description: description,
          claimedAmount: amount,
        );
        _draftClaimId = draft.id;
        _draftClaim = draft;
        _draftCreated = true;
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Draft saved (Claim #${_draftClaim?.claimNumber ?? _draftClaimId})',
          ),
          backgroundColor: AppTheme.primaryTeal,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = 'Failed to save draft: ${_friendlyError(e)}');
    } finally {
      if (mounted) {
        setState(() {
          _submitting = false;
          _uploadProgress = null;
        });
      }
    }
  }

  Future<void> _handleSubmit() async {
    if (_submitting) return;

    // Validate form inputs
    if (!_formKey.currentState!.validate()) return;

    // Check for missing required documents
    final assignedTypes =
        _attachedDocuments.map((d) => d.documentType).toSet();
    final missing =
        _requiredDocTypes.where((t) => !assignedTypes.contains(t)).toList();

    if (missing.isNotEmpty) {
      final proceed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Missing Required Documents'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'The following recommended documents have not been attached:',
                style: TextStyle(fontSize: 13),
              ),
              const SizedBox(height: 8),
              ...missing.map((d) => Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 2),
                    child: Text('• $d',
                        style: const TextStyle(
                            fontSize: 13, fontWeight: FontWeight.w600)),
                  )),
              const SizedBox(height: 12),
              const Text(
                'You can submit now, but the claims adjuster may request additional evidence before processing. Do you want to submit anyway?',
                style: TextStyle(fontSize: 12, color: AppTheme.textSecondary),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Add Documents'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Submit Anyway'),
            ),
          ],
        ),
      );
      if (proceed != true) return;
    }

    await _executeDraftAndSubmission();
  }

  Future<void> _executeDraftAndSubmission() async {
    setState(() {
      _submitting = true;
      _error = null;
      _uploadProgress = 'Creating draft claim...';
    });

    try {
      final policyId = _policyIdController.text.trim();
      final location = _locationController.text.trim();
      final description = _descriptionController.text.trim();
      final amount = double.parse(_amountController.text.trim());

      Claim draft;
      if (_draftCreated && _draftClaimId != null) {
        draft = await _claimService.updateClaim(
          _draftClaimId!,
          description: description,
          incidentLocation: location,
          claimedAmount: amount,
          incidentDate: _incidentDate,
        );
      } else {
        draft = await _claimService.createClaim(
          policyId: policyId,
          claimType: _claimType,
          incidentDate: _incidentDate,
          incidentLocation: location,
          description: description,
          claimedAmount: amount,
        );
        _draftClaimId = draft.id;
        _draftClaim = draft;
        _draftCreated = true;
      }

      // Step 2: Upload local documents
      final localDocs = _attachedDocuments
          .where((d) => !d.filePath.startsWith('http'))
          .toList();
      final total = localDocs.length;

      for (int i = 0; i < total; i++) {
        final doc = localDocs[i];
        if (!mounted) return;
        setState(() {
          _uploadProgress =
              'Uploading document ${i + 1} of $total: ${doc.fileName}';
        });

        try {
          await _claimService.uploadDocument(
            claimId: draft.id,
            filePath: doc.filePath,
            documentType: doc.documentType,
          );
        } catch (uploadError) {
          if (!mounted) return;
          setState(() {
            _submitting = false;
            _uploadProgress = null;
            _error =
                'Upload failed for "${doc.fileName}": ${_friendlyError(uploadError)}.\n'
                'Your draft claim (${draft.claimNumber}) has been preserved. You can retry submission.';
          });
          return;
        }
      }

      // Step 3: Final submission
      if (!mounted) return;
      setState(() => _uploadProgress = 'Submitting claim...');

      final submitted = await _claimService.submitClaim(draft.id);

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Claim ${submitted.claimNumber} submitted successfully! Status: ${submitted.status}',
          ),
          backgroundColor: AppTheme.successGreen,
        ),
      );

      Navigator.pop(context, true);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = _friendlyError(e);
        if (_draftCreated) {
          _error = '$_error\n\nYour draft claim has been preserved.';
        }
        _submitting = false;
        _uploadProgress = null;
      });
    }
  }

  // ─────────────────────── UI Helpers ───────────────────────

  void _showErrorSnackBar(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: AppTheme.errorRed),
    );
  }

  String _friendlyError(dynamic e) {
    final msg = e.toString();
    if (msg.contains('ApiException')) {
      return msg.replaceAll(RegExp(r'ApiException\(\d+\): ?'), '');
    }
    return msg.replaceAll('Exception: ', '');
  }

  String _formatDate(DateTime date) {
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
  }

  String _formatCurrency(double amount) {
    return 'LKR ${amount.toStringAsFixed(2)}';
  }

  int get _coveredRequiredCount {
    final assignedTypes = _attachedDocuments.map((d) => d.documentType).toSet();
    return _requiredDocTypes.where((t) => assignedTypes.contains(t)).length;
  }

  // ─────────────────────── Build ───────────────────────

  @override
  Widget build(BuildContext context) {
    final eligible = _eligiblePolicies;
    debugPrint('SUBMIT_CLAIM_BUILD: policies=${_policies.length}, eligible=${eligible.length}, selected=${_selectedPolicy?.policyNumber}');

    Widget content;
    try {
      content = _buildContent(context);
      debugPrint('SUBMIT_CLAIM_CONTENT_BUILT_OK');
    } catch (e, st) {
      debugPrint('SUBMIT_CLAIM_BUILD_ERROR: $e\n$st');
      content = Center(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Text('Error building claim form: $e', style: const TextStyle(color: Colors.red)),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Submit Claim'),
        actions: [
          if (_draftCreated && _draftClaim != null)
            Padding(
              padding: const EdgeInsets.only(right: 12),
              child: Center(
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: AppTheme.goldAccent.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    'Draft: ${_draftClaim!.claimNumber}',
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: AppTheme.goldAccent,
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
      body: _submitting
          ? Stack(
              children: [
                content,
                LoadingOverlay(message: _uploadProgress ?? 'Processing claim...'),
              ],
            )
          : content,
    );
  }

  Widget _buildContent(BuildContext context) {
    final theme = Theme.of(context);
    final eligible = _eligiblePolicies;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Error Banner
            if (_error != null) ...[
              ErrorBanner(
                message: _error!,
                onDismiss: () => setState(() => _error = null),
              ),
              const SizedBox(height: 16),
            ],

                    // Draft Recovery Banner
                    if (_draftCreated && _draftClaimId != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppTheme.goldAccent.withValues(alpha: 0.1),
                          borderRadius:
                              BorderRadius.circular(AppTheme.radiusMedium),
                          border: Border.all(
                            color: AppTheme.goldAccent.withValues(alpha: 0.3),
                          ),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.drafts,
                                color: AppTheme.goldAccent, size: 20),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                'Preserving draft claim${_draftClaim != null ? " #${_draftClaim!.claimNumber}" : ""}. You can update details and retry upload.',
                                style: const TextStyle(
                                  color: AppTheme.goldAccent,
                                  fontWeight: FontWeight.w600,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],

                    // Policyholder Policy Selector (if user policies loaded)
                    if (_policies.isNotEmpty) ...[
                      if (eligible.isEmpty) ...[
                        Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: const Color(0xFFFEF3C7),
                            borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                            border: Border.all(color: const Color(0xFFF59E0B).withValues(alpha: 0.4)),
                          ),
                          child: const Row(
                            children: [
                              Icon(Icons.info_outline, color: Color(0xFFD97706), size: 20),
                              SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  'You do not have any active policies. Policies in Draft, Expired, or Cancelled status cannot be used to file claims until activated.',
                                  style: TextStyle(
                                    color: Color(0xFF92400E),
                                    fontSize: 13,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 12),
                      ] else ...[
                        DropdownButtonFormField<Policy>(
                          key: ValueKey('policy_${_selectedPolicy?.id}'),
                          initialValue: _selectedPolicy != null &&
                                  eligible.any((p) => p.id == _selectedPolicy!.id)
                              ? eligible.firstWhere((p) => p.id == _selectedPolicy!.id)
                              : null,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'Select Policy',
                            prefixIcon: Icon(Icons.shield_outlined),
                            border: OutlineInputBorder(),
                          ),
                          items: eligible.map((p) {
                            return DropdownMenuItem<Policy>(
                              value: p,
                              child: Text(
                                '${p.policyNumber} — ${p.policyTypeName} (${_formatCurrency(p.coverageLimit)})',
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 14),
                              ),
                            );
                          }).toList(),
                          onChanged: _draftCreated ? null : _onPolicySelected,
                        ),
                        const SizedBox(height: 12),
                      ],
                    ],

                    // Selected Policy Card
                    if (_selectedPolicy != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppTheme.primaryTeal.withValues(alpha: 0.06),
                          borderRadius:
                              BorderRadius.circular(AppTheme.radiusMedium),
                          border: Border.all(
                            color: AppTheme.primaryTeal.withValues(alpha: 0.2),
                          ),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text(
                                  _selectedPolicy!.policyNumber,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    fontSize: 14,
                                    color: AppTheme.deepNavy,
                                  ),
                                ),
                                StatusBadge(
                                  status: _selectedPolicy!.status,
                                  label: _selectedPolicy!.status,
                                ),
                              ],
                            ),
                            const SizedBox(height: 6),
                            Text(
                              'Type: ${_selectedPolicy!.policyTypeName}',
                              style: const TextStyle(fontSize: 13),
                            ),
                            Text(
                              'Coverage Limit: ${_formatCurrency(_selectedPolicy!.coverageLimit)}',
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: AppTheme.primaryTeal,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],

                    // Policy ID input field
                    TextFormField(
                      controller: _policyIdController,
                      decoration: const InputDecoration(
                        labelText: 'Policy ID',
                        prefixIcon: Icon(Icons.policy),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) => (v == null || v.trim().isEmpty)
                          ? 'Policy ID is required'
                          : null,
                      onChanged: (val) {
                        if (_policies.isNotEmpty) {
                          final matching =
                              _policies.where((p) => p.id == val.trim()).toList();
                          if (matching.isNotEmpty) {
                            _onPolicySelected(matching.first);
                          }
                        }
                      },
                    ),
                    const SizedBox(height: 16),

                    // Claim Type
                    DropdownButtonFormField<String>(
                      key: ValueKey('claim_type_$_claimType'),
                      initialValue: _availableClaimTypes.contains(_claimType)
                          ? _claimType
                          : (_availableClaimTypes.isNotEmpty ? _availableClaimTypes.first : null),
                      decoration: const InputDecoration(
                        labelText: 'Claim Type',
                        prefixIcon: Icon(Icons.category),
                        border: OutlineInputBorder(),
                      ),
                      items: _availableClaimTypes.map((type) {
                        return DropdownMenuItem<String>(
                          value: type,
                          child: Text(type),
                        );
                      }).toList(),
                      onChanged: _onClaimTypeChanged,
                    ),
                    const SizedBox(height: 16),

                  // Incident Date
                  InkWell(
                    onTap: _pickDate,
                    borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'Incident Date',
                        prefixIcon: Icon(Icons.calendar_today),
                        border: OutlineInputBorder(),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(_formatDate(_incidentDate)),
                          const Icon(Icons.edit,
                              size: 18, color: AppTheme.textSecondary),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Incident Location
                  TextFormField(
                    controller: _locationController,
                    decoration: const InputDecoration(
                      labelText: 'Incident Location',
                      prefixIcon: Icon(Icons.location_on),
                      border: OutlineInputBorder(),
                    ),
                    validator: (v) => (v == null || v.trim().isEmpty)
                        ? 'Location is required'
                        : null,
                  ),
                  const SizedBox(height: 16),

                  // Description
                  TextFormField(
                    controller: _descriptionController,
                    decoration: const InputDecoration(
                      labelText: 'Description',
                      prefixIcon: Icon(Icons.description),
                      border: OutlineInputBorder(),
                    ),
                    maxLines: 4,
                    validator: (v) => (v == null || v.trim().isEmpty)
                        ? 'Description is required'
                        : null,
                  ),
                  const SizedBox(height: 16),

                  // Claimed Amount
                  TextFormField(
                    controller: _amountController,
                    decoration: const InputDecoration(
                      labelText: 'Claimed Amount (LKR)',
                      prefixIcon: Icon(Icons.attach_money),
                      border: OutlineInputBorder(),
                    ),
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Amount is required';
                      }
                      final amount = double.tryParse(v.trim());
                      if (amount == null || amount <= 0) {
                        return 'Must be greater than zero';
                      }
                      if (_selectedPolicy != null &&
                          amount > _selectedPolicy!.coverageLimit) {
                        return 'Exceeds policy coverage limit of ${_formatCurrency(_selectedPolicy!.coverageLimit)}';
                      }
                      return null;
                    },
                  ),
                  const SizedBox(height: 24),

                  // Evidence Section
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Evidence', style: theme.textTheme.titleMedium),
                      OutlinedButton.icon(
                        onPressed: _showPickOptions,
                        icon: const Icon(Icons.add_a_photo, size: 18),
                        label: const Text('Add'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),

                  // Document Checklist
                  if (_requiredDocTypes.isNotEmpty) ...[
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: AppTheme.surfaceWhite,
                        borderRadius:
                            BorderRadius.circular(AppTheme.radiusMedium),
                        border: Border.all(color: AppTheme.divider),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              const Icon(Icons.checklist,
                                  size: 16, color: AppTheme.primaryTeal),
                              const SizedBox(width: 6),
                              Text(
                                'Checklist for $_claimType claim ($_coveredRequiredCount of ${_requiredDocTypes.length} attached)',
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                  color: AppTheme.deepNavy,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 6),
                          Wrap(
                            spacing: 6,
                            runSpacing: 4,
                            children: _requiredDocTypes.map((type) {
                              final isAttached = _attachedDocuments
                                  .any((d) => d.documentType == type);
                              return Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 6, vertical: 2),
                                decoration: BoxDecoration(
                                  color: isAttached
                                      ? AppTheme.successGreen
                                          .withValues(alpha: 0.1)
                                      : AppTheme.divider.withValues(alpha: 0.3),
                                  borderRadius: BorderRadius.circular(4),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(
                                      isAttached
                                          ? Icons.check
                                          : Icons.circle_outlined,
                                      size: 12,
                                      color: isAttached
                                          ? AppTheme.successGreen
                                          : AppTheme.textSecondary,
                                    ),
                                    const SizedBox(width: 4),
                                    Text(
                                      type,
                                      style: TextStyle(
                                        fontSize: 11,
                                        color: isAttached
                                          ? AppTheme.successGreen
                                          : AppTheme.textSecondary,
                                        fontWeight: isAttached
                                          ? FontWeight.w600
                                          : FontWeight.normal,
                                      ),
                                    ),
                                  ],
                                ),
                              );
                            }).toList(),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 8),
                  ],

                  // Attached Files / Empty State
                  if (_attachedDocuments.isEmpty)
                    Container(
                      padding: const EdgeInsets.all(24),
                      decoration: BoxDecoration(
                        border: Border.all(color: theme.colorScheme.outlineVariant),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Center(
                        child: Text(
                          'No evidence attached.\nTap "Add" to capture or select photos or files.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: theme.colorScheme.outline),
                        ),
                      ),
                    )
                  else
                    ..._attachedDocuments.asMap().entries.map((entry) {
                      final i = entry.key;
                      final doc = entry.value;
                      return Card(
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          leading: Icon(
                            doc.isImage
                                ? Icons.image
                                : Icons.picture_as_pdf,
                            color: doc.isImage
                                ? AppTheme.primaryTeal
                                : AppTheme.errorRed,
                          ),
                          title: Text(
                            doc.fileName,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 14),
                          ),
                          subtitle: InkWell(
                            onTap: () => _showDocumentTypeDialog(i),
                            child: Row(
                              children: [
                                Text(
                                  doc.documentType,
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppTheme.primaryTeal,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                const SizedBox(width: 4),
                                const Icon(Icons.edit,
                                    size: 12, color: AppTheme.primaryTeal),
                                const SizedBox(width: 8),
                                Text(
                                  doc.formattedSize,
                                  style: const TextStyle(
                                      fontSize: 11,
                                      color: AppTheme.textSecondary),
                                ),
                              ],
                            ),
                          ),
                          trailing: IconButton(
                            icon: const Icon(Icons.close, size: 18),
                            onPressed: () async {
                              final doc = _attachedDocuments[i];
                              if (doc.backendId != null && _draftClaimId != null) {
                                try {
                                  await _claimService.deleteDocument(_draftClaimId!, doc.backendId!);
                                } catch (e) {
                                  _showErrorSnackBar('Failed to remove document from server: $e');
                                  return;
                                }
                              }
                              await FilePicker.clearTemporaryFiles();
                              if (mounted) {
                                setState(() => _attachedDocuments.removeAt(i));
                              }
                            },
                          ),
                        ),
                      );
                    }),
                  const SizedBox(height: 24),

                  // Review Summary Card (before submission)
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: AppTheme.surfaceWhite,
                      borderRadius:
                          BorderRadius.circular(AppTheme.radiusMedium),
                      border: Border.all(color: AppTheme.divider),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Row(
                          children: [
                            Icon(Icons.rate_review_outlined,
                                size: 16, color: AppTheme.deepNavy),
                            SizedBox(width: 6),
                            Text(
                              'Claim Summary',
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                fontSize: 13,
                                color: AppTheme.deepNavy,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Type: $_claimType  •  Date: ${_formatDate(_incidentDate)}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        if (_selectedPolicy != null)
                          Text(
                            'Policy: ${_selectedPolicy!.policyNumber} (${_selectedPolicy!.policyTypeName})',
                            style: const TextStyle(fontSize: 12),
                          ),
                        Text(
                          'Documents: ${_attachedDocuments.length} attached',
                          style: const TextStyle(fontSize: 12),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Actions: Save Draft and Submit Claim
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: _submitting ? null : _saveDraftOnly,
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 14),
                          ),
                          child: const Text('Save Draft'),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        flex: 2,
                        child: FilledButton(
                          onPressed: _submitting ? null : _handleSubmit,
                          style: FilledButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            backgroundColor: AppTheme.primaryTeal,
                          ),
                          child: const Text('Submit Claim'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
  }
}
