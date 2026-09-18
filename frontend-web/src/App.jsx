import React from 'react'
import { Routes, Route, NavLink, Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import ProtectedRoute from './components/common/ProtectedRoute'

// Auth Pages
import Login from './pages/auth/Login'
import Register from './pages/auth/Register'

// Role-based Dashboards
import Dashboard from './pages/dashboard/Dashboard'

// Policy Management (Component A — Member 1)
import PolicyPage from './pages/policy/PolicyPage'

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

/**
 * Main App component with integrated Role-Based Access Control and Authentication.
 */
function App() {
  const { user, isAuthenticated, role, logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  const isStaff = role === 'ClaimsAdjuster' || role === 'Underwriter' || role === 'Admin'

  const getRoleBadgeClass = () => {
    if (role === 'ClaimsAdjuster') return 'user-badge adjuster'
    if (role === 'Admin' || role === 'Underwriter') return 'user-badge admin'
    return 'user-badge'
  }

  return (
    <div className="app-layout">
      <header className="app-header">
        <Link to={isAuthenticated ? '/dashboard' : '/login'} style={{ textDecoration: 'none' }}>
          <h1>Insurance Claims Processing System</h1>
        </Link>

        {isAuthenticated ? (
          <>
            <nav>
              <NavLink to="/dashboard" className={({ isActive }) => (isActive ? 'active' : '')}>
                Dashboard
              </NavLink>
              <NavLink to="/policies" className={({ isActive }) => (isActive ? 'active' : '')}>
                Policies
              </NavLink>
              <NavLink to="/claims" className={({ isActive }) => (isActive ? 'active' : '')}>
                Claims
              </NavLink>
              {isStaff && (
                <NavLink to="/risk" className={({ isActive }) => (isActive ? 'active' : '')}>
                  Risk
                </NavLink>
              )}
              <NavLink to="/payouts" className={({ isActive }) => (isActive ? 'active' : '')}>
                Payouts
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
          <nav>
            <NavLink to="/login" className={({ isActive }) => (isActive ? 'active' : '')}>
              Sign In
            </NavLink>
            <NavLink to="/register" className={({ isActive }) => (isActive ? 'active' : '')}>
              Register
            </NavLink>
          </nav>
        )}
      </header>

      <main className="app-content">
        <Routes>
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
              <ProtectedRoute roles={['ClaimsAdjuster', 'Underwriter', 'Admin']}>
                <PayoutApproval />
              </ProtectedRoute>
            }
          />

          {/* Root & Catch-all Redirects */}
          <Route
            path="/"
            element={<Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />}
          />
          <Route
            path="*"
            element={<Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />}
          />
        </Routes>
      </main>
    </div>
  )
}

export default App
