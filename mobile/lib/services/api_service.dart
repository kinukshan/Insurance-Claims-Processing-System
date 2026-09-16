Jathusha
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:flutter/foundation.dart';
 main

/// Base API service for communicating with ASP.NET Core Web API.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
class ApiService {
Jathusha
  static const String _defaultBaseUrl = 'http://10.0.2.2:5000/api';

  final String baseUrl;
  final http.Client _client;

  ApiService({String? baseUrl, http.Client? client})
      : baseUrl = baseUrl ?? const String.fromEnvironment(
            'API_BASE_URL',
            defaultValue: _defaultBaseUrl,
          ),
        _client = client ?? http.Client();

  /// Performs a GET request and returns the decoded JSON.
  Future<dynamic> get(String path) async {
    try {
      final response = await _client.get(
        Uri.parse('$baseUrl$path'),
        headers: {'Content-Type': 'application/json'},
      ).timeout(const Duration(seconds: 15));

      if (response.statusCode == 200) {
        return jsonDecode(response.body);
      } else if (response.statusCode == 404) {
        return null;
      } else {
        throw ApiException(
          'Request failed with status ${response.statusCode}',
          response.statusCode,
        );
      }
    } catch (e) {
      if (e is ApiException) rethrow;
      debugPrint('API GET error: $e');
      throw ApiException('Network error: $e', 0);
    }
  }

  /// Performs a POST request and returns the decoded JSON.
  Future<dynamic> post(String path, Map<String, dynamic> body) async {
    try {
      final response = await _client.post(
        Uri.parse('$baseUrl$path'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      ).timeout(const Duration(seconds: 30));

      if (response.statusCode == 200 || response.statusCode == 201) {
        return jsonDecode(response.body);
      } else {
        throw ApiException(
          'Request failed with status ${response.statusCode}',
          response.statusCode,
        );
      }
    } catch (e) {
      if (e is ApiException) rethrow;
      debugPrint('API POST error: $e');
      throw ApiException('Network error: $e', 0);
    }
  }

  void dispose() {
    _client.close();
  }
}

/// Custom exception for API errors.
class ApiException implements Exception {
  final String message;
  final int statusCode;

  ApiException(this.message, this.statusCode);

  @override
  String toString() => 'ApiException($statusCode): $message';
  // Will be used once http/dio package is added for real API calls
  // ignore: unused_field
  static const String _defaultBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5000/api',
  );

  /// HTTP GET request.
  /// Returns decoded JSON response body, or null on 404.
  static Future<dynamic> get(String url) async {
    // TODO: Replace with http package or dio once dependencies are added.
    // Currently a placeholder to satisfy compile-time contracts.
    // The real implementation will use:
    //   final response = await http.get(Uri.parse(url), headers: _headers());
    //   if (response.statusCode == 200) return jsonDecode(response.body);
    //   if (response.statusCode == 404) return null;
    //   throw Exception('API Error: ${response.statusCode}');
    debugPrint('ApiService.get: $url');
    throw UnimplementedError(
      'HTTP client not yet configured. '
      'Add http or dio package and implement ApiService.get.',
    );
  }

  /// HTTP POST request.
  static Future<dynamic> post(String url, {Map<String, dynamic>? body}) async {
    debugPrint('ApiService.post: $url');
    throw UnimplementedError(
      'HTTP client not yet configured. '
      'Add http or dio package and implement ApiService.post.',
    );
  }
 main
}
