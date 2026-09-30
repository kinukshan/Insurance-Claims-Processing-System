import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../models/policy.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Screen for Underwriters to edit existing policy terms.
///
/// Underwriter permissions:
/// - Can edit: Coverage Limit, Expiry Date, Exclusions.
/// - Read-only: Deductible (authoritatively fixed by product terms).
/// - Read-only: Status (Underwriters cannot change status; Admin only).
class EditPolicyScreen extends StatefulWidget {
  final Policy policy;

  const EditPolicyScreen({super.key, required this.policy});

  @override
  State<EditPolicyScreen> createState() => _EditPolicyScreenState();
}

class _EditPolicyScreenState extends State<EditPolicyScreen> {
  final PolicyService _policyService = PolicyService();
  final _formKey = GlobalKey<FormState>();

  late TextEditingController _coverageLimitCtrl;
  late TextEditingController _exclusionsCtrl;
  late DateTime _expiryDate;
  bool _saving = false;
  String? _error;

  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  @override
  void initState() {
    super.initState();
    _coverageLimitCtrl = TextEditingController(
      text: widget.policy.coverageLimit.toStringAsFixed(0),
    );
    _exclusionsCtrl = TextEditingController(
      text: widget.policy.exclusions ?? '',
    );
    _expiryDate = widget.policy.expiryDate;
  }

  @override
  void dispose() {
    _coverageLimitCtrl.dispose();
    _exclusionsCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickExpiryDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _expiryDate.isAfter(widget.policy.startDate)
          ? _expiryDate
          : widget.policy.startDate.add(const Duration(days: 30)),
      firstDate: widget.policy.startDate.add(const Duration(days: 1)),
      lastDate: DateTime(2045),
    );
    if (picked != null && picked != _expiryDate) {
      setState(() => _expiryDate = picked);
    }
  }

  Future<void> _savePolicyTerms() async {
    if (!_formKey.currentState!.validate()) return;

    final coverageLimit = double.tryParse(_coverageLimitCtrl.text.trim());
    if (coverageLimit == null || coverageLimit <= 0) {
      setState(() => _error = 'Coverage limit must be greater than zero.');
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final updated = await _policyService.updatePolicy(
        widget.policy.id,
        coverageLimit: coverageLimit,
        expiryDate: _expiryDate,
        exclusions: _exclusionsCtrl.text.trim().isNotEmpty
            ? _exclusionsCtrl.text.trim()
            : null,
      );

      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Policy #${updated.policyNumber} updated successfully.'),
          backgroundColor: Colors.green,
        ),
      );
      Navigator.pop(context, updated);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Edit Policy #${widget.policy.policyNumber}'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Underwriter Role Banner
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: Colors.indigo.shade50,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: Colors.indigo.shade200),
                ),
                child: const Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.edit_note, color: Colors.indigo, size: 20),
                    SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        'Underwriter Policy Terms Modification: You are authorized to adjust coverage limits, term expiry, and policy exclusions. Fixed deductibles and policy statuses are restricted.',
                        style: TextStyle(fontSize: 12, color: Colors.indigo, height: 1.3),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              if (_error != null) ...[
                ErrorBanner(
                  message: _error!,
                  onDismiss: () => setState(() => _error = null),
                ),
                const SizedBox(height: 16),
              ],

              // Policy Product Card
              Card(
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            widget.policy.policyTypeName,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                              color: AppTheme.deepNavy,
                            ),
                          ),
                          StatusBadge(status: widget.policy.status),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Policyholder ID: ${widget.policy.policyholderId}',
                        style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // Coverage Limit Input
              TextFormField(
                controller: _coverageLimitCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: InputDecoration(
                  labelText: 'Coverage Limit (LKR)',
                  prefixText: 'LKR ',
                  prefixIcon: const Icon(Icons.attach_money),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                ),
                validator: (val) {
                  if (val == null || val.trim().isEmpty) return 'Coverage limit is required';
                  final num = double.tryParse(val.trim());
                  if (num == null || num <= 0) return 'Must be a positive number';
                  return null;
                },
              ),
              const SizedBox(height: 16),

              // Deductible (Read-only)
              TextFormField(
                initialValue: widget.policy.formattedDeductible,
                readOnly: true,
                enabled: false,
                decoration: InputDecoration(
                  labelText: 'Deductible (Read-only)',
                  prefixIcon: const Icon(Icons.lock_outline),
                  filled: true,
                  fillColor: Colors.grey.shade100,
                  helperText: 'Deductibles are enforced by underwriting policy terms and cannot be modified.',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                ),
              ),
              const SizedBox(height: 16),

              // Expiry Date Picker
              InkWell(
                onTap: _saving ? null : _pickExpiryDate,
                child: InputDecorator(
                  decoration: InputDecoration(
                    labelText: 'Policy Expiry Date',
                    prefixIcon: const Icon(Icons.event),
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                  child: Text(
                    _dateFormat.format(_expiryDate),
                    style: const TextStyle(fontSize: 14),
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // Exclusions Input
              TextFormField(
                controller: _exclusionsCtrl,
                maxLines: 4,
                maxLength: 2000,
                decoration: InputDecoration(
                  labelText: 'Policy Exclusions / Conditions',
                  hintText: 'Enter specific excluded conditions or clauses...',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                ),
              ),
              const SizedBox(height: 20),

              // Save Button
              BrandedButton(
                label: 'Save Policy Modifications',
                isLoading: _saving,
                icon: Icons.check,
                onPressed: _saving ? null : _savePolicyTerms,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
