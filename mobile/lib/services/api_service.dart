import 'package:flutter/foundation.dart';

/// Base API service for communicating with ASP.NET Core Web API.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
class ApiService {
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
}
