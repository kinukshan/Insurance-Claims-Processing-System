import 'package:flutter/foundation.dart';

import '../models/user.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';

/// Authentication state managed by Provider (ChangeNotifier).
///
/// This is the single source of truth for authentication state in the app.
///
/// State management approach: **Provider + ChangeNotifier**
///
/// Why Provider:
/// - Already part of Flutter SDK ecosystem (minimal dependency).
/// - Simple, well-documented, and appropriate for the current app scope.
/// - The auth state is inherently global and needs to be accessible throughout
///   the widget tree — Provider's InheritedWidget pattern fits this perfectly.
/// - No complex async state patterns needed — just login/logout/restore.
/// - Easy to test with mock services.
///
/// The AuthProvider:
/// - Holds the current User, auth status, loading states, and errors.
/// - Delegates all API calls to AuthService.
/// - Handles 401 responses from ApiService by clearing the session.
/// - Is the only place that manages auth state (no competing sources).
class AuthProvider extends ChangeNotifier {
  final AuthService _authService;
  final ApiService _apiService;

  AuthStatus _status = AuthStatus.initial;
  User? _user;
  String? _error;
  bool _isLoading = false;

  AuthProvider({
    required this._authService,
    required this._apiService,
  }) {
    // Register 401 handler so expired tokens trigger session cleanup
    _apiService.onSessionExpired = _handleSessionExpired;
  }

  // — State getters —

  AuthStatus get status => _status;
  User? get user => _user;
  String? get error => _error;
  bool get isLoading => _isLoading;

  bool get isAuthenticated => _status == AuthStatus.authenticated && _user != null;
  bool get isUnauthenticated => _status == AuthStatus.unauthenticated;
  bool get isInitial => _status == AuthStatus.initial;

  /// Current user's role. Returns null if not authenticated.
  String? get userRole => _user?.role;

  /// Whether the current user is a Policyholder.
  bool get isPolicyholder => _user?.isPolicyholder ?? false;

  /// For testing: set authenticated user directly.
  @visibleForTesting
  void setAuthenticatedUserForTesting(User user) {
    _user = user;
    _status = AuthStatus.authenticated;
    notifyListeners();
  }

  // — Auth operations —

  /// Attempt to restore a previous session from stored JWT.
  ///
  /// Called on app startup. Shows a loading screen during this process.
  Future<void> restoreSession() async {
    _setLoading(true);
    _clearError();

    try {
      final user = await _authService.restoreSession();
      if (user != null) {
        _user = user;
        _status = AuthStatus.authenticated;
      } else {
        _status = AuthStatus.unauthenticated;
      }
    } catch (e) {
      _status = AuthStatus.unauthenticated;
    } finally {
      _setLoading(false);
    }
  }

  /// Log in with email and password.
  Future<bool> login({required String email, required String password}) async {
    _setLoading(true);
    _clearError();

    try {
      final result = await _authService.login(
        email: email,
        password: password,
      );

      if (result.isSuccess && result.user != null) {
        _user = result.user;
        _status = AuthStatus.authenticated;
        _setLoading(false);
        return true;
      } else {
        _error = result.error ?? 'Login failed.';
        _setLoading(false);
        return false;
      }
    } on ApiException catch (e) {
      _error = e.message;
      _setLoading(false);
      return false;
    } catch (e) {
      _error = 'An unexpected error occurred. Please try again.';
      _setLoading(false);
      return false;
    }
  }

  /// Register a new Policyholder account.
  Future<bool> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    _setLoading(true);
    _clearError();

    try {
      final result = await _authService.register(
        firstName: firstName,
        lastName: lastName,
        email: email,
        password: password,
        confirmPassword: confirmPassword,
      );

      if (result.isSuccess && result.user != null) {
        _user = result.user;
        _status = AuthStatus.authenticated;
        _setLoading(false);
        return true;
      } else {
        _error = result.error ?? 'Registration failed.';
        _setLoading(false);
        return false;
      }
    } on ApiException catch (e) {
      _error = e.message;
      _setLoading(false);
      return false;
    } catch (e) {
      _error = 'An unexpected error occurred. Please try again.';
      _setLoading(false);
      return false;
    }
  }

  /// Log out and clear the session.
  Future<void> logout() async {
    _setLoading(true);
    try {
      await _authService.logout();
    } finally {
      _user = null;
      _status = AuthStatus.unauthenticated;
      _clearError();
      _setLoading(false);
    }
  }

  /// Clear the current error message.
  void clearError() {
    _clearError();
  }

  // — Internal —

  void _handleSessionExpired() {
    _user = null;
    _status = AuthStatus.unauthenticated;
    _error = 'Your session has expired. Please log in again.';
    notifyListeners();
  }

  void _setLoading(bool loading) {
    _isLoading = loading;
    notifyListeners();
  }

  void _clearError() {
    if (_error != null) {
      _error = null;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _apiService.onSessionExpired = null;
    super.dispose();
  }
}

/// Authentication status.
enum AuthStatus {
  /// Initial state — not yet checked.
  initial,

  /// Authenticated — user is logged in.
  authenticated,

  /// Unauthenticated — user needs to log in.
  unauthenticated,
}
