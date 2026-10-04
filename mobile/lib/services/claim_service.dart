import '../models/claim.dart';
import '../models/claim_document.dart';
import '../models/document_requirement.dart';
import '../models/ai_workflow_models.dart';
import 'api_service.dart';

/// Claim service — Component B (Member 2).
/// Policyholder-facing claim operations via ASP.NET Core.
///
/// MODIFICATION (Phase 3): Added document requirements, draft management,
/// withdrawal, and fixed ClaimType enum mapping to include Motor (8).
class ClaimService {
  final ApiService _api;

  ClaimService({ApiService? apiService}) : _api = apiService ?? ApiService.shared;

  /// Backend ClaimType enum values.
  /// Must exactly match: Auto=0, Home=1, Health=2, Life=3, Travel=4,
  /// Property=5, Liability=6, Other=7, Motor=8
  static const Map<String, int> claimTypeIndices = {
    'Auto': 0,
    'Home': 1,
    'Health': 2,
    'Life': 3,
    'Travel': 4,
    'Property': 5,
    'Liability': 6,
    'Other': 7,
    'Motor': 8,
  };

  /// Authoritative policy-to-claim-type compatibility map.
  /// Mirrors backend PolicyClaimCompatibility.IsCompatible exactly.
  static const Map<String, List<String>> policyClaimCompatibility = {
    'Motor Insurance': ['Motor', 'Auto'],
    'Health Insurance': ['Health'],
    'Home Insurance': ['Property', 'Home'],
    'Life Insurance': ['Life'],
  };

  /// Backend document checklist per claim type.
  /// Mirrors backend DocumentChecklistValidator.RequiredDocumentsByClaimType.
  static const Map<String, List<String>> requiredDocumentsByClaimType = {
    'Auto': ['Police Report', 'Photos of Damage', 'Repair Estimate', 'Driver License'],
    'Motor': ['Police Report', 'Photos of Damage', 'Repair Estimate', 'Driver License'],
    'Home': ['Photos of Damage', 'Repair Estimate', 'Property Deed'],
    'Health': ['Medical Report', 'Hospital Bills', 'Prescription', 'Doctor Referral'],
    'Life': ['Death Certificate', 'Policy Document', 'Beneficiary / Nominee Identification', 'Claim Form'],
    'Travel': ['Travel Itinerary', 'Receipts', 'Incident Report'],
    'Property': ['Photos of Damage', 'Repair Estimate', 'Property Valuation'],
    'Liability': ['Incident Report', 'Third Party Claim', 'Legal Notice'],
    'Other': ['Supporting Document'],
  };

  /// Returns compatible claim types for a given policy type name.
  /// Normalizes the policy type name using the same aliases as the backend.
  static List<String> getCompatibleClaimTypes(String? policyTypeName) {
    if (policyTypeName == null || policyTypeName.trim().isEmpty) return [];
    final normalized = _normalizePolicyType(policyTypeName);
    return policyClaimCompatibility[normalized] ?? [];
  }

  /// Returns the required document types for a given claim type.
  static List<String> getRequiredDocuments(String claimType) {
    return requiredDocumentsByClaimType[claimType] ?? ['Supporting Document'];
  }

  /// Normalize policy type name using the same rules as backend
  /// PolicyClaimCompatibility.NormalizePolicyType.
  static String _normalizePolicyType(String rawName) {
    final trimmed = rawName.trim().toLowerCase();
    if (['motor insurance', 'motor', 'auto', 'auto insurance', 'comprehensive auto']
        .contains(trimmed)) {
      return 'Motor Insurance';
    }
    if (['health insurance', 'health'].contains(trimmed)) {
      return 'Health Insurance';
    }
    if (['home insurance', 'home / property insurance', 'property insurance', 'property', 'home']
        .contains(trimmed)) {
      return 'Home Insurance';
    }
    if (['life insurance', 'life'].contains(trimmed)) {
      return 'Life Insurance';
    }
    return rawName.trim();
  }

  /// POST /api/claims — Create a new claim (starts as Draft).
  Future<Claim> createClaim({
    required String policyId,
    required String claimType,
    required DateTime incidentDate,
    required String incidentLocation,
    required String description,
    required double claimedAmount,
  }) async {
    final claimTypeIndex = _claimTypeToIndex(claimType);

    final data = await _api.post('/claims', body: {
      'policyId': policyId,
      'claimType': claimTypeIndex,
      'incidentDate': _formatDateOnly(incidentDate),
      'incidentLocation': incidentLocation,
      'description': description,
      'claimedAmount': claimedAmount,
    });
    return Claim.fromJson(data as Map<String, dynamic>);
  }

  /// GET /api/claims/my-claims — Get current user's claims.
  Future<List<Claim>> getMyClaims() async {
    final data = await _api.get('/claims/my-claims');
    return (data as List<dynamic>)
        .map((c) => Claim.fromJson(c as Map<String, dynamic>))
        .toList();
  }

  /// GET /api/claims/{id} — Get a claim by ID.
  Future<Claim> getClaim(String id) async {
    final data = await _api.get('/claims/$id');
    return Claim.fromJson(data as Map<String, dynamic>);
  }

  /// PUT /api/claims/{id} — Update a draft claim.
  Future<Claim> updateClaim(String id, {
    String? description,
    String? incidentLocation,
    double? claimedAmount,
    DateTime? incidentDate,
  }) async {
    final body = <String, dynamic>{};
    if (description != null) body['description'] = description;
    if (incidentLocation != null) body['incidentLocation'] = incidentLocation;
    if (claimedAmount != null) body['claimedAmount'] = claimedAmount;
    if (incidentDate != null) body['incidentDate'] = _formatDateOnly(incidentDate);

    final data = await _api.put('/claims/$id', body: body);
    return Claim.fromJson(data as Map<String, dynamic>);
  }

  /// POST /api/claims/{id}/submit — Submit a draft claim.
  Future<Claim> submitClaim(String id) async {
    final data = await _api.post('/claims/$id/submit');
    return Claim.fromJson(data as Map<String, dynamic>);
  }

  /// POST /api/claims/{id}/withdraw — Withdraw a submitted claim.
  Future<Claim> withdrawClaim(String id) async {
    final data = await _api.post('/claims/$id/withdraw');
    return Claim.fromJson(data as Map<String, dynamic>);
  }

  /// DELETE /api/claims/{id} — Hard delete a draft claim.
  Future<void> deleteClaim(String id) async {
    await _api.delete('/claims/$id');
  }

  /// POST /api/claims/{id}/documents — Upload a document (evidence).
  Future<ClaimDocument> uploadDocument({
    required String claimId,
    required String filePath,
    required String documentType,
  }) async {
    final data = await _api.uploadFile(
      '/claims/$claimId/documents',
      filePath: filePath,
      fieldName: 'file',
      fields: {'documentType': documentType},
    );
    return ClaimDocument.fromJson(data as Map<String, dynamic>);
  }

  /// GET /api/claims/{id}/documents — Get documents for a claim.
  Future<List<ClaimDocument>> getDocuments(String claimId) async {
    final data = await _api.get('/claims/$claimId/documents');
    return (data as List<dynamic>)
        .map((d) => ClaimDocument.fromJson(d as Map<String, dynamic>))
        .toList();
  }

  /// DELETE /api/claims/{claimId}/documents/{documentId} — Delete a document.
  Future<void> deleteDocument(String claimId, String documentId) async {
    await _api.delete('/claims/$claimId/documents/$documentId');
  }

  /// GET /api/claims/{id}/document-requirements — Get required documents and upload status.
  Future<DocumentRequirements> getDocumentRequirements(String claimId) async {
    final data = await _api.get('/claims/$claimId/document-requirements');
    return DocumentRequirements.fromJson(data as Map<String, dynamic>);
  }

  /// GET /api/claims — Get all claims across system (Staff) with optional filters.
  Future<List<Claim>> getAllClaims({String? status, String? search}) async {
    final params = <String>[];
    if (status != null && status.isNotEmpty) params.add('status=$status');
    if (search != null && search.isNotEmpty) params.add('search=$search');
    final query = params.isNotEmpty ? '?${params.join('&')}' : '';

    final data = await _api.get('/claims$query');
    return (data as List<dynamic>)
        .map((c) => Claim.fromJson(c as Map<String, dynamic>))
        .toList();
  }

  /// POST /api/claims/{id}/validate-coverage — AI Workflow 1: Validate claim against policy coverage.
  Future<CoverageValidationResult> validateCoverage(String claimId) async {
    final data = await _api.post('/claims/$claimId/validate-coverage');
    return CoverageValidationResult.fromJson(data as Map<String, dynamic>);
  }

  /// POST /api/claims/{id}/start-workflow — AI Workflow 2: Start document verification workflow.
  Future<DocumentVerificationResult> startWorkflow(String claimId) async {
    final data = await _api.post('/claims/$claimId/start-workflow');
    return DocumentVerificationResult.fromJson(data as Map<String, dynamic>);
  }

  /// Maps claim type string to its enum int value for the backend.
  /// Includes Motor (8) which was missing in the original mapping.
  int _claimTypeToIndex(String claimType) {
    return claimTypeIndices[claimType] ?? 7; // Default to 'Other'
  }

  String _formatDateOnly(DateTime date) {
    final y = date.year.toString().padLeft(4, '0');
    final m = date.month.toString().padLeft(2, '0');
    final d = date.day.toString().padLeft(2, '0');
    return '$y-$m-$d';
  }
}
