import React, { useState, useCallback } from 'react'
import { Routes, Route, NavLink, Link, Navigate, useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import ProtectedRoute from './components/common/ProtectedRoute'
import Logo from './components/common/Logo'

// Auth Pages
import Login from './pages/auth/Login'
import Register from './pages/auth/Register'

// Homepage
import Homepage from './pages/home/Homepage'

// Role-based Dashboards
import Dashboard from './pages/dashboard/Dashboard'

// Policy Management (Component A — Member 1)
import PolicyPage from './pages/policy/PolicyPage'
import PolicyEditRoute from './pages/policy/PolicyEditRoute'

// Claims Management (Component B — Member 2)
import ClaimsList from './pages/claims/ClaimsList'
import ClaimDetails from './pages/claims/ClaimDetails'

// Risk Assessment / Fraud (Component C — Member 3)
import RiskDashboard from './pages/risk/RiskDashboard'
import FlaggedClaims from './pages/risk/FlaggedClaims'
import RiskAssessmentDetailRoute from './pages/risk/RiskAssessmentDetailRoute'
import FraudCaseHistory from './pages/risk/FraudCaseHistory'

// Payout Processing (Component D — Kinukshan)
import PayoutHistory from './pages/payout/PayoutHistory'
import PayoutCalculation from './pages/payout/PayoutCalculation'
import PayoutApproval from './pages/payout/PayoutApproval'

// Notifications
import NotificationHistory from './pages/notifications/NotificationHistory'

/**
 * Main App component with integrated Role-Based Access Control and Authentication.
 */
function App() {
  const { user, isAuthenticated, role, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  const handleLogout = () => {
    logout()
    setMobileMenuOpen(false)
    navigate('/login')
  }

  const closeMobileMenu = useCallback(() => {
    setMobileMenuOpen(false)
  }, [])

  const isStaff = role === 'ClaimsAdjuster' || role === 'Underwriter' || role === 'Admin'

  const getRoleBadgeClass = () => {
    if (role === 'ClaimsAdjuster') return 'user-badge adjuster'
    if (role === 'Admin' || role === 'Underwriter') return 'user-badge admin'
    return 'user-badge'
  }

  // Determine if we're on the public homepage (unauthenticated root)
  const isHomepage = location.pathname === '/' && !isAuthenticated

  return (
    <div className="app-layout">
      <a href="#main-content" className="skip-link">Skip to main content</a>

      <header className="app-header">
        <Link
          to={isAuthenticated ? '/dashboard' : '/'}
          className="header-brand"
          onClick={closeMobileMenu}
        >
          <Logo size={38} />
          <h1>
            <span className="brand-accent">Insurance</span> Claims<br />
            Processing System
          </h1>
        </Link>

        {isAuthenticated ? (
          <>
            <button
              className="mobile-menu-toggle"
              onClick={() => setMobileMenuOpen(prev => !prev)}
              aria-label={mobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
              aria-expanded={mobileMenuOpen}
            >
              {mobileMenuOpen ? '✕' : '☰'}
            </button>
            <nav className={mobileMenuOpen ? 'mobile-open' : ''} role="navigation">
              <NavLink to="/dashboard" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Dashboard
              </NavLink>
              <NavLink to="/policies" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Policies
              </NavLink>
              <NavLink to="/claims" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Claims
              </NavLink>
              {isStaff && (
                <NavLink to="/risk" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                  Risk
                </NavLink>
              )}
              <NavLink to="/payouts" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Payouts
              </NavLink>
              <NavLink to="/notifications" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Notifications
              </NavLink>
            </nav>

            <div className="header-right">
              <div className="user-chip">
                <span>{user?.firstName} {user?.lastName}</span>
                <span className={getRoleBadgeClass()}>{role}</span>
              </div>
              <button onClick={handleLogout} className="btn-logout" title="Sign out">
                Logout
              </button>
            </div>
          </>
        ) : (
          <>
            <button
              className="mobile-menu-toggle"
              onClick={() => setMobileMenuOpen(prev => !prev)}
              aria-label={mobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
              aria-expanded={mobileMenuOpen}
            >
              {mobileMenuOpen ? '✕' : '☰'}
            </button>
            <nav className={mobileMenuOpen ? 'mobile-open' : ''} role="navigation">
              <NavLink to="/" end className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Home
              </NavLink>
              <NavLink to="/login" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Sign In
              </NavLink>
              <NavLink to="/register" className={({ isActive }) => (isActive ? 'active' : '')} onClick={closeMobileMenu}>
                Register
              </NavLink>
            </nav>
          </>
        )}
      </header>

      <main id="main-content" className={isHomepage ? '' : 'app-content'}>
        <Routes>
          {/* Public Homepage */}
          <Route
            path="/"
            element={isAuthenticated ? <Navigate to="/dashboard" replace /> : <Homepage />}
          />

          {/* Public Auth Routes */}
          <Route
            path="/login"
            element={isAuthenticated ? <Navigate to="/dashboard" replace /> : <Login />}
          />
          <Route
            path="/register"
            element={isAuthenticated ? <Navigate to="/dashboard" replace /> : <Register />}
          />

          {/* Role-based Dashboard */}
          <Route
            path="/dashboard"
            element={
              <ProtectedRoute>
                <Dashboard />
              </ProtectedRoute>
            }
          />

          {/* Policy Management */}
          <Route
            path="/policies"
            element={
              <ProtectedRoute>
                <PolicyPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/policies/:id/edit"
            element={
              <ProtectedRoute roles={['Underwriter', 'Admin']}>
                <PolicyEditRoute />
              </ProtectedRoute>
            }
          />
          <Route
            path="/policies/edit/:id"
            element={
              <ProtectedRoute roles={['Underwriter', 'Admin']}>
                <PolicyEditRoute />
              </ProtectedRoute>
            }
          />

          {/* Claims Management */}
          <Route
            path="/claims"
            element={
              <ProtectedRoute>
                <ClaimsList />
              </ProtectedRoute>
            }
          />
          <Route
            path="/claims/:id"
            element={
              <ProtectedRoute>
                <ClaimDetails />
              </ProtectedRoute>
            }
          />

          {/* Risk Assessment / Fraud (Staff Only) */}
          <Route
            path="/risk"
            element={
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <RiskDashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/risk/flagged"
            element={
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <FlaggedClaims />
              </ProtectedRoute>
            }
          />
          <Route
            path="/risk/assessment/:claimId"
            element={
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <RiskAssessmentDetailRoute />
              </ProtectedRoute>
            }
          />
          <Route
            path="/risk/fraud-history"
            element={
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <FraudCaseHistory />
              </ProtectedRoute>
            }
          />

          {/* Payout Processing */}
          <Route
            path="/payouts"
            element={
              <ProtectedRoute>
                <PayoutHistory />
              </ProtectedRoute>
            }
          />
          <Route
            path="/payouts/calculate"
            element={
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <PayoutCalculation />
              </ProtectedRoute>
            }
          />
          <Route
            path="/payouts/approval"
            element={
              <ProtectedRoute roles={['Underwriter', 'Admin']}>
                <PayoutApproval />
              </ProtectedRoute>
            }
          />

          {/* Notification History */}
          <Route
            path="/notifications"
            element={
              <ProtectedRoute>
                <NotificationHistory />
              </ProtectedRoute>
            }
          />

          {/* Catch-all Redirect */}
          <Route
            path="*"
            element={<Navigate to={isAuthenticated ? '/dashboard' : '/'} replace />}
          />
        </Routes>
      </main>
    </div>
  )
}

export default App
