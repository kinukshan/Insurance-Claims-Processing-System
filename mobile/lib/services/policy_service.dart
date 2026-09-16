/// Policy service — Component A (Member 1).
/// Communicates with ASP.NET Core Web API for policy operations.
import '../models/policy.dart';
import 'api_service.dart';

class PolicyService {
  final ApiService _api = ApiService();

  /// Get all policies for the current policyholder.
  Future<List<Policy>> getPolicies(String policyholderId) async {
    final data = await _api.get('/policies/policyholder/$policyholderId');
    return (data as List<dynamic>)
        .map((json) => Policy.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Get a single policy by ID.
  Future<Policy> getPolicyById(String id) async {
    final data = await _api.get('/policies/$id');
    return Policy.fromJson(data as Map<String, dynamic>);
  }

  /// Get coverage details for a policy.
  Future<List<PolicyCoverage>> getCoverage(String policyId) async {
    final data = await _api.get('/policies/$policyId/coverage');
    return (data as List<dynamic>)
        .map((json) => PolicyCoverage.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Calculate premium for a policy.
  Future<Map<String, dynamic>> calculatePremium(String policyId) async {
    final data = await _api.post('/policies/$policyId/calculate-premium');
    return data as Map<String, dynamic>;
  }
}
