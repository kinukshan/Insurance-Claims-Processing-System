import 'dart:convert';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:insurance_claims_mobile/models/claim.dart';
import 'package:insurance_claims_mobile/models/claim_document.dart';
import 'package:insurance_claims_mobile/models/payout.dart';
import 'package:insurance_claims_mobile/models/policy.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/services/claim_service.dart';
import 'package:insurance_claims_mobile/services/payout_service.dart';
import 'package:insurance_claims_mobile/services/policy_service.dart';

// ─────────────────────── Test Data ───────────────────────

/// Auth response matching backend AuthResponse DTO.
const _loginResponse = {
  'token': 'test.jwt.token.header.payload.signature',
  'userId': '11111111-1111-1111-1111-111111111111',
  'firstName': 'Kasun',
  'lastName': 'Perera',
  'email': 'kasun@example.com',
  'role': 'Policyholder',
};

/// User profile response matching backend UserProfileResponse DTO.
const _profileResponse = {
  'userId': '11111111-1111-1111-1111-111111111111',
  'email': 'kasun@example.com',
  'firstName': 'Kasun',
  'lastName': 'Perera',
  'role': 'Policyholder',
  'isActive': true,
};

/// Claim response matching backend ClaimResponseDto.
final _claimResponse = {
  'id': 'claim-001',
  'policyId': 'policy-001',
  'policyHolderId': '11111111-1111-1111-1111-111111111111',
  'claimNumber': 'CLM-2026-001',
  'claimType': 'Motor',
  'description': 'Fender bender on Galle Road',
  'claimedAmount': 45000.0,
  'incidentDate': '2026-09-15',
  'incidentLocation': 'Colombo 03',
  'status': 'Submitted',
  'submittedAt': '2026-09-15T10:00:00Z',
  'createdAt': '2026-09-15T09:00:00Z',
  'updatedAt': '2026-09-15T10:00:00Z',
  'documents': <Map<String, dynamic>>[],
};

/// A list of claims for getMyClaims.
final _claimsListResponse = [_claimResponse];

/// Policy response matching backend PolicyResponseDto.
final _policyResponse = {
  'id': 'policy-001',
  'policyNumber': 'POL-2026-001',
  'policyholderId': '11111111-1111-1111-1111-111111111111',
  'policyTypeId': 'pt-motor',
  'policyTypeName': 'Motor Insurance',
  'coverageLimit': 500000.0,
  'premium': 12000.0,
  'deductible': 10000.0,
  'deductiblePercentage': 5.0,
  'startDate': '2026-01-01',
  'expiryDate': '2027-01-01',
  'status': 'Active',
  'renewalStatus': 'None',
  'isExpired': false,
  'canRenew': false,
  'createdAt': '2026-01-01T00:00:00Z',
  'updatedAt': '2026-01-01T00:00:00Z',
};

/// Payout response matching backend PayoutResponseDto.
final _payoutResponse = {
  'id': 'payout-001',
  'claimId': 'claim-001',
  'claimNumber': 'CLM-2026-001',
  'approvedClaimAmount': 45000.0,
  'coverageLimit': 500000.0,
  'deductible': 2250.0,
  'deductiblePercentage': 5.0,
  'eligibleAmount': 45000.0,
  'proposedPayout': 42750.0,
  'finalPayout': 42750.0,
  'explanation': 'Standard payout calculation',
  'status': 'PendingApproval',
  'statusDisplay': 'Pending Approval',
  'createdAt': '2026-09-20T12:00:00Z',
  'updatedAt': '2026-09-20T12:00:00Z',
};

/// Claim document response matching backend ClaimDocumentDto.
final _documentResponse = {
  'id': 'doc-001',
  'claimId': 'claim-001',
  'fileName': 'police_report.pdf',
  'fileUrl': 'http://localhost/files/police_report.pdf',
  'documentType': 'Police Report',
  'contentType': 'application/pdf',
  'fileSize': 204800,
  'uploadedAt': '2026-09-15T10:30:00Z',
  'verificationStatus': 'Pending',
};

// ─────────────────────── Mock Secure Storage ───────────────────────

void setupMockSecureStorage() {
  final storage = <String, String>{};

  TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
      .setMockMethodCallHandler(
    const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
    (MethodCall methodCall) async {
      switch (methodCall.method) {
        case 'write':
          final args = methodCall.arguments as Map;
          storage[args['key'] as String] = args['value'] as String;
          return null;
        case 'read':
          final args = methodCall.arguments as Map;
          return storage[args['key'] as String];
        case 'delete':
          final args = methodCall.arguments as Map;
          storage.remove(args['key'] as String);
          return null;
        case 'readAll':
          return storage;
        case 'deleteAll':
          storage.clear();
          return null;
        case 'containsKey':
          final args = methodCall.arguments as Map;
          return storage.containsKey(args['key'] as String);
        default:
          return null;
      }
    },
  );
}

// ─────────────────────── Helper: Create services with mock client ─────────

ApiService _createApiService(MockClient client) {
  final api = ApiService(baseUrl: 'http://localhost/api', client: client);
  ApiService.shared = api;
  return api;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    setupMockSecureStorage();
  });

  // ═════════════════════════════════════════════════════════════════════
  // ApiService Tests
  // ═════════════════════════════════════════════════════════════════════

  group('ApiService', () {
    test('GET returns parsed JSON on success', () async {
      final client = MockClient((request) async {
        expect(request.method, 'GET');
        expect(request.url.path, '/api/test');
        return http.Response(jsonEncode({'key': 'value'}), 200);
      });
      final api = _createApiService(client);

      final result = await api.get('/test');

      expect(result, isA<Map<String, dynamic>>());
      expect(result['key'], 'value');
    });

    test('POST sends JSON body and returns parsed response', () async {
      final client = MockClient((request) async {
        expect(request.method, 'POST');
        expect(request.headers['Content-Type'], 'application/json');
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['email'], 'user@test.com');
        return http.Response(jsonEncode({'id': '123'}), 201);
      });
      final api = _createApiService(client);

      final result = await api.post('/resource', body: {'email': 'user@test.com'});

      expect(result['id'], '123');
    });

    test('PUT sends JSON body correctly', () async {
      final client = MockClient((request) async {
        expect(request.method, 'PUT');
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['name'], 'updated');
        return http.Response(jsonEncode({'name': 'updated'}), 200);
      });
      final api = _createApiService(client);

      final result = await api.put('/resource/1', body: {'name': 'updated'});

      expect(result['name'], 'updated');
    });

    test('DELETE returns null on 204', () async {
      final client = MockClient((request) async {
        expect(request.method, 'DELETE');
        return http.Response('', 204);
      });
      final api = _createApiService(client);

      final result = await api.delete('/resource/1');

      expect(result, isNull);
    });

    test('sets Authorization header when auth token is configured', () async {
      final client = MockClient((request) async {
        expect(request.headers['Authorization'], 'Bearer my-jwt-token');
        return http.Response(jsonEncode({'ok': true}), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('my-jwt-token');

      await api.get('/protected');
    });

    test('does not include Authorization header when no token set', () async {
      final client = MockClient((request) async {
        expect(request.headers.containsKey('Authorization'), false);
        return http.Response(jsonEncode({'ok': true}), 200);
      });
      final api = _createApiService(client);

      await api.get('/public');
    });

    test('clearAuthToken removes the auth header', () async {
      final api = _createApiService(MockClient((r) async => http.Response('{}', 200)));
      api.setAuthToken('token');
      expect(api.hasAuthToken, true);

      api.clearAuthToken();
      expect(api.hasAuthToken, false);
    });

    test('throws ApiException with 401 on unauthorized', () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Token expired'}),
          401,
        );
      });
      final api = _createApiService(client);

      expect(
        () => api.get('/protected'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 401)
            .having((e) => e.isUnauthorized, 'isUnauthorized', true)),
      );
    });

    test('throws ApiException with 403 on forbidden', () async {
      final client = MockClient((request) async {
        return http.Response('', 403);
      });
      final api = _createApiService(client);

      expect(
        () => api.get('/admin-only'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 403)
            .having((e) => e.isForbidden, 'isForbidden', true)
            .having((e) => e.message, 'message',
                'Access denied. You do not have permission to perform this action.')),
      );
    });

    test('throws ApiException with 404 on not found', () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Claim not found'}),
          404,
        );
      });
      final api = _createApiService(client);

      expect(
        () => api.get('/claims/nonexistent'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 404)
            .having((e) => e.isNotFound, 'isNotFound', true)),
      );
    });

    test('throws ApiException with 400 on validation error', () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({
            'errors': {'email': ['Email is already taken']}
          }),
          400,
        );
      });
      final api = _createApiService(client);

      expect(
        () => api.post('/auth/register', body: {'email': 'dup@test.com'}),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having((e) => e.isValidationError, 'isValidationError', true)),
      );
    });

    test('throws ApiException with 500 on server error', () async {
      final client = MockClient((request) async {
        return http.Response('Internal Server Error', 500);
      });
      final api = _createApiService(client);

      expect(
        () => api.get('/crash'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 500)
            .having((e) => e.isServerError, 'isServerError', true)
            .having((e) => e.message, 'message',
                'A server error occurred. Please try again later.')),
      );
    });

    test('throws ApiException on network error', () async {
      final client = MockClient((request) async {
        throw Exception('Connection refused');
      });
      final api = _createApiService(client);

      expect(
        () => api.get('/offline'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 0)
            .having((e) => e.isNetworkError, 'isNetworkError', true)),
      );
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // AuthService Tests
  // ═════════════════════════════════════════════════════════════════════

  group('AuthService', () {
    test('login success returns AuthResult with user and token', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/login') {
          return http.Response(jsonEncode(_loginResponse), 200);
        }
        return http.Response('', 404);
      });
      final api = _createApiService(client);
      final authService = AuthService(api: api);

      final result = await authService.login(
        email: 'kasun@example.com',
        password: 'Password123!',
      );

      expect(result.isSuccess, true);
      expect(result.user, isNotNull);
      expect(result.user!.email, 'kasun@example.com');
      expect(result.user!.firstName, 'Kasun');
      expect(result.user!.role, 'Policyholder');
      expect(result.token, 'test.jwt.token.header.payload.signature');
      expect(api.hasAuthToken, true);
    });

    test('login with invalid credentials returns failure', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/login') {
          return http.Response(
            jsonEncode({'error': 'Invalid credentials'}),
            401,
          );
        }
        return http.Response('', 404);
      });
      final api = _createApiService(client);
      final authService = AuthService(api: api);

      final result = await authService.login(
        email: 'wrong@example.com',
        password: 'wrongpassword',
      );

      expect(result.isSuccess, false);
      expect(result.error, 'Invalid email or password.');
      expect(result.user, isNull);
    });

    test('login with network error returns failure', () async {
      final client = MockClient((request) async {
        throw Exception('No internet');
      });
      final api = _createApiService(client);
      final authService = AuthService(api: api);

      final result = await authService.login(
        email: 'user@example.com',
        password: 'password',
      );

      expect(result.isSuccess, false);
      expect(result.error, isNotNull);
    });

    test('register success returns AuthResult with user', () async {
      final registerResponse = {
        'token': 'new.user.jwt.token',
        'userId': '22222222-2222-2222-2222-222222222222',
        'firstName': 'Nimal',
        'lastName': 'Silva',
        'email': 'nimal@example.com',
        'role': 'Policyholder',
      };

      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/register') {
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          expect(body['firstName'], 'Nimal');
          expect(body['lastName'], 'Silva');
          expect(body['email'], 'nimal@example.com');
          expect(body['confirmPassword'], isNotNull);
          return http.Response(jsonEncode(registerResponse), 200);
        }
        return http.Response('', 404);
      });
      final api = _createApiService(client);
      final authService = AuthService(api: api);

      final result = await authService.register(
        firstName: 'Nimal',
        lastName: 'Silva',
        email: 'nimal@example.com',
        password: 'Password123!',
        confirmPassword: 'Password123!',
      );

      expect(result.isSuccess, true);
      expect(result.user!.firstName, 'Nimal');
      expect(result.user!.email, 'nimal@example.com');
      expect(result.user!.role, 'Policyholder');
    });

    test('register with duplicate email returns failure', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/register') {
          return http.Response(
            jsonEncode({'error': 'Email already registered'}),
            400,
          );
        }
        return http.Response('', 404);
      });
      final api = _createApiService(client);
      final authService = AuthService(api: api);

      final result = await authService.register(
        firstName: 'Test',
        lastName: 'User',
        email: 'existing@example.com',
        password: 'Password123!',
        confirmPassword: 'Password123!',
      );

      expect(result.isSuccess, false);
      expect(result.error, isNotNull);
    });

    test('getCurrentUser returns User on success', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/me') {
          return http.Response(jsonEncode(_profileResponse), 200);
        }
        return http.Response('', 404);
      });
      final api = _createApiService(client);
      api.setAuthToken('valid-token');
      final authService = AuthService(api: api);

      final user = await authService.getCurrentUser();

      expect(user, isNotNull);
      expect(user!.email, 'kasun@example.com');
      expect(user.isActive, true);
      expect(user.isPolicyholder, true);
    });

    test('getCurrentUser returns null on 401', () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Unauthorized'}),
          401,
        );
      });
      final api = _createApiService(client);
      api.setAuthToken('expired-token');
      final authService = AuthService(api: api);

      final user = await authService.getCurrentUser();

      expect(user, isNull);
    });

    test('logout clears auth token', () async {
      final client = MockClient((request) async => http.Response('{}', 200));
      final api = _createApiService(client);
      api.setAuthToken('token-to-clear');
      final authService = AuthService(api: api);

      expect(api.hasAuthToken, true);
      await authService.logout();
      expect(api.hasAuthToken, false);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // ClaimService Tests
  // ═════════════════════════════════════════════════════════════════════

  group('ClaimService', () {
    test('getMyClaims returns list of claims', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims/my-claims');
        return http.Response(jsonEncode(_claimsListResponse), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final claims = await claimService.getMyClaims();

      expect(claims, isA<List<Claim>>());
      expect(claims.length, 1);
      expect(claims.first.claimNumber, 'CLM-2026-001');
      expect(claims.first.claimType, 'Motor');
      expect(claims.first.claimedAmount, 45000.0);
      expect(claims.first.status, 'Submitted');
    });

    test('getClaim returns a single claim by ID', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims/claim-001');
        return http.Response(jsonEncode(_claimResponse), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final claim = await claimService.getClaim('claim-001');

      expect(claim.id, 'claim-001');
      expect(claim.description, 'Fender bender on Galle Road');
      expect(claim.incidentLocation, 'Colombo 03');
    });

    test('createClaim sends correct body and returns claim', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims');
        expect(request.method, 'POST');
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['policyId'], 'policy-001');
        expect(body['claimType'], 8); // Motor = 8
        expect(body['description'], 'Test claim');
        return http.Response(jsonEncode(_claimResponse), 201);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final claim = await claimService.createClaim(
        policyId: 'policy-001',
        claimType: 'Motor',
        incidentDate: DateTime(2026, 9, 15),
        incidentLocation: 'Colombo 03',
        description: 'Test claim',
        claimedAmount: 45000.0,
      );

      expect(claim, isA<Claim>());
      expect(claim.claimNumber, 'CLM-2026-001');
    });

    test('submitClaim posts to correct endpoint', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims/claim-001/submit');
        expect(request.method, 'POST');
        return http.Response(
          jsonEncode({..._claimResponse, 'status': 'Submitted'}),
          200,
        );
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final claim = await claimService.submitClaim('claim-001');

      expect(claim.status, 'Submitted');
    });

    test('withdrawClaim posts to correct endpoint', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims/claim-001/withdraw');
        return http.Response(
          jsonEncode({..._claimResponse, 'status': 'Withdrawn'}),
          200,
        );
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final claim = await claimService.withdrawClaim('claim-001');

      expect(claim.status, 'Withdrawn');
    });

    test('deleteClaim calls DELETE on correct endpoint', () async {
      final client = MockClient((request) async {
        expect(request.method, 'DELETE');
        expect(request.url.path, '/api/claims/claim-001');
        return http.Response('', 204);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      // Should not throw
      await claimService.deleteClaim('claim-001');
    });

    test('getDocuments returns list of claim documents', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/claims/claim-001/documents');
        return http.Response(jsonEncode([_documentResponse]), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      final docs = await claimService.getDocuments('claim-001');

      expect(docs, isA<List<ClaimDocument>>());
      expect(docs.length, 1);
      expect(docs.first.fileName, 'police_report.pdf');
      expect(docs.first.documentType, 'Police Report');
      expect(docs.first.fileSize, 204800);
    });

    test('getMyClaims throws ApiException on server error', () async {
      final client = MockClient((request) async {
        return http.Response('Internal Server Error', 500);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final claimService = ClaimService(apiService: api);

      expect(
        () => claimService.getMyClaims(),
        throwsA(isA<ApiException>()
            .having((e) => e.isServerError, 'isServerError', true)),
      );
    });

    test('claimTypeToIndex maps Motor to 8', () {
      // Test via the static mapping
      expect(ClaimService.claimTypeIndices['Motor'], 8);
      expect(ClaimService.claimTypeIndices['Auto'], 0);
      expect(ClaimService.claimTypeIndices['Health'], 2);
      expect(ClaimService.claimTypeIndices['Life'], 3);
      expect(ClaimService.claimTypeIndices['Other'], 7);
    });

    test('getCompatibleClaimTypes returns correct types for Motor Insurance',
        () {
      final types = ClaimService.getCompatibleClaimTypes('Motor Insurance');
      expect(types, contains('Motor'));
      expect(types, contains('Auto'));
    });

    test('getCompatibleClaimTypes returns correct types for Health Insurance',
        () {
      final types = ClaimService.getCompatibleClaimTypes('Health Insurance');
      expect(types, contains('Health'));
    });

    test('getCompatibleClaimTypes returns empty for unknown policy type', () {
      final types = ClaimService.getCompatibleClaimTypes('Unknown Insurance');
      expect(types, isEmpty);
    });

    test('getRequiredDocuments returns correct docs for Motor claim type', () {
      final docs = ClaimService.getRequiredDocuments('Motor');
      expect(docs, contains('Police Report'));
      expect(docs, contains('Photos of Damage'));
      expect(docs, contains('Repair Estimate'));
      expect(docs, contains('Driver License'));
    });

    test('getRequiredDocuments defaults to Supporting Document for unknown type',
        () {
      final docs = ClaimService.getRequiredDocuments('UnknownType');
      expect(docs, ['Supporting Document']);
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // PolicyService Tests
  // ═════════════════════════════════════════════════════════════════════

  group('PolicyService', () {
    test('getMyPolicies returns list of policies', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/policies/my');
        return http.Response(jsonEncode([_policyResponse]), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final policyService = PolicyService(apiService: api);

      final policies = await policyService.getMyPolicies();

      expect(policies, isA<List<Policy>>());
      expect(policies.length, 1);
      expect(policies.first.policyNumber, 'POL-2026-001');
      expect(policies.first.policyTypeName, 'Motor Insurance');
      expect(policies.first.coverageLimit, 500000.0);
      expect(policies.first.status, 'Active');
    });

    test('getPolicyById returns a single policy', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/policies/policy-001');
        return http.Response(jsonEncode(_policyResponse), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final policyService = PolicyService(apiService: api);

      final policy = await policyService.getPolicyById('policy-001');

      expect(policy.id, 'policy-001');
      expect(policy.premium, 12000.0);
      expect(policy.deductible, 10000.0);
    });

    test('getMyPolicies returns empty list when no policies exist', () async {
      final client = MockClient((request) async {
        return http.Response(jsonEncode([]), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final policyService = PolicyService(apiService: api);

      final policies = await policyService.getMyPolicies();

      expect(policies, isEmpty);
    });

    test('getPolicyById throws ApiException on 404', () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Policy not found'}),
          404,
        );
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final policyService = PolicyService(apiService: api);

      expect(
        () => policyService.getPolicyById('nonexistent'),
        throwsA(isA<ApiException>()
            .having((e) => e.isNotFound, 'isNotFound', true)),
      );
    });

    test('deletePolicy calls DELETE on correct endpoint', () async {
      final client = MockClient((request) async {
        expect(request.method, 'DELETE');
        expect(request.url.path, '/api/policies/policy-001');
        return http.Response('', 204);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final policyService = PolicyService(apiService: api);

      await policyService.deletePolicy('policy-001');
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // PayoutService Tests
  // ═════════════════════════════════════════════════════════════════════

  group('PayoutService', () {
    test('getPayoutByClaimId returns payout', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/payouts/claim/claim-001');
        return http.Response(jsonEncode(_payoutResponse), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final payoutService = PayoutService(apiService: api);

      final payout = await payoutService.getPayoutByClaimId('claim-001');

      expect(payout, isNotNull);
      expect(payout!.id, 'payout-001');
      expect(payout.claimId, 'claim-001');
      expect(payout.proposedPayout, 42750.0);
      expect(payout.finalPayout, 42750.0);
      expect(payout.statusDisplay, 'Pending Approval');
    });

    test('getMyPayouts returns list of payouts', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/payouts/my');
        return http.Response(jsonEncode([_payoutResponse]), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final payoutService = PayoutService(apiService: api);

      final payouts = await payoutService.getMyPayouts();

      expect(payouts, isA<List<Payout>>());
      expect(payouts.length, 1);
      expect(payouts.first.claimNumber, 'CLM-2026-001');
    });

    test('getMyPayouts returns empty list when no payouts', () async {
      final client = MockClient((request) async {
        return http.Response(jsonEncode([]), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final payoutService = PayoutService(apiService: api);

      final payouts = await payoutService.getMyPayouts();

      expect(payouts, isEmpty);
    });

    test('getPayoutByClaimId rethrows on server error', () async {
      final client = MockClient((request) async {
        return http.Response('Server Error', 500);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final payoutService = PayoutService(apiService: api);

      expect(
        () => payoutService.getPayoutByClaimId('claim-001'),
        throwsA(isA<ApiException>()
            .having((e) => e.isServerError, 'isServerError', true)),
      );
    });

    test('getMyPayouts handles paginated response with items key', () async {
      final paginatedResponse = {
        'items': [_payoutResponse],
        'totalCount': 1,
        'page': 1,
        'pageSize': 20,
      };
      final client = MockClient((request) async {
        return http.Response(jsonEncode(paginatedResponse), 200);
      });
      final api = _createApiService(client);
      api.setAuthToken('token');
      final payoutService = PayoutService(apiService: api);

      final payouts = await payoutService.getMyPayouts();

      expect(payouts.length, 1);
      expect(payouts.first.id, 'payout-001');
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // Model Parsing Tests
  // ═════════════════════════════════════════════════════════════════════

  group('Model Parsing', () {
    test('User.fromAuthResponse parses correctly', () {
      final user = User.fromAuthResponse(_loginResponse);

      expect(user.id, '11111111-1111-1111-1111-111111111111');
      expect(user.email, 'kasun@example.com');
      expect(user.firstName, 'Kasun');
      expect(user.lastName, 'Perera');
      expect(user.role, 'Policyholder');
      expect(user.isPolicyholder, true);
      expect(user.isAdmin, false);
      expect(user.fullName, 'Kasun Perera');
    });

    test('User.fromProfileResponse parses correctly', () {
      final user = User.fromProfileResponse(_profileResponse);

      expect(user.id, '11111111-1111-1111-1111-111111111111');
      expect(user.isActive, true);
      expect(user.isPolicyholder, true);
    });

    test('Claim.fromJson parses correctly', () {
      final claim = Claim.fromJson(_claimResponse);

      expect(claim.id, 'claim-001');
      expect(claim.claimNumber, 'CLM-2026-001');
      expect(claim.claimType, 'Motor');
      expect(claim.claimedAmount, 45000.0);
      expect(claim.incidentDate, DateTime(2026, 9, 15));
      expect(claim.status, 'Submitted');
      expect(claim.documents, isEmpty);
    });

    test('Policy.fromJson parses correctly', () {
      final policy = Policy.fromJson(_policyResponse);

      expect(policy.id, 'policy-001');
      expect(policy.policyNumber, 'POL-2026-001');
      expect(policy.policyTypeName, 'Motor Insurance');
      expect(policy.coverageLimit, 500000.0);
      expect(policy.premium, 12000.0);
      expect(policy.deductible, 10000.0);
      expect(policy.isExpired, false);
      expect(policy.status, 'Active');
    });

    test('Payout.fromJson parses correctly', () {
      final payout = Payout.fromJson(_payoutResponse);

      expect(payout.id, 'payout-001');
      expect(payout.claimId, 'claim-001');
      expect(payout.proposedPayout, 42750.0);
      expect(payout.finalPayout, 42750.0);
      expect(payout.deductible, 2250.0);
      expect(payout.deductiblePercentage, 5.0);
      expect(payout.formattedPayout, 'LKR 42750.00');
    });

    test('ClaimDocument.fromJson parses correctly', () {
      final doc = ClaimDocument.fromJson(_documentResponse);

      expect(doc.id, 'doc-001');
      expect(doc.fileName, 'police_report.pdf');
      expect(doc.documentType, 'Police Report');
      expect(doc.fileSize, 204800);
      expect(doc.contentType, 'application/pdf');
      expect(doc.verificationStatus, 'Pending');
      expect(doc.formattedSize, '200.0 KB');
    });

    test('Claim.toJson produces valid JSON', () {
      final claim = Claim.fromJson(_claimResponse);
      final json = claim.toJson();

      expect(json['id'], 'claim-001');
      expect(json['claimNumber'], 'CLM-2026-001');
      expect(json['claimedAmount'], 45000.0);
    });

    test('User equality based on id', () {
      final user1 = User.fromAuthResponse(_loginResponse);
      final user2 = User.fromAuthResponse(_loginResponse);
      final differentUser = User.fromAuthResponse({
        ..._loginResponse,
        'userId': 'different-id',
      });

      expect(user1, equals(user2));
      expect(user1, isNot(equals(differentUser)));
    });
  });

  // ═════════════════════════════════════════════════════════════════════
  // ApiException Tests
  // ═════════════════════════════════════════════════════════════════════

  group('ApiException', () {
    test('correctly identifies status code types', () {
      expect(ApiException('msg', 401).isUnauthorized, true);
      expect(ApiException('msg', 403).isForbidden, true);
      expect(ApiException('msg', 404).isNotFound, true);
      expect(ApiException('msg', 400).isValidationError, true);
      expect(ApiException('msg', 500).isServerError, true);
      expect(ApiException('msg', 502).isServerError, true);
      expect(ApiException('msg', 0).isNetworkError, true);
    });

    test('toString includes status code and message', () {
      final ex = ApiException('Something failed', 422);
      expect(ex.toString(), 'ApiException(422): Something failed');
    });
  });
}
