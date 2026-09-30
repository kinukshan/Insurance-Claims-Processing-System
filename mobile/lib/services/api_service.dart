import 'dart:convert';
import 'package:http/http.dart' as http;

/// Base API service for communicating with ASP.NET Core Web API.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
///
/// Supports JWT Bearer authentication. The authentication token is injected
/// via [setAuthToken] and removed via [clearAuthToken].
class ApiService {
  /// Default base URL pointing to the ASP.NET Core backend.
  /// Port 5097 matches the backend launchSettings.json http profile.
  ///
  /// Override via constructor for different environments:
  /// - Android emulator:  `http://10.0.2.2:5097/api`
  /// - iOS simulator:     `http://localhost:5097/api`
  /// - Physical device:   `http://<dev-machine-ip>:5097/api`
  static const String defaultBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5097/api',
  );

  static ApiService? _shared;
  static ApiService get shared => _shared ??= ApiService();
  static set shared(ApiService instance) => _shared = instance;

  final String baseUrl;
  final http.Client _client;
  String? _authToken;

  /// Callback invoked when a 401 response indicates the session is invalid.
  /// The auth provider sets this to clear the session and redirect to login.
  void Function()? onSessionExpired;

  ApiService({String? baseUrl, http.Client? client})
      : baseUrl = baseUrl ?? defaultBaseUrl,
        _client = client ?? http.Client() {
    _shared ??= this;
  }

  /// Set the JWT bearer token for authenticated requests.
  void setAuthToken(String token) {
    _authToken = token;
  }

  /// Clear the stored authentication token.
  void clearAuthToken() {
    _authToken = null;
  }

  /// Whether an auth token is currently set.
  bool get hasAuthToken => _authToken != null;

  /// Default headers for all requests.
  /// Uses JWT Bearer authentication when a token is available.
  Map<String, String> get _headers {
    final headers = <String, String>{
      'Content-Type': 'application/json',
    };
    if (_authToken != null) {
      headers['Authorization'] = 'Bearer $_authToken';
    }
    return headers;
  }

  /// GET request.
  Future<dynamic> get(String endpoint) async {
    try {
      final response = await _client.get(
        Uri.parse('$baseUrl$endpoint'),
        headers: _headers,
      ).timeout(const Duration(seconds: 30));
      return _handleResponse(response);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException(_sanitizeErrorMessage(e.toString()), 0);
    }
  }

  /// POST request with JSON body.
  Future<dynamic> post(String endpoint, {Map<String, dynamic>? body}) async {
    try {
      final response = await _client.post(
        Uri.parse('$baseUrl$endpoint'),
        headers: _headers,
        body: body != null ? jsonEncode(body) : null,
      ).timeout(const Duration(seconds: 30));
      return _handleResponse(response);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException(_sanitizeErrorMessage(e.toString()), 0);
    }
  }

  /// PUT request with JSON body.
  Future<dynamic> put(String endpoint, {Map<String, dynamic>? body}) async {
    try {
      final response = await _client.put(
        Uri.parse('$baseUrl$endpoint'),
        headers: _headers,
        body: body != null ? jsonEncode(body) : null,
      ).timeout(const Duration(seconds: 30));
      return _handleResponse(response);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException(_sanitizeErrorMessage(e.toString()), 0);
    }
  }

  /// DELETE request.
  Future<dynamic> delete(String endpoint) async {
    try {
      final response = await _client.delete(
        Uri.parse('$baseUrl$endpoint'),
        headers: _headers,
      ).timeout(const Duration(seconds: 15));
      return _handleResponse(response);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException(_sanitizeErrorMessage(e.toString()), 0);
    }
  }

  /// Multipart file upload (for document evidence).
  Future<dynamic> uploadFile(
    String endpoint, {
    required String filePath,
    required String fieldName,
    Map<String, String>? fields,
  }) async {
    final request = http.MultipartRequest(
      'POST',
      Uri.parse('$baseUrl$endpoint'),
    );

    // Inject auth header for file uploads
    if (_authToken != null) {
      request.headers['Authorization'] = 'Bearer $_authToken';
    }

    if (fields != null) {
      request.fields.addAll(fields);
    }

    request.files.add(await http.MultipartFile.fromPath(fieldName, filePath));

    final streamedResponse = await _client.send(request);
    final response = await http.Response.fromStream(streamedResponse);
    return _handleResponse(response);
  }

  /// Process the HTTP response and handle errors.
  dynamic _handleResponse(http.Response response) {
    if (response.statusCode == 204) return null;

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(response.body);
    }

    // 401 Unauthorized — session expired or invalid token
    if (response.statusCode == 401) {
      final message = _extractErrorMessage(response) ?? 'Authentication required. Please log in.';
      // Notify listener to clear session — but don't do it for login/register failures
      // The caller handles that distinction
      throw ApiException(message, 401);
    }

    // 403 Forbidden — authenticated but not authorized
    if (response.statusCode == 403) {
      throw ApiException(
        'Access denied. You do not have permission to perform this action.',
        403,
      );
    }

    // 404 Not Found
    if (response.statusCode == 404) {
      final message = _extractErrorMessage(response) ?? 'The requested resource was not found.';
      throw ApiException(message, 404);
    }

    // 400 Bad Request — validation errors
    if (response.statusCode == 400) {
      final message = _extractErrorMessage(response) ?? 'Invalid request. Please check your input.';
      throw ApiException(message, 400);
    }

    // 500+ Server errors
    if (response.statusCode >= 500) {
      throw ApiException(
        'A server error occurred. Please try again later.',
        response.statusCode,
      );
    }

    // Other errors
    final message = _extractErrorMessage(response) ?? 'An unexpected error occurred.';
    throw ApiException(message, response.statusCode);
  }

  /// Extract error message from response body.
  /// Sanitizes so that internal details are not exposed.
  String? _extractErrorMessage(http.Response response) {
    if (response.body.isEmpty) return null;
    try {
      final body = jsonDecode(response.body);
      if (body is Map<String, dynamic>) {
        // ASP.NET Core format: { "error": "message" }
        if (body['error'] is String) {
          return _sanitizeErrorMessage(body['error'] as String);
        }
        // ASP.NET Core validation: { "errors": ["msg1", "msg2"] }
        if (body['errors'] is List) {
          return (body['errors'] as List)
              .map((e) => _sanitizeErrorMessage(e.toString()))
              .join('; ');
        }
        // ASP.NET Core validation: { "errors": { "field": ["msg"] } }
        if (body['errors'] is Map) {
          final errors = body['errors'] as Map<String, dynamic>;
          final messages = <String>[];
          for (final entry in errors.entries) {
            if (entry.value is List) {
              for (final msg in entry.value as List) {
                messages.add(_sanitizeErrorMessage(msg.toString()));
              }
            }
          }
          if (messages.isNotEmpty) return messages.join('; ');
        }
        // Fallback: { "title": "message" }
        if (body['title'] is String) {
          return _sanitizeErrorMessage(body['title'] as String);
        }
      }
    } catch (_) {
      // Not JSON — ignore
    }
    return null;
  }

  /// Sanitize error messages to remove internal details.
  /// Strips stack traces, connection strings, file paths, and tokens.
  String _sanitizeErrorMessage(String message) {
    // Remove anything that looks like a stack trace
    final cleaned = message.replaceAll(RegExp(r'at [\w.]+ in .+'), '');
    // Remove connection strings
    final noConnStr = cleaned.replaceAll(RegExp(r'(Host|Server|Data Source)=.+?(;|$)', caseSensitive: false), '');
    // Remove file paths
    final noPath = noConnStr.replaceAll(RegExp(r'[A-Z]:\\[\w\\]+|/[\w/]+\.[\w]+'), '');
    // Remove anything that looks like a JWT
    final noToken = noPath.replaceAll(RegExp(r'eyJ[\w-]+\.eyJ[\w-]+\.[\w-]+'), '[redacted]');
    // Trim and limit length
    final result = noToken.trim();
    if (result.length > 200) return '${result.substring(0, 200)}…';
    return result.isEmpty ? 'An error occurred.' : result;
  }

  /// Closes the underlying HTTP client.
  void dispose() {
    _client.close();
  }
}

/// Custom exception for API errors.
class ApiException implements Exception {
  final String message;
  final int statusCode;

  ApiException(this.message, this.statusCode);

  /// Whether this is an authentication failure (401).
  bool get isUnauthorized => statusCode == 401;

  /// Whether this is a forbidden/authorization failure (403).
  bool get isForbidden => statusCode == 403;

  /// Whether this is a not-found error (404).
  bool get isNotFound => statusCode == 404;

  /// Whether this is a validation error (400).
  bool get isValidationError => statusCode == 400;

  /// Whether this is a server error (5xx).
  bool get isServerError => statusCode >= 500;

  /// Whether this is a network/connection error.
  bool get isNetworkError => statusCode == 0;

  @override
  String toString() => 'ApiException($statusCode): $message';
}
