import 'package:flutter/material.dart';
import '../../models/ai_workflow_models.dart';
import '../../services/risk_service.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';
import '../../widgets/ai_workflow_widgets.dart';
import 'claim_investigation_screen.dart';

/// Risk intelligence screen for Claims Adjusters and Risk Staff.
/// Authoritative view of fraud flags, anomaly indicators, and risk scores.
class RiskIntelligenceScreen extends StatefulWidget {
  const RiskIntelligenceScreen({super.key});

  @override
  State<RiskIntelligenceScreen> createState() => _RiskIntelligenceScreenState();
}

class _RiskIntelligenceScreenState extends State<RiskIntelligenceScreen> {
  final RiskService _riskService = RiskService();

  List<StaffRiskAssessment> _allAssessments = [];
  List<StaffRiskAssessment> _flaggedClaims = [];
  bool _loading = true;
  String? _error;
  int _tabIndex = 0; // 0 = Flagged, 1 = All Assessments

  @override
  void initState() {
    super.initState();
    _loadRiskData();
  }

  Future<void> _loadRiskData() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final all = await _riskService.getAllAssessments();
      final flagged = await _riskService.getFlaggedClaims();

      if (!mounted) return;
      setState(() {
        _allAssessments = all;
        _flaggedClaims = flagged;
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Risk & Fraud Intelligence'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadRiskData,
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
      return ErrorRetryView(message: _error!, onRetry: _loadRiskData);
    }

    final highRiskCount = _allAssessments
        .where((a) => a.riskLevel.toLowerCase() == 'high' || a.riskScore >= 60)
        .length;

    final displayedList = _tabIndex == 0 ? _flaggedClaims : _allAssessments;

    return RefreshIndicator(
      onRefresh: _loadRiskData,
      child: Column(
        children: [
          // KPI Metric Header
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            color: Colors.grey.shade50,
            child: Row(
              children: [
                _kpiCard('Flagged Cases', '${_flaggedClaims.length}', Colors.red.shade700),
                const SizedBox(width: 8),
                _kpiCard('High Risk', '$highRiskCount', Colors.orange.shade800),
                const SizedBox(width: 8),
                _kpiCard('Total Assessed', '${_allAssessments.length}', AppTheme.deepNavy),
              ],
            ),
          ),

          // Segmented Tab Selector
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Row(
              children: [
                Expanded(
                  child: ChoiceChip(
                    label: Center(
                      child: Text('Flagged Claims (${_flaggedClaims.length})'),
                    ),
                    selected: _tabIndex == 0,
                    selectedColor: Colors.red.shade100,
                    onSelected: (val) {
                      if (val) setState(() => _tabIndex = 0);
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: ChoiceChip(
                    label: Center(
                      child: Text('All Assessments (${_allAssessments.length})'),
                    ),
                    selected: _tabIndex == 1,
                    selectedColor: AppTheme.primaryTeal.withValues(alpha: 0.2),
                    onSelected: (val) {
                      if (val) setState(() => _tabIndex = 1);
                    },
                  ),
                ),
              ],
            ),
          ),

          // Assessment List
          Expanded(
            child: displayedList.isEmpty
                ? EmptyStateView(
                    icon: Icons.shield_outlined,
                    title: _tabIndex == 0 ? 'No Flagged Claims' : 'No Risk Assessments',
                    description: _tabIndex == 0
                        ? 'No active claims have unresolved fraud flags.'
                        : 'No claims have been assessed yet.',
                  )
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: displayedList.length,
                    itemBuilder: (context, index) {
                      final item = displayedList[index];
                      return _buildAssessmentListItem(item);
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _kpiCard(String label, String value, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.grey.shade200),
        ),
        child: Column(
          children: [
            Text(value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color)),
            const SizedBox(height: 2),
            Text(
              label,
              style: const TextStyle(fontSize: 10.5, color: AppTheme.textSecondary),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAssessmentListItem(StaffRiskAssessment item) {
    final isHigh = item.riskLevel.toLowerCase() == 'high' || item.riskScore >= 60;
    final color = isHigh ? Colors.red : (item.riskScore >= 30 ? Colors.orange : Colors.green);

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 1.5,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          await Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => ClaimInvestigationScreen(claimId: item.claimId),
            ),
          );
          _loadRiskData();
        },
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    item.claimNumber ?? 'Claim ID: ${item.claimId.substring(0, 8)}...',
                    style: const TextStyle(
                      fontSize: 15.5,
                      fontWeight: FontWeight.w800,
                      color: AppTheme.deepNavy,
                    ),
                  ),
                  AiWorkflowBadge(
                    aiUsed: item.aiUsed,
                    aiModel: item.aiModel,
                    fallbackUsed: item.fallbackUsed,
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.12),
                      shape: BoxShape.circle,
                      border: Border.all(color: color, width: 2),
                    ),
                    child: Center(
                      child: Text(
                        '${item.riskScore.toInt()}',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w800,
                          color: color,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            StatusBadge(status: item.riskLevel, customColor: color),
                            const SizedBox(width: 6),
                            StatusBadge(status: item.recommendation),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${item.fraudFlagCount} fraud flags • ${item.hasFraudCase ? "Escalated" : "Not Escalated"}',
                          style: const TextStyle(fontSize: 11.5, color: AppTheme.textSecondary),
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: AppTheme.textSecondary),
                ],
              ),
              if (item.summary.isNotEmpty) ...[
                const SizedBox(height: 10),
                Text(
                  item.summary,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 12, color: Colors.black87),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
