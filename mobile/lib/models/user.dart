/// User model for authentication.
///
/// Matches the ASP.NET Core backend DTOs:
/// - AuthResponse:        Token, UserId, FirstName, LastName, Email, Role
/// - UserProfileResponse: UserId, Email, FirstName, LastName, Role, IsActive
class User {
  final String id;
  final String email;
  final String firstName;
  final String lastName;
  final String role;
  final bool isActive;

  const User({
    required this.id,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.role,
    this.isActive = true,
  });

  /// Parse from AuthResponse JSON (login / register).
  ///
  /// AuthResponse fields: token, userId, firstName, lastName, email, role
  factory User.fromAuthResponse(Map<String, dynamic> json) {
    return User(
      id: json['userId'] as String,
      email: json['email'] as String,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      role: json['role'] as String,
    );
  }

  /// Parse from UserProfileResponse JSON (GET /api/auth/me).
  ///
  /// UserProfileResponse fields: userId, email, firstName, lastName, role, isActive
  factory User.fromProfileResponse(Map<String, dynamic> json) {
    return User(
      id: json['userId'] as String,
      email: json['email'] as String,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      role: json['role'] as String,
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'userId': id,
      'email': email,
      'firstName': firstName,
      'lastName': lastName,
      'role': role,
      'isActive': isActive,
    };
  }

  /// Display name.
  String get fullName => '$firstName $lastName';

  /// Convenience alias for id.
  String get userId => id;

  /// Role checks — never trust these as sole authorization; backend is authoritative.
  bool get isPolicyholder => role == 'Policyholder';
  bool get isAdmin => role == 'Admin';
  bool get isClaimsAdjuster => role == 'ClaimsAdjuster';
  bool get isUnderwriter => role == 'Underwriter';

  @override
  String toString() => 'User($id, $email, $role)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is User && runtimeType == other.runtimeType && id == other.id;

  @override
  int get hashCode => id.hashCode;
}
