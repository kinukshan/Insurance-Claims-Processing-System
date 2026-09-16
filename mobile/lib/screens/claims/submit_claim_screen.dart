import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'dart:io';
import '../../services/claim_service.dart';

/// Submit claim screen — Component B (Member 2).
/// Policyholder-facing claim submission form with image picker for evidence.
class SubmitClaimScreen extends StatefulWidget {
  const SubmitClaimScreen({super.key});

  @override
  State<SubmitClaimScreen> createState() => _SubmitClaimScreenState();
}

class _SubmitClaimScreenState extends State<SubmitClaimScreen> {
  final _formKey = GlobalKey<FormState>();
  final _claimService = ClaimService();
  final _picker = ImagePicker();

  // Form fields
  final _policyIdController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _locationController = TextEditingController();
  final _amountController = TextEditingController();
  String _claimType = 'Auto';
  DateTime _incidentDate = DateTime.now();
  final List<XFile> _evidenceFiles = [];

  bool _submitting = false;
  String? _error;

  static const _claimTypes = [
    'Auto', 'Home', 'Health', 'Life', 'Travel', 'Property', 'Liability', 'Other',
  ];

  static const _documentTypeMap = {
    'Auto': 'Photos of Damage',
    'Home': 'Photos of Damage',
    'Health': 'Medical Report',
    'Life': 'Supporting Document',
    'Travel': 'Receipts',
    'Property': 'Photos of Damage',
    'Liability': 'Incident Report',
    'Other': 'Supporting Document',
  };

  @override
  void dispose() {
    _policyIdController.dispose();
    _descriptionController.dispose();
    _locationController.dispose();
    _amountController.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _incidentDate,
      firstDate: DateTime.now().subtract(const Duration(days: 3650)),
      lastDate: DateTime.now(),
    );
    if (picked != null) {
      setState(() => _incidentDate = picked);
    }
  }

  Future<void> _pickEvidence() async {
    showModalBottomSheet(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Take Photo'),
              onTap: () async {
                Navigator.pop(ctx);
                final photo = await _picker.pickImage(source: ImageSource.camera);
                if (photo != null) setState(() => _evidenceFiles.add(photo));
              },
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from Gallery'),
              onTap: () async {
                Navigator.pop(ctx);
                final images = await _picker.pickMultiImage();
                if (images.isNotEmpty) setState(() => _evidenceFiles.addAll(images));
              },
            ),
          ],
        ),
      ),
    );
  }

  void _removeEvidence(int index) {
    setState(() => _evidenceFiles.removeAt(index));
  }

  Future<void> _submitClaim() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      // 1. Create the claim (Draft)
      final claim = await _claimService.createClaim(
        policyId: _policyIdController.text.trim(),
        claimType: _claimType,
        incidentDate: _incidentDate,
        incidentLocation: _locationController.text.trim(),
        description: _descriptionController.text.trim(),
        claimedAmount: double.parse(_amountController.text.trim()),
      );

      // 2. Upload evidence documents
      for (final file in _evidenceFiles) {
        final docType = _documentTypeMap[_claimType] ?? 'Supporting Document';
        await _claimService.uploadDocument(
          claimId: claim.id,
          filePath: file.path,
          documentType: docType,
        );
      }

      // 3. Submit the claim (Draft → Submitted)
      await _claimService.submitClaim(claim.id);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Claim ${claim.claimNumber} submitted successfully!'),
            backgroundColor: Colors.green,
          ),
        );
        Navigator.pop(context, true); // Return true to refresh history
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Submit Claim')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Error banner
              if (_error != null)
                Container(
                  margin: const EdgeInsets.only(bottom: 16),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.errorContainer,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _error!,
                    style: TextStyle(color: theme.colorScheme.onErrorContainer),
                  ),
                ),

              // Policy ID
              TextFormField(
                controller: _policyIdController,
                decoration: const InputDecoration(
                  labelText: 'Policy ID',
                  hintText: 'Enter your policy ID',
                  prefixIcon: Icon(Icons.policy),
                  border: OutlineInputBorder(),
                ),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Policy ID is required' : null,
              ),
              const SizedBox(height: 16),

              // Claim Type
              DropdownButtonFormField<String>(
                value: _claimType,
                decoration: const InputDecoration(
                  labelText: 'Claim Type',
                  prefixIcon: Icon(Icons.category),
                  border: OutlineInputBorder(),
                ),
                items: _claimTypes
                    .map((t) => DropdownMenuItem(value: t, child: Text(t)))
                    .toList(),
                onChanged: (v) => setState(() => _claimType = v!),
              ),
              const SizedBox(height: 16),

              // Incident Date
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.calendar_today),
                title: const Text('Incident Date'),
                subtitle: Text(
                  '${_incidentDate.year}-${_incidentDate.month.toString().padLeft(2, '0')}-${_incidentDate.day.toString().padLeft(2, '0')}',
                  style: theme.textTheme.bodyLarge,
                ),
                trailing: FilledButton.tonal(
                  onPressed: _pickDate,
                  child: const Text('Change'),
                ),
              ),
              const SizedBox(height: 16),

              // Incident Location
              TextFormField(
                controller: _locationController,
                decoration: const InputDecoration(
                  labelText: 'Incident Location',
                  hintText: 'Where did the incident occur?',
                  prefixIcon: Icon(Icons.location_on),
                  border: OutlineInputBorder(),
                ),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Location is required' : null,
              ),
              const SizedBox(height: 16),

              // Description
              TextFormField(
                controller: _descriptionController,
                decoration: const InputDecoration(
                  labelText: 'Description',
                  hintText: 'Describe the incident in detail',
                  prefixIcon: Icon(Icons.description),
                  border: OutlineInputBorder(),
                ),
                maxLines: 4,
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Description is required' : null,
              ),
              const SizedBox(height: 16),

              // Claimed Amount
              TextFormField(
                controller: _amountController,
                decoration: const InputDecoration(
                  labelText: 'Claimed Amount (\$)',
                  hintText: '0.00',
                  prefixIcon: Icon(Icons.attach_money),
                  border: OutlineInputBorder(),
                ),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                validator: (v) {
                  if (v == null || v.trim().isEmpty) return 'Amount is required';
                  final amount = double.tryParse(v.trim());
                  if (amount == null || amount <= 0) return 'Must be greater than zero';
                  return null;
                },
              ),
              const SizedBox(height: 24),

              // Evidence section
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text('Evidence', style: theme.textTheme.titleMedium),
                          FilledButton.tonalIcon(
                            onPressed: _pickEvidence,
                            icon: const Icon(Icons.add_a_photo),
                            label: const Text('Add'),
                          ),
                        ],
                      ),
                      if (_evidenceFiles.isEmpty)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 24),
                          child: Center(
                            child: Text(
                              'No evidence attached.\nTap "Add" to capture or select photos.',
                              textAlign: TextAlign.center,
                              style: TextStyle(color: theme.colorScheme.outline),
                            ),
                          ),
                        )
                      else
                        ..._evidenceFiles.asMap().entries.map((entry) {
                          final i = entry.key;
                          final file = entry.value;
                          return ListTile(
                            contentPadding: EdgeInsets.zero,
                            leading: ClipRRect(
                              borderRadius: BorderRadius.circular(6),
                              child: Image.file(
                                File(file.path),
                                width: 48,
                                height: 48,
                                fit: BoxFit.cover,
                              ),
                            ),
                            title: Text(
                              file.name,
                              overflow: TextOverflow.ellipsis,
                            ),
                            subtitle: Text(
                              _documentTypeMap[_claimType] ?? 'Supporting Document',
                            ),
                            trailing: IconButton(
                              icon: const Icon(Icons.close),
                              onPressed: () => _removeEvidence(i),
                            ),
                          );
                        }),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 24),

              // Submit button
              FilledButton.icon(
                onPressed: _submitting ? null : _submitClaim,
                icon: _submitting
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.send),
                label: Text(_submitting ? 'Submitting…' : 'Submit Claim'),
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
