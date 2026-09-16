/// Base API service for communicating with ASP.NET Core Web API.
///
/// All API calls go through ASP.NET Core — never directly to the AI service.
import 'dart:convert';
import 'dart:io';
import 'package:flutter/foundation.dart';

class ApiService {
  // Use 10.0.2.2 for Android emulator to reach host localhost
  static String get baseUrl {
    const envUrl = String.fromEnvironment('API_BASE_URL');
    if (envUrl.isNotEmpty) return envUrl;
    if (!kIsWeb && Platform.isAndroid) {
      return 'http://10.0.2.2:5000/api';
    }
    return 'http://localhost:5000/api';
  }

  final HttpClient _client = HttpClient();

  /// Performs a GET request and returns decoded JSON.
  Future<dynamic> get(String path) async {
    final uri = Uri.parse('$baseUrl$path');
    final request = await _client.getUrl(uri);
    request.headers.set('Content-Type', 'application/json');
    final response = await request.close();
    final body = await response.transform(utf8.decoder).join();
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return json.decode(body);
    }
    throw HttpException('GET $path failed: ${response.statusCode} $body');
  }

  /// Performs a POST request with optional JSON body.
  Future<dynamic> post(String path, {Map<String, dynamic>? body}) async {
    final uri = Uri.parse('$baseUrl$path');
    final request = await _client.postUrl(uri);
    request.headers.set('Content-Type', 'application/json');
    if (body != null) {
      request.add(utf8.encode(json.encode(body)));
    }
    final response = await request.close();
    final responseBody = await response.transform(utf8.decoder).join();
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (responseBody.isEmpty) return null;
      return json.decode(responseBody);
    }
    throw HttpException('POST $path failed: ${response.statusCode} $responseBody');
  }
}
