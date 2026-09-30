import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../models/claim.dart';
import '../../models/document_requirement.dart';
import '../../models/payout.dart';
import '../../models/ai_workflow_models.dart';
import '../../services/claim_service.dart';
import '../../services/risk_service.dart';
import '../../services/payout_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import '../../widgets/ai_workflow_widgets.dart';

/// Claim investigation screen for Claims Adjusters (Component B & AI Agents).
///
/// Features:
/// - Claim overview & policy info.
/// - Required document checklist & uploaded evidence inspection.
/// - Trigger Coverage Validation (AI Workflow 1).
/// - Trigger AI Document Verification Agent (AI Workflow 2).
/// - Trigger Fraud & Risk Assessment Agent (AI Workflow 3).
/// - Calculate & submit Payout Proposal (AI Workflow 4: Safety Agent).
/// - Enforces rule: Adjusters CANNOT approve their own proposals or execute payments.
class ClaimInvestigationScreen extends StatefulWidget {
  final String claimId;

  const ClaimInvestigationScreen({super.key, required this.claimId});

  @override
  State<ClaimInvestigationScreen> createState() => _ClaimInvestigationScreenState();
}

class _ClaimInvestigationScreenState extends State<ClaimInvestigationScreen> {
  final ClaimService _claimService = ClaimService();
  final RiskService _riskService = RiskService();
  final PayoutService _payoutService = PayoutService();
  final ImagePicker _picker = ImagePicker();

  Claim? _claim;
  DocumentRequirements? _docRequirements;
  CoverageValidationResult? _coverageResult;
  DocumentVerificationResult? _docVerificationResult;
  StaffRiskAssessment? _riskAssessment;
  Payout? _payout;

  bool _loading = true;
  String? _error;

  bool _validatingCoverage = false;
  bool _verifyingDocs = false;
  bool _assessingRisk = false;
  bool _calculatingPayout = false;
  bool _uploadingDoc = false;

  final NumberFormat _currencyFormat = NumberFormat.currency(symbol: 'LKR ', decimalDigits: 2);

  @override
  void initState() {
    super.initState();
    _loadAll();
  }

  Future<void> _loadAll() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final claim = await _claimService.getClaim(widget.claimId);
      DocumentRequirements? reqs;
      try {
        reqs = await _claimService.getDocumentRequirements(widget.claimId);
      } catch (_) {}

      StaffRiskAssessment? risk;
      try {
        risk = await _riskService.getAssessment(widget.claimId);
      } catch (_) {}

      Payout? payout;
      try {
        payout = await _payoutService.getPayoutByClaimId(widget.claimId);
      } catch (_) {}

      if (!mounted) return;
      setState(() {
        _claim = claim;
        _docRequirements = reqs;
        _riskAssessment = risk;
        _payout = payout;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _loading = false;
      });
    }
  }

  Future<void> _runCoverageValidation() async {
    setState(() => _validatingCoverage = true);
    try {
      final result = await _claimService.validateCoverage(widget.claimId);
      if (!mounted) return;
      setState(() {
        _coverageResult = result;
        _validatingCoverage = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Policy coverage validation completed.')),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _validatingCoverage = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Coverage validation failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  Future<void> _runDocumentVerification() async {
    setState(() => _verifyingDocs = true);
    try {
      final result = await _claimService.startWorkflow(widget.claimId);
      if (!mounted) return;
      setState(() {
        _docVerificationResult = result;
        _verifyingDocs = false;
      });
      // Refresh requirements
      _loadDocRequirements();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result.aiUsed
              ? 'AI Document Verification completed (Gemini).'
              : 'Deterministic Document Verification completed.'),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _verifyingDocs = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Document verification failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  Future<void> _runRiskAssessment({bool includeAi = true}) async {
    setState(() => _assessingRisk = true);
    try {
      final result = await _riskService.assessClaim(
        widget.claimId,
        includeAiAnalysis: includeAi,
      );
      if (!mounted) return;
      setState(() {
        _riskAssessment = result;
        _assessingRisk = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result.aiUsed
              ? 'Risk assessment completed with AI Agent analysis.'
              : 'Deterministic risk assessment completed.'),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _assessingRisk = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Risk assessment failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  Future<void> _calculatePayout() async {
    setState(() => _calculatingPayout = true);
    try {
      final payout = await _payoutService.calculatePayout(widget.claimId);
      if (!mounted) return;
      setState(() {
        _payout = payout;
        _calculatingPayout = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Payout proposal calculated and submitted for review.'),
          backgroundColor: Colors.green,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _calculatingPayout = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Payout calculation failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  Future<void> _loadDocRequirements() async {
    try {
      final reqs = await _claimService.getDocumentRequirements(widget.claimId);
      if (mounted) setState(() => _docRequirements = reqs);
    } catch (_) {}
  }

  Future<void> _pickAndUploadDocument() async {
    final picked = await _picker.pickImage(source: ImageSource.gallery);
    if (picked == null || !mounted) return;

    final docType = await showDialog<String>(
      context: context,
      builder: (ctx) {
        String selected = 'Photos of Damage';
        return StatefulBuilder(
          builder: (dlgContext, setDlgState) => AlertDialog(
            title: const Text('Select Document Classification'),
            content: DropdownButton<String>(
              isExpanded: true,
              value: selected,
              items: const [
                DropdownMenuItem(value: 'Police Report', child: Text('Police Report')),
                DropdownMenuItem(value: 'Photos of Damage', child: Text('Photos of Damage')),
                DropdownMenuItem(value: 'Repair Estimate', child: Text('Repair Estimate')),
                DropdownMenuItem(value: 'Driver License', child: Text('Driver License')),
                DropdownMenuItem(value: 'Medical Report', child: Text('Medical Report')),
                DropdownMenuItem(value: 'Hospital Bills', child: Text('Hospital Bills')),
                DropdownMenuItem(value: 'Supporting Document', child: Text('Supporting Document')),
              ],
              onChanged: (val) {
                if (val != null) setDlgState(() => selected = val);
              },
            ),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
              ElevatedButton(onPressed: () => Navigator.pop(ctx, selected), child: const Text('Upload')),
            ],
          ),
        );
      },
    );

    if (docType == null) return;

    setState(() => _uploadingDoc = true);
    try {
      await _claimService.uploadDocument(
        claimId: widget.claimId,
        filePath: picked.path,
        documentType: docType,
      );
      await _loadAll();
      if (!mounted) return;
      setState(() => _uploadingDoc = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Document uploaded successfully.')),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _uploadingDoc = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Upload failed: $e'), backgroundColor: Colors.red),
      );
    }
  }

  void _showEscalateDialog() {
    final reasonCtrl = TextEditingController();
    String priority = 'High';

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (dlgContext, setDlg) => AlertDialog(
          title: const Text('Escalate to Fraud Case'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: reasonCtrl,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'Escalation Reason',
                  hintText: 'Enter justification for SIU investigation...',
                ),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: priority,
                items: const [
                  DropdownMenuItem(value: 'Low', child: Text('Low Priority')),
                  DropdownMenuItem(value: 'Medium', child: Text('Medium Priority')),
                  DropdownMenuItem(value: 'High', child: Text('High Priority')),
                  DropdownMenuItem(value: 'Critical', child: Text('Critical Priority')),
                ],
                onChanged: (v) => setDlg(() => priority = v ?? 'High'),
                decoration: const InputDecoration(labelText: 'Priority'),
              ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: Colors.red, foregroundColor: Colors.white),
              onPressed: () async {
                if (reasonCtrl.text.trim().isEmpty) return;
                Navigator.pop(ctx);
                if (_riskAssessment != null) {
                  try {
                    await _riskService.escalateClaim(
                      _riskAssessment!.id,
                      reason: reasonCtrl.text.trim(),
                      priority: priority,
                    );
                    await _loadAll();
                    if (!mounted) return;
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Claim successfully escalated to Fraud Case.')),
                    );
                  } catch (e) {
                    if (!mounted) return;
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Escalation failed: $e'), backgroundColor: Colors.red),
                    );
                  }
                }
              },
              child: const Text('Escalate'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_claim != null ? 'Claim ${_claim!.claimNumber}' : 'Claim Investigation'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadAll,
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: AppTheme.primaryTeal));
    }
    if (_error != null) {
      return ErrorRetryView(message: _error!, onRetry: _loadAll);
    }
    if (_claim == null) {
      return const Center(child: Text('Claim not found.'));
    }

    final claim = _claim!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Role & Policyholder Notice
          _buildAdjusterNoticeBanner(),
          const SizedBox(height: 16),

          // Claim Overview Card
          _buildClaimHeaderCard(claim),
          const SizedBox(height: 16),

          // Documents & Checklist Card
          _buildDocumentsSection(claim),
          const SizedBox(height: 20),

          // ── AI Workflow Action Center ─────────────────────────────────────
          const Text(
            'Agentic AI Workflows & Analysis',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
          ),
          const SizedBox(height: 12),

          // Workflow 1: Coverage Validation
          if (_coverageResult != null) ...[
            CoverageValidationCard(result: _coverageResult!),
            const SizedBox(height: 12),
          ] else
            _buildActionTriggerCard(
              title: '1. Policy Coverage Validation',
              description: 'Validate claim against policy limits, deductibles, active status and terms.',
              buttonLabel: 'Validate Coverage',
              isLoading: _validatingCoverage,
              icon: Icons.verified_user_outlined,
              onPressed: _runCoverageValidation,
            ),
          const SizedBox(height: 12),

          // Workflow 2: Document Verification
          if (_docVerificationResult != null) ...[
            DocumentVerificationCard(result: _docVerificationResult!),
            const SizedBox(height: 12),
          ] else
            _buildActionTriggerCard(
              title: '2. Document Verification Agent',
              description: 'Run AI checklist comparison, identify missing evidence, and detect inconsistencies.',
              buttonLabel: 'Start AI Document Verification',
              isLoading: _verifyingDocs,
              icon: Icons.document_scanner_outlined,
              buttonColor: Colors.indigo,
              onPressed: _runDocumentVerification,
            ),
          const SizedBox(height: 12),

          // Workflow 3: Fraud & Risk Assessment
          if (_riskAssessment != null) ...[
            RiskAssessmentCard(
              assessment: _riskAssessment!,
              onEscalate: _showEscalateDialog,
            ),
            const SizedBox(height: 12),
          ] else
            _buildActionTriggerCard(
              title: '3. Fraud & Risk Assessment Agent',
              description: 'Calculate multidimensional risk score, detect anomalies, and generate recommendation.',
              buttonLabel: 'Run AI Risk Assessment',
              isLoading: _assessingRisk,
              icon: Icons.shield_outlined,
              buttonColor: Colors.orange.shade800,
              onPressed: () => _runRiskAssessment(includeAi: true),
            ),
          const SizedBox(height: 16),

          // ── Workflow 4: Payout Proposal Preparation ───────────────────────
          const Text(
            'Financial Settlement & Payout Proposal',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
          ),
          const SizedBox(height: 12),

          if (_payout != null) ...[
            _buildPayoutSummaryCard(_payout!),
            if (_payout!.validationResult != null) ...[
              const SizedBox(height: 12),
              PayoutValidationCard(validation: _payout!.validationResult!),
            ],
            const SizedBox(height: 12),
            _buildApprovalNoticeBanner(_payout!),
          ] else
            _buildActionTriggerCard(
              title: '4. Prepare Payout Proposal (Safety Agent)',
              description: 'Authoritative backend calculation applying fixed deductible and coverage caps. Evaluated by AI Validation Agent.',
              buttonLabel: 'Calculate Payout Proposal',
              isLoading: _calculatingPayout,
              icon: Icons.payments_outlined,
              buttonColor: Colors.teal.shade700,
              onPressed: _calculatePayout,
            ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  Widget _buildAdjusterNoticeBanner() {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.blue.shade50,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.blue.shade200),
      ),
      child: const Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.admin_panel_settings_outlined, color: Colors.blue, size: 20),
          SizedBox(width: 10),
          Expanded(
            child: Text(
              'Claims Adjuster Investigation Workspace: Run verification and submit payout proposals. Final approval and payment execution require Underwriter / Admin authority.',
              style: TextStyle(fontSize: 12, color: Colors.blue, height: 1.3),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildClaimHeaderCard(Claim claim) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      claim.claimNumber,
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: AppTheme.deepNavy),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Type: ${claim.claimType}',
                      style: const TextStyle(fontSize: 13, color: AppTheme.textSecondary, fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
                StatusBadge(status: claim.status),
              ],
            ),
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _infoCol('Claimed Amount', _currencyFormat.format(claim.claimedAmount)),
                _infoCol('Incident Date', DateFormat('yyyy-MM-dd').format(claim.incidentDate)),
                _infoCol('Location', claim.incidentLocation.isNotEmpty ? claim.incidentLocation : 'N/A'),
              ],
            ),
            if (claim.description.isNotEmpty) ...[
              const SizedBox(height: 12),
              const Text('Description:', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
              const SizedBox(height: 2),
              Text(claim.description, style: const TextStyle(fontSize: 13, color: Colors.black87)),
            ],
            const SizedBox(height: 8),
            Text(
              'Policyholder ID: ${claim.policyHolderId ?? "N/A"}',
              style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDocumentsSection(Claim claim) {
    final docs = claim.documents;
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Row(
                    children: [
                      const Icon(Icons.attach_file, color: AppTheme.primaryTeal, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Submitted Documents (${docs.length})',
                          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                ),
                TextButton.icon(
                  onPressed: _uploadingDoc ? null : _pickAndUploadDocument,
                  icon: const Icon(Icons.add, size: 16),
                  label: const Text('Add', style: TextStyle(fontSize: 12)),
                ),
              ],
            ),
            if (_docRequirements != null) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: Colors.grey.shade100,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  'Checklist: ${_docRequirements!.uploadedRequiredCount} of ${_docRequirements!.requiredCount} required documents uploaded',
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                ),
              ),
            ],
            const SizedBox(height: 10),
            if (docs.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text('No documents uploaded yet.', style: TextStyle(color: AppTheme.textSecondary)),
              )
            else
              ...docs.map((d) => ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.insert_drive_file_outlined, color: AppTheme.primaryTeal),
                    title: Text(d.documentType, style: const TextStyle(fontWeight: FontWeight.w600)),
                    subtitle: Text('${d.fileName} • ${(d.fileSize / 1024).toStringAsFixed(1)} KB'),
                    trailing: Text(
                      DateFormat('MMM d').format(d.uploadedAt),
                      style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary),
                    ),
                  )),
          ],
        ),
      ),
    );
  }

  Widget _buildActionTriggerCard({
    required String title,
    required String description,
    required String buttonLabel,
    required bool isLoading,
    required IconData icon,
    Color? buttonColor,
    required VoidCallback onPressed,
  }) {
    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: buttonColor ?? AppTheme.primaryTeal, size: 20),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(fontSize: 14.5, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(description, style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary)),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 44,
              child: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: buttonColor ?? AppTheme.primaryTeal,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                onPressed: isLoading ? null : onPressed,
                child: isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : Text(buttonLabel, style: const TextStyle(fontWeight: FontWeight.w700)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPayoutSummaryCard(Payout payout) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Row(
                  children: [
                    Icon(Icons.account_balance_wallet_outlined, color: Colors.green, size: 20),
                    SizedBox(width: 8),
                    Text(
                      'Calculated Payout Proposal',
                      style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: AppTheme.deepNavy),
                    ),
                  ],
                ),
                StatusBadge(status: payout.statusDisplay),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _infoCol('Claimed', _currencyFormat.format(payout.approvedClaimAmount)),
                _infoCol(
                  payout.deductiblePercentage != null
                      ? 'Ded (${payout.deductiblePercentage!.toStringAsFixed(payout.deductiblePercentage! % 1 == 0 ? 0 : 2)}%)'
                      : 'Deductible',
                  '-${_currencyFormat.format(payout.deductible)}',
                ),
                _infoCol('Net Payout', _currencyFormat.format(payout.finalPayout), isHighlight: true),
              ],
            ),
            if (payout.explanation != null && payout.explanation!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text(
                payout.explanation!,
                style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildApprovalNoticeBanner(Payout payout) {
    final status = payout.statusDisplay;
    final isApproved = status == 'Approved' || status == 'Paid';

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: isApproved ? Colors.green.shade50 : Colors.amber.shade50,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: isApproved ? Colors.green.shade200 : Colors.amber.shade300),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            isApproved ? Icons.verified : Icons.hourglass_top,
            color: isApproved ? Colors.green.shade700 : Colors.amber.shade800,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              isApproved
                  ? 'Payout is $status. Authorized by ${payout.approvedBy ?? "Underwriting"}.'
                  : 'Proposal is currently $status. Awaiting Underwriter or Administrator decision.',
              style: TextStyle(
                fontSize: 12.5,
                fontWeight: FontWeight.w600,
                color: isApproved ? Colors.green.shade900 : Colors.amber.shade900,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _infoCol(String label, String value, {bool isHighlight = false}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
        const SizedBox(height: 2),
        Text(
          value,
          style: TextStyle(
            fontSize: isHighlight ? 16 : 14,
            fontWeight: FontWeight.w700,
            color: isHighlight ? Colors.green.shade700 : AppTheme.deepNavy,
          ),
        ),
      ],
    );
  }
}
