import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../providers/auth_provider.dart';
import '../../utils/app_theme.dart';
import '../../widgets/shared_widgets.dart';

/// Login screen — authenticates the user via the ASP.NET Core backend.
///
/// Features:
/// - Email and password input with validation
/// - Show/hide password toggle
/// - Loading indicator during authentication
/// - Error messages from the backend
/// - Navigation to the registration screen
/// - Responsive and accessible layout
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _emailFocusNode = FocusNode();
  final _passwordFocusNode = FocusNode();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _emailFocusNode.dispose();
    _passwordFocusNode.dispose();
    super.dispose();
  }

  Future<void> _handleLogin() async {
    if (!_formKey.currentState!.validate()) return;

    final authProvider = context.read<AuthProvider>();

    final success = await authProvider.login(
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );

    if (success && mounted) {
      // Navigation is handled by the auth-aware root widget
      // which listens to AuthProvider status changes.
    }
  }

  String? _validateEmail(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Email is required';
    }
    final emailRegex = RegExp(r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');
    if (!emailRegex.hasMatch(value.trim())) {
      return 'Enter a valid email address';
    }
    return null;
  }

  String? _validatePassword(String? value) {
    if (value == null || value.isEmpty) {
      return 'Password is required';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.backgroundGrey,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const SizedBox(height: 32),

                  // — Logo & branding —
                  _buildHeader(),

                  const SizedBox(height: 40),

                  // — Login form card —
                  _buildLoginCard(),

                  // — Quick Role Selectors for testing all four roles (Debug only) —
                  if (!kReleaseMode) _buildQuickRoleSelectors(),

                  const SizedBox(height: 12),

                  // — Register link —
                  _buildRegisterLink(),

                  const SizedBox(height: 32),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeader() {
    return Column(
      children: [
        // App icon
        Container(
          width: 72,
          height: 72,
          decoration: BoxDecoration(
            color: AppTheme.primaryTeal,
            borderRadius: BorderRadius.circular(18),
            boxShadow: [
              BoxShadow(
                color: AppTheme.primaryTeal.withValues(alpha: 0.3),
                blurRadius: 20,
                offset: const Offset(0, 8),
              ),
            ],
          ),
          child: const Icon(
            Icons.shield_outlined,
            size: 38,
            color: Colors.white,
          ),
        ),
        const SizedBox(height: 20),
        const Text(
          'Insurance Claims',
          style: TextStyle(
            fontSize: 26,
            fontWeight: FontWeight.w700,
            color: AppTheme.deepNavy,
            letterSpacing: 0.3,
          ),
        ),
        const SizedBox(height: 6),
        const Text(
          'Sign in to manage your claims',
          style: TextStyle(
            fontSize: 14,
            color: AppTheme.textSecondary,
          ),
        ),
      ],
    );
  }

  Widget _buildLoginCard() {
    return Consumer<AuthProvider>(
      builder: (context, auth, _) {
        return Container(
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: AppTheme.surfaceWhite,
            borderRadius: BorderRadius.circular(AppTheme.radiusLarge),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Welcome Back',
                  style: TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w700,
                    color: AppTheme.deepNavy,
                  ),
                ),
                const SizedBox(height: 20),

                // Error message
                if (auth.error != null) ...[
                  ErrorBanner(
                    message: auth.error!,
                    onDismiss: auth.clearError,
                  ),
                  const SizedBox(height: 16),
                ],

                // Email
                FormInput(
                  controller: _emailController,
                  label: 'Email',
                  hint: 'you@example.com',
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  prefixIcon: const Icon(Icons.email_outlined, size: 20),
                  validator: _validateEmail,
                  enabled: !auth.isLoading,
                  focusNode: _emailFocusNode,
                  onFieldSubmitted: (_) {
                    FocusScope.of(context).requestFocus(_passwordFocusNode);
                  },
                ),
                const SizedBox(height: 16),

                // Password
                FormInput(
                  controller: _passwordController,
                  label: 'Password',
                  obscureText: _obscurePassword,
                  textInputAction: TextInputAction.done,
                  prefixIcon: const Icon(Icons.lock_outlined, size: 20),
                  suffixIcon: IconButton(
                    icon: Icon(
                      _obscurePassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                      size: 20,
                      color: AppTheme.textSecondary,
                    ),
                    onPressed: () {
                      setState(() => _obscurePassword = !_obscurePassword);
                    },
                  ),
                  validator: _validatePassword,
                  enabled: !auth.isLoading,
                  focusNode: _passwordFocusNode,
                  onFieldSubmitted: (_) => _handleLogin(),
                ),
                const SizedBox(height: 24),

                // Submit button
                BrandedButton(
                  label: 'Sign In',
                  onPressed: _handleLogin,
                  isLoading: auth.isLoading,
                  icon: Icons.login,
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildQuickRoleSelectors() {
    if (kReleaseMode) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.only(top: 8, bottom: 4),
      child: Column(
        children: [
          const Text(
            'Quick Role Fill (Demo)',
            style: TextStyle(fontSize: 11, color: AppTheme.textSecondary, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            alignment: WrapAlignment.center,
            children: [
              ActionChip(
                label: const Text('Policyholder', style: TextStyle(fontSize: 11)),
                onPressed: () {
                  _emailController.text = 'flutter_test_dev@example.com';
                  if (!kReleaseMode) {
                    _passwordController.text = 'Password123!';
                  }
                },
              ),
              ActionChip(
                label: const Text('Adjuster', style: TextStyle(fontSize: 11)),
                onPressed: () {
                  _emailController.text = 'adjuster@test.com';
                  if (!kReleaseMode) {
                    _passwordController.text = 'Staff@Test2026!';
                  }
                },
              ),
              ActionChip(
                label: const Text('Underwriter', style: TextStyle(fontSize: 11)),
                onPressed: () {
                  _emailController.text = 'underwriter@test.com';
                  if (!kReleaseMode) {
                    _passwordController.text = 'Staff@Test2026!';
                  }
                },
              ),
              ActionChip(
                label: const Text('Admin', style: TextStyle(fontSize: 11)),
                onPressed: () {
                  _emailController.text = 'admin@test.com';
                  if (!kReleaseMode) {
                    _passwordController.text = 'Staff@Test2026!';
                  }
                },
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildRegisterLink() {
    return Wrap(
      alignment: WrapAlignment.center,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        const Text(
          "Don't have an account? ",
          style: TextStyle(color: AppTheme.textSecondary, fontSize: 14),
        ),
        TextButton(
          onPressed: () {
            Navigator.pushReplacementNamed(context, '/register');
          },
          child: const Text(
            'Create Account',
            style: TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 14,
            ),
          ),
        ),
      ],
    );
  }
}
