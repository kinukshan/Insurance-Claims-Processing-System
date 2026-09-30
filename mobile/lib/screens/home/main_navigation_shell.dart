import 'package:flutter/material.dart';

import '../../utils/app_theme.dart';
import '../claims/claim_details_screen.dart';
import '../claims/claim_history_screen.dart';
import '../claims/claim_status_screen.dart';
import '../claims/submit_claim_screen.dart';
import '../notifications/notifications_screen.dart';
import '../payout/payout_history_screen.dart';
import '../policy/policies_screen.dart';
import '../policy/policy_details_screen.dart';
import 'home_screen.dart';

/// Main navigation shell for authenticated Policyholders.
///
/// Implements a 5-tab bottom navigation experience:
/// 1. Home — Dashboard metrics and quick actions
/// 2. Policies — My Policies list and details
/// 3. Claims — Claim history, submission, status timeline
/// 4. Payouts — Payout tracking and calculations
/// 5. Alerts — Notification history and delivery status
///
/// Uses an [IndexedStack] with nested [Navigator] instances so users can open
/// detail screens and switch tabs without losing their state or navigation history.
class MainNavigationShell extends StatefulWidget {
  final int initialIndex;

  const MainNavigationShell({super.key, this.initialIndex = 0});

  @override
  State<MainNavigationShell> createState() => _MainNavigationShellState();
}

class _MainNavigationShellState extends State<MainNavigationShell> {
  late int _currentIndex;

  final List<GlobalKey<NavigatorState>> _navigatorKeys = [
    GlobalKey<NavigatorState>(),
    GlobalKey<NavigatorState>(),
    GlobalKey<NavigatorState>(),
    GlobalKey<NavigatorState>(),
    GlobalKey<NavigatorState>(),
  ];

  @override
  void initState() {
    super.initState();
    _currentIndex = widget.initialIndex;
  }

  void switchTab(int index) {
    if (index >= 0 && index < _navigatorKeys.length) {
      setState(() => _currentIndex = index);
    }
  }

  Widget _buildTabNavigator({
    required int index,
    required Widget root,
  }) {
    return Navigator(
      key: _navigatorKeys[index],
      onGenerateRoute: (settings) {
        Widget page = root;

        switch (settings.name) {
          case '/claims/details':
            page = const ClaimDetailsScreen();
            break;
          case '/claims/status':
            page = const ClaimStatusScreen();
            break;
          case '/claims/submit':
            page = const SubmitClaimScreen();
            break;
          case '/policies/details':
            final policyId = settings.arguments as String? ?? '';
            page = PolicyDetailsScreen(policyId: policyId);
            break;
          default:
            page = root;
        }

        return MaterialPageRoute(
          settings: settings,
          builder: (_) => page,
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) return;
        final currentNavigator = _navigatorKeys[_currentIndex].currentState;
        if (currentNavigator != null && currentNavigator.canPop()) {
          currentNavigator.pop();
        } else if (_currentIndex != 0) {
          setState(() => _currentIndex = 0);
        }
      },
      child: Scaffold(
        body: IndexedStack(
          index: _currentIndex,
          children: [
            _buildTabNavigator(
              index: 0,
              root: HomeScreen(
                onNavigateTab: switchTab,
              ),
            ),
            _buildTabNavigator(
              index: 1,
              root: const PoliciesScreen(),
            ),
            _buildTabNavigator(
              index: 2,
              root: const ClaimHistoryScreen(),
            ),
            _buildTabNavigator(
              index: 3,
              root: const PayoutHistoryScreen(),
            ),
            _buildTabNavigator(
              index: 4,
              root: const NotificationsScreen(),
            ),
          ],
        ),
        bottomNavigationBar: BottomNavigationBar(
          currentIndex: _currentIndex,
          onTap: (index) {
            if (index == _currentIndex) {
              // Pop to root of current tab if tapped again
              _navigatorKeys[index].currentState?.popUntil((r) => r.isFirst);
            } else {
              setState(() => _currentIndex = index);
            }
          },
          type: BottomNavigationBarType.fixed,
          backgroundColor: Colors.white,
          selectedItemColor: AppTheme.primaryTeal,
          unselectedItemColor: AppTheme.textSecondary,
          selectedFontSize: 12,
          unselectedFontSize: 12,
          elevation: 8,
          items: const [
            BottomNavigationBarItem(
              icon: Icon(Icons.dashboard_outlined),
              activeIcon: Icon(Icons.dashboard),
              label: 'Home',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.shield_outlined),
              activeIcon: Icon(Icons.shield),
              label: 'Policies',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.assignment_outlined),
              activeIcon: Icon(Icons.assignment),
              label: 'Claims',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.payments_outlined),
              activeIcon: Icon(Icons.payments),
              label: 'Payouts',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.notifications_outlined),
              activeIcon: Icon(Icons.notifications),
              label: 'Alerts',
            ),
          ],
        ),
      ),
    );
  }
}
