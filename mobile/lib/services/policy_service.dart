// Policy service — Component A (Member 1).
// Communicates with ASP.NET Core Web API for policy operations.
import '../models/policy.dart';
import 'api_service.dart';

class PolicyService {
  final ApiService _api;

  PolicyService({ApiService? apiService})
      : _api = apiService ?? ApiService.shared;

  /// Get policies for the authenticated Policyholder.
  ///
  /// GET /api/policies/my
  Future<List<Policy>> getMyPolicies() async {
    final data = await _api.get('/policies/my');
    return (data as List<dynamic>)
        .map((json) => Policy.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Get all policies for the given policyholder ID.
  ///
  /// GET /api/policies/policyholder/{policyholderId}
  Future<List<Policy>> getPolicies(String policyholderId) async {
    final data = await _api.get('/policies/policyholder/$policyholderId');
    return (data as List<dynamic>)
        .map((json) => Policy.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Get a single policy by ID.
  ///
  /// GET /api/policies/{id}
  Future<Policy> getPolicyById(String id) async {
    final data = await _api.get('/policies/$id');
    return Policy.fromJson(data as Map<String, dynamic>);
  }

  /// Get coverage details for a policy.
  ///
  /// GET /api/policies/{id}/coverage
  Future<List<PolicyCoverage>> getCoverage(String policyId) async {
    final data = await _api.get('/policies/$policyId/coverage');
    return (data as List<dynamic>)
        .map((json) => PolicyCoverage.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Calculate premium for a policy.
  ///
  /// POST /api/policies/{id}/calculate-premium
  Future<Map<String, dynamic>> calculatePremium(String policyId) async {
    final data = await _api.post('/policies/$policyId/calculate-premium');
    return data as Map<String, dynamic>;
  }

  /// Get active policy types (reference products data).
  ///
  /// GET /api/policytypes
  Future<List<PolicyType>> getPolicyTypes() async {
    final data = await _api.get('/policytypes');
    return (data as List<dynamic>)
        .map((json) => PolicyType.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Create a new policy.
  ///
  /// POST /api/policies
  Future<Policy> createPolicy(CreatePolicyRequest request) async {
    final data = await _api.post('/policies', body: request.toJson());
    return Policy.fromJson(data as Map<String, dynamic>);
  }

  /// Get all policies across the organization (Staff: Admin, Underwriter, ClaimsAdjuster).
  ///
  /// GET /api/policies
  Future<List<Policy>> getAllPolicies() async {
    final data = await _api.get('/policies');
    return (data as List<dynamic>)
        .map((json) => Policy.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Update an existing policy (Underwriter: coverage limit, expiry date, exclusions; Admin: also status).
  ///
  /// PUT /api/policies/{id}
  Future<Policy> updatePolicy(String id, {
    double? coverageLimit,
    DateTime? expiryDate,
    String? exclusions,
    String? status,
  }) async {
    final body = <String, dynamic>{};
    if (coverageLimit != null) body['coverageLimit'] = coverageLimit;
    if (expiryDate != null) body['expiryDate'] = expiryDate.toIso8601String();
    if (exclusions != null) body['exclusions'] = exclusions;
    if (status != null) body['status'] = status;

    final data = await _api.put('/policies/$id', body: body);
    return Policy.fromJson(data as Map<String, dynamic>);
  }

  /// Renew a policy.
  ///
  /// POST /api/policies/{id}/renew
  Future<Map<String, dynamic>> renewPolicy(String id) async {
    final data = await _api.post('/policies/$id/renew');
    return data as Map<String, dynamic>;
  }

  /// Delete a draft policy.
  ///
  /// DELETE /api/policies/{id}
  Future<void> deletePolicy(String id) async {
    await _api.delete('/policies/$id');
  }
}
