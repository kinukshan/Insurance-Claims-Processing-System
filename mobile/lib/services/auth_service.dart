import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/user.dart';
import 'api_service.dart';

/// Authentication service — manages login, registration, session restoration,
/// and logout using the ASP.NET Core backend.
///
/// Endpoints:
///   POST /api/auth/register → AuthResponse
///   POST /api/auth/login    → AuthResponse
///   GET  /api/auth/me       → UserProfileResponse
///
/// JWT tokens are stored in flutter_secure_storage and injected into the
/// shared ApiService instance.
class AuthService {
  final ApiService _api;
  final FlutterSecureStorage _secureStorage;

  static const String _tokenKey = 'auth_jwt_token';

  AuthService({
    required this._api,
    FlutterSecureStorage? secureStorage,
  })  : _secureStorage = secureStorage ?? const FlutterSecureStorage(
          aOptions: AndroidOptions(encryptedSharedPreferences: true),
        );

  /// Register a new Policyholder account.
  ///
  /// POST /api/auth/register
  /// Body: { firstName, lastName, email, password, confirmPassword }
  /// Response: AuthResponse { token, userId, firstName, lastName, email, role }
  ///
  /// The backend always assigns Policyholder role for public registration.
  Future<AuthResult> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    try {
      final data = await _api.post('/auth/register', body: {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'password': password,
        'confirmPassword': confirmPassword,
      });

      final responseMap = data as Map<String, dynamic>;
      final token = responseMap['token'] as String;
      final user = User.fromAuthResponse(responseMap);

      // Store token securely and inject into API service
      await _storeToken(token);
      _api.setAuthToken(token);

      return AuthResult.success(user: user, token: token);
    } on ApiException catch (e) {
      return AuthResult.failure(error: e.message);
    } catch (e) {
      return AuthResult.failure(error: 'Registration failed. Please try again.');
    }
  }

  /// Authenticate with email and password.
  ///
  /// POST /api/auth/login
  /// Body: { email, password }
  /// Response: AuthResponse { token, userId, firstName, lastName, email, role }
  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    try {
      final data = await _api.post('/auth/login', body: {
        'email': email,
        'password': password,
      });

      final responseMap = data as Map<String, dynamic>;
      final token = responseMap['token'] as String;
      final user = User.fromAuthResponse(responseMap);

      // Store token securely and inject into API service
      await _storeToken(token);
      _api.setAuthToken(token);

      return AuthResult.success(user: user, token: token);
    } on ApiException catch (e) {
      if (e.isUnauthorized) {
        return AuthResult.failure(error: 'Invalid email or password.');
      }
      return AuthResult.failure(error: e.message);
    } catch (e) {
      return AuthResult.failure(error: 'Login failed. Please check your connection and try again.');
    }
  }

  /// Get the current user's profile from the backend.
  ///
  /// GET /api/auth/me
  /// Response: UserProfileResponse { userId, email, firstName, lastName, role, isActive }
  ///
  /// Requires a valid JWT token. Returns null if the token is invalid or expired.
  Future<User?> getCurrentUser() async {
    try {
      final data = await _api.get('/auth/me');
      if (data == null) return null;
      return User.fromProfileResponse(data as Map<String, dynamic>);
    } on ApiException catch (e) {
      if (e.isUnauthorized || e.isNotFound) {
        return null;
      }
      rethrow;
    }
  }

  /// Attempt to restore a session from a previously stored JWT token.
  ///
  /// 1. Read the stored token from secure storage.
  /// 2. Inject it into the API service.
  /// 3. Validate it by calling GET /api/auth/me.
  /// 4. If the token is invalid or expired, clear it.
  ///
  /// Returns the authenticated user if the session is valid, null otherwise.
  Future<User?> restoreSession() async {
    try {
      final token = await _secureStorage.read(key: _tokenKey);
      if (token == null || token.isEmpty) return null;

      // Inject token before validating
      _api.setAuthToken(token);

      // Validate with the backend
      final user = await getCurrentUser();
      if (user == null) {
        // Token is invalid or expired — clean up
        await _clearToken();
        _api.clearAuthToken();
        return null;
      }

      return user;
    } catch (e) {
      // Any error during restoration — clear and return null
      debugPrint('Session restoration failed: ${e.runtimeType}');
      await _clearToken();
      _api.clearAuthToken();
      return null;
    }
  }

  /// Log out the current user.
  ///
  /// Clears the stored token and removes it from the API service.
  /// No backend logout endpoint exists — we just discard the JWT.
  Future<void> logout() async {
    await _clearToken();
    _api.clearAuthToken();
  }

  /// Store token in secure storage.
  Future<void> _storeToken(String token) async {
    await _secureStorage.write(key: _tokenKey, value: token);
  }

  /// Clear stored token.
  Future<void> _clearToken() async {
    try {
      await _secureStorage.delete(key: _tokenKey);
    } catch (_) {
      // Ignore errors during cleanup
    }
  }
}

/// Result of an authentication operation (login or register).
class AuthResult {
  final bool isSuccess;
  final User? user;
  final String? token;
  final String? error;

  const AuthResult._({
    required this.isSuccess,
    this.user,
    this.token,
    this.error,
  });

  factory AuthResult.success({required User user, required String token}) {
    return AuthResult._(isSuccess: true, user: user, token: token);
  }

  factory AuthResult.failure({required String error}) {
    return AuthResult._(isSuccess: false, error: error);
  }
}
