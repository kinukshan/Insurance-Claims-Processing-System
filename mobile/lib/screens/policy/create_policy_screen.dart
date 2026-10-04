import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../models/policy.dart';
import '../../providers/auth_provider.dart';
import '../../services/policy_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Screen for creating a new insurance policy by an authenticated Policyholder.
///
/// Component A (Member 1).
///
/// Features:
/// - Fetches real policy types and coverage options from ASP.NET Core backend.
/// - Validates coverage limit, dates, fixed deductibles, and exclusions.
/// - Prevents duplicate submissions.
/// - Displays real persisted status (Draft) upon creation.
/// - Clearly informs policyholders that Draft policies require Admin activation
///   before claims can be filed.
class CreatePolicyScreen extends StatefulWidget {
  final PolicyService? policyService;
  final String? policyholderId;

  const CreatePolicyScreen({
    super.key,
    this.policyService,
    this.policyholderId,
  });

  @override
  State<CreatePolicyScreen> createState() => _CreatePolicyScreenState();
}

class _CreatePolicyScreenState extends State<CreatePolicyScreen> {
  late final PolicyService _policyService;
  final _formKey = GlobalKey<FormState>();

  final _coverageLimitController = TextEditingController();
  final _deductibleController = TextEditingController();
  final _exclusionsController = TextEditingController();

  List<PolicyType> _policyTypes = [];
  PolicyType? _selectedPolicyType;
  bool _loadingTypes = true;
  String? _typesError;

  DateTime _startDate = DateTime(DateTime.now().year, DateTime.now().month, DateTime.now().day);
  DateTime _expiryDate = DateTime(DateTime.now().year + 1, DateTime.now().month, DateTime.now().day);

  bool _submitting = false;
  String? _submitError;
  Policy? _createdPolicy;

  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');
  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);

  @override
  void initState() {
    super.initState();
    _policyService = widget.policyService ?? PolicyService();
    _loadPolicyTypes();
  }

  @override
  void dispose() {
    _coverageLimitController.dispose();
    _deductibleController.dispose();
    _exclusionsController.dispose();
    super.dispose();
  }

  Future<void> _loadPolicyTypes() async {
    setState(() {
      _loadingTypes = true;
      _typesError = null;
    });

    try {
      final types = await _policyService.getPolicyTypes();
      if (!mounted) return;
      setState(() {
        _policyTypes = types;
        _loadingTypes = false;
        if (types.isNotEmpty) {
          _onPolicyTypeSelected(types.first);
        }
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _typesError = 'Failed to load policy products. Please check your connection and try again.';
        _loadingTypes = false;
      });
    }
  }

  void _onPolicyTypeSelected(PolicyType? type) {
    if (type == null) return;
    setState(() {
      _selectedPolicyType = type;
      _coverageLimitController.text = type.defaultCoverageLimit.toStringAsFixed(0);
      _deductibleController.text =
          '${type.deductiblePercentage.toStringAsFixed(type.deductiblePercentage % 1 == 0 ? 0 : 2)}% of approved claim';
      _submitError = null;
    });
  }

  Future<void> _pickStartDate() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final initial = _startDate.isBefore(today) ? today : _startDate;
    final picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: today,
      lastDate: DateTime(2040),
    );
    if (picked != null && picked != _startDate) {
      setState(() {
        _startDate = DateTime(picked.year, picked.month, picked.day);
        if (!_expiryDate.isAfter(_startDate)) {
          _expiryDate = DateTime(_startDate.year + 1, _startDate.month, _startDate.day);
        }
      });
    }
  }

  Future<void> _pickExpiryDate() async {
    final minExpiry = _startDate.add(const Duration(days: 1));
    final defaultExpiry = DateTime(_startDate.year + 1, _startDate.month, _startDate.day);
    final initial = _expiryDate.isAfter(_startDate) ? _expiryDate : defaultExpiry;
    final picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: minExpiry,
      lastDate: DateTime(2045),
    );
    if (picked != null && picked != _expiryDate) {
      setState(() {
        _expiryDate = DateTime(picked.year, picked.month, picked.day);
      });
    }
  }

  Future<void> _submitPolicy() async {
    if (_submitting) return;

    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_selectedPolicyType == null) {
      setState(() {
        _submitError = 'Please select an insurance policy product.';
      });
      return;
    }

    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    if (_startDate.isBefore(today)) {
      setState(() {
        _submitError = 'Start date cannot be before today.';
      });
      return;
    }

    if (!_startDate.isBefore(_expiryDate)) {
      setState(() {
        _submitError = 'Start date must be before expiry date.';
      });
      return;
    }

    final coverageLimit = double.tryParse(_coverageLimitController.text.trim());
    if (coverageLimit == null || coverageLimit <= 0) {
      setState(() {
        _submitError = 'Please enter a valid coverage limit greater than zero.';
      });
      return;
    }

    final authUser = context.read<AuthProvider>().user;
    final policyholderId = widget.policyholderId ?? authUser?.userId ?? '';

    setState(() {
      _submitting = true;
      _submitError = null;
    });

    try {
      final request = CreatePolicyRequest(
        policyholderId: policyholderId,
        policyTypeId: _selectedPolicyType!.id,
        coverageLimit: coverageLimit,
        deductible: _selectedPolicyType!.fixedDeductible,
        deductiblePercentage: _selectedPolicyType!.effectiveDeductiblePercentage,
        startDate: _startDate,
        expiryDate: _expiryDate,
        exclusions: _exclusionsController.text.trim().isNotEmpty
            ? _exclusionsController.text.trim()
            : null,
      );

      final created = await _policyService.createPolicy(request);

      if (!mounted) return;
      setState(() {
        _submitting = false;
        _createdPolicy = created;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _submitError = e.toString().replaceFirst('Exception: ', '').replaceFirst('ApiException: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Create Policy'),
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loadingTypes) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(color: AppTheme.primaryTeal),
            SizedBox(height: 16),
            Text(
              'Loading insurance products...',
              style: TextStyle(color: AppTheme.textSecondary),
            ),
          ],
        ),
      );
    }

    if (_typesError != null) {
      return ErrorRetryView(
        message: _typesError!,
        onRetry: _loadPolicyTypes,
      );
    }

    if (_createdPolicy != null) {
      return _buildSuccessView();
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_submitError != null) ...[
              ErrorBanner(
                message: _submitError!,
                onDismiss: () => setState(() => _submitError = null),
              ),
              const SizedBox(height: 16),
            ],

            // Notice about Draft Status & Activation
            _buildDraftNoticeBanner(),
            const SizedBox(height: 20),

            // Product Selection Section
            const Text(
              'Insurance Product',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: AppTheme.deepNavy,
              ),
            ),
            const SizedBox(height: 8),

            DropdownButtonFormField<PolicyType>(
              key: const Key('policy_type_dropdown'),
              initialValue: _selectedPolicyType,
              isExpanded: true,
              decoration: InputDecoration(
                labelText: 'Select Policy Type',
                prefixIcon: const Icon(Icons.shield_outlined, color: AppTheme.primaryTeal),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              items: _policyTypes.map((type) {
                final dedText =
                    '${type.deductiblePercentage.toStringAsFixed(type.deductiblePercentage % 1 == 0 ? 0 : 2)}%';
                return DropdownMenuItem<PolicyType>(
                  value: type,
                  child: Text(
                    '${type.name} ($dedText deductible)',
                    style: const TextStyle(fontWeight: FontWeight.w500),
                  ),
                );
              }).toList(),
              onChanged: _submitting ? null : _onPolicyTypeSelected,
              validator: (val) => val == null ? 'Please select a policy type' : null,
            ),
            const SizedBox(height: 12),

            // Product Details Card
            if (_selectedPolicyType != null) _buildProductDetailsCard(_selectedPolicyType!),
            const SizedBox(height: 24),

            // Policy Terms Section
            const Text(
              'Coverage & Terms',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: AppTheme.deepNavy,
              ),
            ),
            const SizedBox(height: 12),

            // Coverage Limit Input
            TextFormField(
              key: const Key('coverage_limit_field'),
              controller: _coverageLimitController,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'Coverage Limit (LKR)',
                hintText: 'e.g. 500000',
                prefixText: 'LKR ',
                prefixIcon: const Icon(Icons.attach_money),
                helperText: 'Enter desired coverage amount in LKR',
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              enabled: !_submitting,
              validator: (val) {
                if (val == null || val.trim().isEmpty) {
                  return 'Coverage limit is required';
                }
                final num = double.tryParse(val.trim());
                if (num == null) {
                  return 'Please enter a valid number';
                }
                if (num <= 0) {
                  return 'Coverage limit must be greater than zero';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),

            // Deductible Field (Fixed per policy type)
            TextFormField(
              key: const Key('deductible_field'),
              controller: _deductibleController,
              readOnly: true,
              enabled: false,
              decoration: InputDecoration(
                labelText: 'Authoritative Deductible',
                prefixIcon: const Icon(Icons.lock_outline),
                helperText: _selectedPolicyType != null
                    ? '${_selectedPolicyType!.deductiblePercentage.toStringAsFixed(_selectedPolicyType!.deductiblePercentage % 1 == 0 ? 0 : 2)}% of eligible approved claim amount'
                    : 'Standard percentage deductible enforced by policy terms.',
                filled: true,
                fillColor: Colors.grey.shade100,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
            const SizedBox(height: 16),

            // Date Pickers Row
            Row(
              children: [
                Expanded(
                  child: InkWell(
                    key: const Key('start_date_picker'),
                    onTap: _submitting ? null : _pickStartDate,
                    child: InputDecorator(
                      decoration: InputDecoration(
                        labelText: 'Start Date',
                        prefixIcon: const Icon(Icons.calendar_today, size: 20),
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                      ),
                      child: Text(
                        _dateFormat.format(_startDate),
                        style: const TextStyle(fontSize: 14),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: InkWell(
                    key: const Key('expiry_date_picker'),
                    onTap: _submitting ? null : _pickExpiryDate,
                    child: InputDecorator(
                      decoration: InputDecoration(
                        labelText: 'Expiry Date',
                        prefixIcon: const Icon(Icons.event, size: 20),
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                      ),
                      child: Text(
                        _dateFormat.format(_expiryDate),
                        style: const TextStyle(fontSize: 14),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Exclusions Input
            TextFormField(
              key: const Key('exclusions_field'),
              controller: _exclusionsController,
              maxLines: 3,
              maxLength: 2000,
              enabled: !_submitting,
              decoration: InputDecoration(
                labelText: 'Exclusions (Optional)',
                hintText: 'Specify any pre-existing conditions or exclusions...',
                alignLabelWithHint: true,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              validator: (val) {
                if (val != null && val.length > 2000) {
                  return 'Exclusions text cannot exceed 2000 characters';
                }
                return null;
              },
            ),
            const SizedBox(height: 24),

            // Submit Button
            BrandedButton(
              label: 'Create Policy',
              isLoading: _submitting,
              onPressed: _submitting ? null : _submitPolicy,
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }

  Widget _buildDraftNoticeBanner() {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppTheme.primaryTeal.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppTheme.primaryTeal.withValues(alpha: 0.25)),
      ),
      child: const Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline, color: AppTheme.primaryTeal, size: 22),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Policy Creation Notice',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppTheme.deepNavy,
                    fontSize: 14,
                  ),
                ),
                SizedBox(height: 4),
                Text(
                  'Newly created policies will be created in Draft status. Draft policies must be reviewed and activated by an Administrator before claims can be filed against them.',
                  style: TextStyle(
                    fontSize: 12.5,
                    color: AppTheme.textSecondary,
                    height: 1.35,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildProductDetailsCard(PolicyType type) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade300),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                type.name,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 15,
                  color: AppTheme.deepNavy,
                ),
              ),
              StatusBadge(
                status: 'Active',
                label: type.insuranceClassName,
                customColor: type.isLife ? Colors.purple : AppTheme.primaryTeal,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            type.description,
            style: const TextStyle(fontSize: 13, color: AppTheme.textSecondary),
          ),
          const Divider(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Standard Coverage', style: TextStyle(fontSize: 11, color: Colors.grey)),
                  Text(
                    _currencyFormat.format(type.defaultCoverageLimit),
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text('Deductible', style: TextStyle(fontSize: 11, color: Colors.grey)),
                  Text(
                    '${type.deductiblePercentage.toStringAsFixed(type.deductiblePercentage % 1 == 0 ? 0 : 2)}%',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: type.isLife ? Colors.green.shade700 : AppTheme.deepNavy,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildSuccessView() {
    final policy = _createdPolicy!;
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: Colors.green.shade50,
                shape: BoxShape.circle,
                border: Border.all(color: Colors.green.shade200, width: 2),
              ),
              child: Icon(Icons.check_circle, size: 48, color: Colors.green.shade600),
            ),
            const SizedBox(height: 20),
            const Text(
              'Policy Created Successfully!',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w700,
                color: AppTheme.deepNavy,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Policy #${policy.policyNumber}',
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: AppTheme.primaryTeal,
              ),
            ),
            const SizedBox(height: 20),

            Card(
              elevation: 1,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  children: [
                    _successRow('Status', policy.status, isStatus: true),
                    const Divider(height: 20),
                    _successRow('Policy Type', policy.policyTypeName),
                    const Divider(height: 20),
                    _successRow('Coverage Limit', _currencyFormat.format(policy.coverageLimit)),
                    const Divider(height: 20),
                    _successRow('Deductible', policy.formattedDeductible),
                    const Divider(height: 20),
                    _successRow('Term', '${_dateFormat.format(policy.startDate)} to ${_dateFormat.format(policy.expiryDate)}'),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),

            // Ineligibility Warning
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFFEF3C7),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFFF59E0B).withValues(alpha: 0.4)),
              ),
              child: const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.warning_amber_rounded, color: Color(0xFFD97706), size: 22),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'This policy is currently in Draft status. It requires Administrator activation before claims can be submitted against it.',
                      style: TextStyle(
                        fontSize: 13,
                        color: Color(0xFF92400E),
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 28),

            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton.icon(
                key: const Key('return_to_policies_btn'),
                icon: const Icon(Icons.arrow_back),
                label: const Text('Return to My Policies'),
                onPressed: () {
                  Navigator.pop(context, true);
                },
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _successRow(String label, String value, {bool isStatus = false}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 13, color: AppTheme.textSecondary),
        ),
        if (isStatus)
          StatusBadge(status: value)
        else
          Text(
            value,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: AppTheme.deepNavy,
            ),
          ),
      ],
    );
  }
}
