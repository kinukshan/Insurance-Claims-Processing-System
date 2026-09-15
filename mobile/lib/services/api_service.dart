import 'dart:convert';
import 'package:http/http.dart' as http;

/// Base API service for communicating with ASP.NET Core Web API.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
class ApiService {
  /// Default base URL pointing to the ASP.NET Core backend.
  static const String defaultBaseUrl = 'http://10.0.2.2:5000/api';

  /// Dev-mode user ID for testing without JWT auth.
  static const String devUserId = '00000000-0000-0000-0000-000000000001';

  final String baseUrl;
  final http.Client _client;

  ApiService({String? baseUrl, http.Client? client})
      : baseUrl = baseUrl ?? defaultBaseUrl,
        _client = client ?? http.Client();

  /// Default headers for all requests.
  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        'X-User-Id': devUserId,
      };

  /// GET request.
  Future<dynamic> get(String endpoint) async {
    final response = await _client.get(
      Uri.parse('$baseUrl$endpoint'),
      headers: _headers,
    );
    return _handleResponse(response);
  }

  /// POST request with JSON body.
  Future<dynamic> post(String endpoint, {Map<String, dynamic>? body}) async {
    final response = await _client.post(
      Uri.parse('$baseUrl$endpoint'),
      headers: _headers,
      body: body != null ? jsonEncode(body) : null,
    );
    return _handleResponse(response);
  }

  /// PUT request with JSON body.
  Future<dynamic> put(String endpoint, {Map<String, dynamic>? body}) async {
    final response = await _client.put(
      Uri.parse('$baseUrl$endpoint'),
      headers: _headers,
      body: body != null ? jsonEncode(body) : null,
    );
    return _handleResponse(response);
  }

  /// DELETE request.
  Future<dynamic> delete(String endpoint) async {
    final response = await _client.delete(
      Uri.parse('$baseUrl$endpoint'),
      headers: _headers,
    );
    return _handleResponse(response);
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
    request.headers['X-User-Id'] = devUserId;

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

    // Parse error body
    String errorMessage;
    try {
      final body = jsonDecode(response.body);
      errorMessage = body['error'] ??
          (body['errors'] as List?)?.join('; ') ??
          'API Error: ${response.statusCode}';
    } catch (_) {
      errorMessage = 'API Error: ${response.statusCode}';
    }

    throw ApiException(errorMessage, response.statusCode);
  }
}

/// Custom exception for API errors.
class ApiException implements Exception {
  final String message;
  final int statusCode;

  ApiException(this.message, this.statusCode);

  @override
  String toString() => 'ApiException($statusCode): $message';
}
