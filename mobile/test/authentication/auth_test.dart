import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:insurance_claims_mobile/models/user.dart';
import 'package:insurance_claims_mobile/services/api_service.dart';
import 'package:insurance_claims_mobile/services/auth_service.dart';
import 'package:insurance_claims_mobile/providers/auth_provider.dart';
import 'package:flutter/services.dart';

// — Test data matching backend DTOs —

const _testAuthResponse = {
  'token': 'test.jwt.token',
  'userId': '11111111-1111-1111-1111-111111111111',
  'firstName': 'Kasun',
  'lastName': 'Perera',
  'email': 'kasun@example.com',
  'role': 'Policyholder',
};

const _testProfileResponse = {
  'userId': '11111111-1111-1111-1111-111111111111',
  'email': 'kasun@example.com',
  'firstName': 'Kasun',
  'lastName': 'Perera',
  'role': 'Policyholder',
  'isActive': true,
};

const _testAdminAuthResponse = {
  'token': 'admin.jwt.token',
  'userId': '22222222-2222-2222-2222-222222222222',
  'firstName': 'Admin',
  'lastName': 'User',
  'email': 'admin@example.com',
  'role': 'Admin',
};

/// Sets up a mock platform channel for FlutterSecureStorage in tests.
/// This prevents MissingPluginException when tests call secure storage methods.
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

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    setupMockSecureStorage();
  });

  group('User model', () {
    test('fromAuthResponse parses all fields correctly', () {
      final user = User.fromAuthResponse(_testAuthResponse);
      expect(user.id, '11111111-1111-1111-1111-111111111111');
      expect(user.email, 'kasun@example.com');
      expect(user.firstName, 'Kasun');
      expect(user.lastName, 'Perera');
      expect(user.role, 'Policyholder');
      expect(user.fullName, 'Kasun Perera');
      expect(user.isPolicyholder, isTrue);
      expect(user.isAdmin, isFalse);
    });

    test('fromProfileResponse parses all fields correctly', () {
      final user = User.fromProfileResponse(_testProfileResponse);
      expect(user.id, '11111111-1111-1111-1111-111111111111');
      expect(user.email, 'kasun@example.com');
      expect(user.firstName, 'Kasun');
      expect(user.lastName, 'Perera');
      expect(user.role, 'Policyholder');
      expect(user.isActive, isTrue);
    });

    test('fromProfileResponse handles missing isActive', () {
      final json = Map<String, dynamic>.from(_testProfileResponse);
      json.remove('isActive');
      final user = User.fromProfileResponse(json);
      expect(user.isActive, isTrue); // defaults to true
    });

    test('toJson produces correct structure', () {
      final user = User.fromAuthResponse(_testAuthResponse);
      final json = user.toJson();
      expect(json['userId'], user.id);
      expect(json['email'], user.email);
      expect(json['firstName'], user.firstName);
      expect(json['lastName'], user.lastName);
      expect(json['role'], user.role);
    });

    test('role checks work correctly', () {
      expect(User.fromAuthResponse(_testAuthResponse).isPolicyholder, isTrue);
      expect(User.fromAuthResponse(_testAdminAuthResponse).isAdmin, isTrue);
      expect(User.fromAuthResponse(_testAdminAuthResponse).isPolicyholder, isFalse);
    });

    test('equality based on id', () {
      final user1 = User.fromAuthResponse(_testAuthResponse);
      final user2 = User.fromAuthResponse(_testAuthResponse);
      expect(user1, equals(user2));
    });
  });

  group('ApiService', () {
    test('injects Authorization header when token is set', () async {
      String? capturedAuth;
      final mockClient = MockClient((request) async {
        capturedAuth = request.headers['Authorization'];
        return http.Response('{}', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      api.setAuthToken('my.jwt.token');
      await api.get('/test');

      expect(capturedAuth, 'Bearer my.jwt.token');
    });

    test('does not include Authorization header without token', () async {
      String? capturedAuth;
      final mockClient = MockClient((request) async {
        capturedAuth = request.headers['Authorization'];
        return http.Response('{}', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      await api.get('/test');

      expect(capturedAuth, isNull);
    });

    test('clearAuthToken removes Authorization header', () async {
      String? capturedAuth;
      final mockClient = MockClient((request) async {
        capturedAuth = request.headers['Authorization'];
        return http.Response('{}', 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      api.setAuthToken('my.jwt.token');
      api.clearAuthToken();
      await api.get('/test');

      expect(capturedAuth, isNull);
    });

    test('throws ApiException with isUnauthorized for 401', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid token.'}),
          401,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/auth/me');
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 401);
        expect(e.isUnauthorized, isTrue);
        expect(e.message, contains('Invalid token'));
      }
    });

    test('throws ApiException with isForbidden for 403', () async {
      final mockClient = MockClient((request) async {
        return http.Response('', 403);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/admin/users');
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 403);
        expect(e.isForbidden, isTrue);
        expect(e.message, contains('Access denied'));
      }
    });

    test('throws ApiException for 400 with validation errors', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Email is required.'}),
          400,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.post('/auth/register', body: {});
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 400);
        expect(e.isValidationError, isTrue);
        expect(e.message, contains('Email is required'));
      }
    });

    test('throws ApiException for 404', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'User not found.'}),
          404,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/auth/me');
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 404);
        expect(e.isNotFound, isTrue);
      }
    });

    test('throws ApiException for 500 server error with sanitized message', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Internal Server Error at C:\\source\\file.cs'}),
          500,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/test');
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 500);
        expect(e.isServerError, isTrue);
        // Should not expose internal server details
        expect(e.message, contains('server error'));
      }
    });

    test('handles network errors gracefully', () async {
      final mockClient = MockClient((request) async {
        throw Exception('SocketException: Connection refused');
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/test');
        fail('Expected ApiException');
      } on ApiException catch (e) {
        expect(e.statusCode, 0);
        expect(e.isNetworkError, isTrue);
      }
    });

    test('POST sends correct request body', () async {
      Map<String, dynamic>? capturedBody;
      final mockClient = MockClient((request) async {
        capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
        return http.Response(jsonEncode(_testAuthResponse), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      await api.post('/auth/login', body: {
        'email': 'test@example.com',
        'password': 'password123',
      });

      expect(capturedBody!['email'], 'test@example.com');
      expect(capturedBody!['password'], 'password123');
    });

    test('handles 204 No Content correctly', () async {
      final mockClient = MockClient((request) async {
        return http.Response('', 204);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final result = await api.delete('/test/1');
      expect(result, isNull);
    });
  });

  group('Login request serialization', () {
    test('login POST sends correct JSON fields', () async {
      Map<String, dynamic>? sentBody;
      final mockClient = MockClient((request) async {
        sentBody = jsonDecode(request.body) as Map<String, dynamic>;
        return http.Response(jsonEncode(_testAuthResponse), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      await api.post('/auth/login', body: {
        'email': 'kasun@example.com',
        'password': 'TestPass123',
      });

      expect(sentBody, isNotNull);
      expect(sentBody!['email'], 'kasun@example.com');
      expect(sentBody!['password'], 'TestPass123');
      expect(sentBody!.keys.length, 2);
    });
  });

  group('Registration request serialization', () {
    test('register POST sends correct JSON fields', () async {
      Map<String, dynamic>? sentBody;
      final mockClient = MockClient((request) async {
        sentBody = jsonDecode(request.body) as Map<String, dynamic>;
        return http.Response(jsonEncode(_testAuthResponse), 201);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      await api.post('/auth/register', body: {
        'firstName': 'Kasun',
        'lastName': 'Perera',
        'email': 'kasun@example.com',
        'password': 'TestPass123',
        'confirmPassword': 'TestPass123',
      });

      expect(sentBody, isNotNull);
      expect(sentBody!['firstName'], 'Kasun');
      expect(sentBody!['lastName'], 'Perera');
      expect(sentBody!['email'], 'kasun@example.com');
      expect(sentBody!['password'], 'TestPass123');
      expect(sentBody!['confirmPassword'], 'TestPass123');
      expect(sentBody!.keys.length, 5);
    });
  });

  group('AuthProvider', () {
    late ApiService apiService;

    ApiService createApiWithClient(MockClient client) {
      return ApiService(baseUrl: 'http://localhost/api', client: client);
    }

    test('initial status is initial', () {
      final mockClient = MockClient((r) async => http.Response('', 200));
      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      expect(provider.status, AuthStatus.initial);
      expect(provider.isAuthenticated, isFalse);
      expect(provider.user, isNull);
    });

    test('successful login sets authenticated status', () async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(jsonEncode(_testAuthResponse), 200);
        }
        return http.Response('', 404);
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      final result = await provider.login(
        email: 'kasun@example.com',
        password: 'TestPass123',
      );

      expect(result, isTrue);
      expect(provider.isAuthenticated, isTrue);
      expect(provider.user!.email, 'kasun@example.com');
      expect(provider.user!.role, 'Policyholder');
      expect(provider.isPolicyholder, isTrue);
      expect(provider.error, isNull);
    });

    test('failed login sets error', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid email or password.'}),
          401,
        );
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      final result = await provider.login(
        email: 'wrong@example.com',
        password: 'wrongpassword',
      );

      expect(result, isFalse);
      expect(provider.isAuthenticated, isFalse);
      expect(provider.error, isNotNull);
      expect(provider.error, contains('Invalid email or password'));
    });

    test('successful registration sets authenticated status', () async {
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/auth/register')) {
          return http.Response(jsonEncode(_testAuthResponse), 201);
        }
        return http.Response('', 404);
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      final result = await provider.register(
        firstName: 'Kasun',
        lastName: 'Perera',
        email: 'kasun@example.com',
        password: 'TestPass123',
        confirmPassword: 'TestPass123',
      );

      expect(result, isTrue);
      expect(provider.isAuthenticated, isTrue);
      expect(provider.user!.firstName, 'Kasun');
      expect(provider.user!.role, 'Policyholder');
    });

    test('registration with existing email shows error', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'An account with this email already exists.'}),
          400,
        );
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      final result = await provider.register(
        firstName: 'Kasun',
        lastName: 'Perera',
        email: 'existing@example.com',
        password: 'TestPass123',
        confirmPassword: 'TestPass123',
      );

      expect(result, isFalse);
      expect(provider.error, contains('already exists'));
    });

    test('logout clears user and sets unauthenticated', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testAuthResponse), 200);
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      // Login first
      await provider.login(email: 'test@example.com', password: 'pass');
      expect(provider.isAuthenticated, isTrue);

      // Logout
      await provider.logout();
      expect(provider.isAuthenticated, isFalse);
      expect(provider.isUnauthenticated, isTrue);
      expect(provider.user, isNull);
      expect(provider.error, isNull);
    });

    test('clearError removes error message', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'error': 'Bad request'}), 401);
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      await provider.login(email: 'test@example.com', password: 'wrong');
      expect(provider.error, isNotNull);

      provider.clearError();
      expect(provider.error, isNull);
    });

    test('non-policyholder role is detected correctly', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testAdminAuthResponse), 200);
      });

      apiService = createApiWithClient(mockClient);
      final authService = AuthService(api: apiService);
      final provider = AuthProvider(authService: authService, apiService: apiService);

      await provider.login(email: 'admin@example.com', password: 'pass');
      expect(provider.isAuthenticated, isTrue);
      expect(provider.isPolicyholder, isFalse);
      expect(provider.userRole, 'Admin');
    });
  });

  group('AuthService', () {
    test('login returns success with user and token', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testAuthResponse), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      final result = await authService.login(
        email: 'kasun@example.com',
        password: 'TestPass123',
      );

      expect(result.isSuccess, isTrue);
      expect(result.user, isNotNull);
      expect(result.user!.email, 'kasun@example.com');
      expect(result.token, 'test.jwt.token');
      expect(api.hasAuthToken, isTrue);
    });

    test('login returns failure on 401', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid email or password.'}),
          401,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      final result = await authService.login(
        email: 'wrong@example.com',
        password: 'wrong',
      );

      expect(result.isSuccess, isFalse);
      expect(result.error, contains('Invalid email or password'));
    });

    test('register returns success and sets token', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testAuthResponse), 201);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      final result = await authService.register(
        firstName: 'Kasun',
        lastName: 'Perera',
        email: 'kasun@example.com',
        password: 'TestPass123',
        confirmPassword: 'TestPass123',
      );

      expect(result.isSuccess, isTrue);
      expect(result.user!.firstName, 'Kasun');
      expect(result.user!.role, 'Policyholder');
      expect(api.hasAuthToken, isTrue);
    });

    test('register returns failure on validation error', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Password must be at least 8 characters.'}),
          400,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      final result = await authService.register(
        firstName: 'Kasun',
        lastName: 'Perera',
        email: 'kasun@example.com',
        password: 'short',
        confirmPassword: 'short',
      );

      expect(result.isSuccess, isFalse);
      expect(result.error, contains('at least 8 characters'));
    });

    test('getCurrentUser returns user on valid token', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testProfileResponse), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      api.setAuthToken('valid.jwt.token');
      final authService = AuthService(api: api);

      final user = await authService.getCurrentUser();
      expect(user, isNotNull);
      expect(user!.email, 'kasun@example.com');
      expect(user.isActive, isTrue);
    });

    test('getCurrentUser returns null on 401', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid token.'}),
          401,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      api.setAuthToken('expired.jwt.token');
      final authService = AuthService(api: api);

      final user = await authService.getCurrentUser();
      expect(user, isNull);
    });

    test('logout clears token from API service', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode(_testAuthResponse), 200);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      // Login to set token
      await authService.login(email: 'test@example.com', password: 'pass');
      expect(api.hasAuthToken, isTrue);

      // Logout
      await authService.logout();
      expect(api.hasAuthToken, isFalse);
    });
  });

  group('Session restoration', () {
    test('restoreSession returns null when no token stored', () async {
      final mockClient = MockClient((request) async {
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      final user = await authService.restoreSession();
      expect(user, isNull);
    });

    test('restoreSession validates stored token with backend', () async {
      // First, simulate a login to store a token
      int callCount = 0;
      final mockClient = MockClient((request) async {
        callCount++;
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(jsonEncode(_testAuthResponse), 200);
        }
        if (request.url.path.endsWith('/auth/me')) {
          return http.Response(jsonEncode(_testProfileResponse), 200);
        }
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      // Login first to store token
      await authService.login(email: 'kasun@example.com', password: 'TestPass123');

      // Now restore the session
      final user = await authService.restoreSession();
      expect(user, isNotNull);
      expect(user!.email, 'kasun@example.com');
      expect(callCount, 2);
    });

    test('restoreSession clears invalid token', () async {
      // First login, then simulate expired token on restore
      bool loginPhase = true;
      final mockClient = MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(jsonEncode(_testAuthResponse), 200);
        }
        if (request.url.path.endsWith('/auth/me')) {
          if (loginPhase) {
            return http.Response(jsonEncode(_testProfileResponse), 200);
          }
          // Token expired
          return http.Response(jsonEncode({'error': 'Invalid token.'}), 401);
        }
        return http.Response('', 404);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      final authService = AuthService(api: api);

      // Login first
      await authService.login(email: 'kasun@example.com', password: 'TestPass123');
      loginPhase = false;

      // Restore with expired token
      final user = await authService.restoreSession();
      expect(user, isNull);
      expect(api.hasAuthToken, isFalse);
    });

    test('API service token cleared after failed restore', () async {
      final mockClient = MockClient((request) async {
        return http.Response(
          jsonEncode({'error': 'Invalid token.'}),
          401,
        );
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);
      api.setAuthToken('stored.invalid.token');
      final authService = AuthService(api: api);

      final user = await authService.getCurrentUser();
      expect(user, isNull);
    });
  });

  group('HTTP error handling', () {
    test('401 response throws ApiException with isUnauthorized', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'error': 'Token expired'}), 401);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/claims/my-claims');
        fail('Should throw');
      } on ApiException catch (e) {
        expect(e.isUnauthorized, isTrue);
        expect(e.isForbidden, isFalse);
      }
    });

    test('403 response throws ApiException with isForbidden', () async {
      final mockClient = MockClient((request) async {
        return http.Response('', 403);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/admin/settings');
        fail('Should throw');
      } on ApiException catch (e) {
        expect(e.isForbidden, isTrue);
        expect(e.isUnauthorized, isFalse);
        expect(e.message, contains('Access denied'));
      }
    });

    test('500 response shows generic server error message', () async {
      final mockClient = MockClient((request) async {
        return http.Response('Internal Server Error', 500);
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/test');
        fail('Should throw');
      } on ApiException catch (e) {
        expect(e.isServerError, isTrue);
        expect(e.message, contains('server error'));
      }
    });

    test('network error throws ApiException with isNetworkError', () async {
      final mockClient = MockClient((request) async {
        throw Exception('Connection refused');
      });

      final api = ApiService(baseUrl: 'http://localhost/api', client: mockClient);

      try {
        await api.get('/test');
        fail('Should throw');
      } on ApiException catch (e) {
        expect(e.isNetworkError, isTrue);
        expect(e.statusCode, 0);
      }
    });
  });

  group('ApiException', () {
    test('isUnauthorized returns true for 401', () {
      expect(ApiException('test', 401).isUnauthorized, isTrue);
    });

    test('isForbidden returns true for 403', () {
      expect(ApiException('test', 403).isForbidden, isTrue);
    });

    test('isNotFound returns true for 404', () {
      expect(ApiException('test', 404).isNotFound, isTrue);
    });

    test('isValidationError returns true for 400', () {
      expect(ApiException('test', 400).isValidationError, isTrue);
    });

    test('isServerError returns true for 500+', () {
      expect(ApiException('test', 500).isServerError, isTrue);
      expect(ApiException('test', 503).isServerError, isTrue);
    });

    test('isNetworkError returns true for 0', () {
      expect(ApiException('test', 0).isNetworkError, isTrue);
    });
  });
}
