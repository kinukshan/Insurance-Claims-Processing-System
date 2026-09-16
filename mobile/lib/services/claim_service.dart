import '../models/claim.dart';
import '../models/claim_document.dart';
import 'api_service.dart';

/// Claim service — Component B (Member 2).
/// Policyholder-facing claim operations via ASP.NET Core.
class ClaimService {
  final ApiService _api;

  ClaimService({ApiService? apiService}) : _api = apiService ?? ApiService();

  /// POST /api/claims — Create a new claim (starts as Draft).
  Future<Claim> createClaim({
    required String policyId,
    required String claimType,
    required DateTime incidentDate,
    required String incidentLocation,
    required String description,
    required double claimedAmount,
  }) async {
    // Map claimType string to enum int value
    final claimTypeIndex = _claimTypeToIndex(claimType);

    final data = await _api.post('/claims', body: {
      'policyId': policyId,
      'claimType': claimTypeIndex,
      'incidentDate': incidentDate.toUtc().toIso8601String(),
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

  /// POST /api/claims/{id}/submit — Submit a draft claim.
  Future<Claim> submitClaim(String id) async {
    final data = await _api.post('/claims/$id/submit');
    return Claim.fromJson(data as Map<String, dynamic>);
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

  /// Maps claim type string to its enum int value for the backend.
  int _claimTypeToIndex(String claimType) {
    const types = [
      'Auto', 'Home', 'Health', 'Life', 'Travel', 'Property', 'Liability', 'Other'
    ];
    final index = types.indexOf(claimType);
    return index >= 0 ? index : 7; // Default to 'Other'
  }
}
