import React from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

/**
 * ProtectedRoute — guards routes requiring authentication and optional role authorization.
 *
 * @param {React.ReactNode} children - The protected content
 * @param {string[]} [roles] - Optional list of allowed roles
 */
function ProtectedRoute({ children, roles }) {
  const { isAuthenticated, role, loading } = useAuth()

  if (loading) {
    return (
      <div className="loading-state">
        <div className="loading-spinner" />
        <p>Loading...</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (roles && roles.length > 0 && !roles.includes(role)) {
    // Redirect to the user's appropriate dashboard
    const dashboardMap = {
      Policyholder: '/dashboard',
      ClaimsAdjuster: '/dashboard',
      Underwriter: '/dashboard',
      Admin: '/dashboard',
    }
    return <Navigate to={dashboardMap[role] || '/dashboard'} replace />
  }

  return children
}

export default ProtectedRoute
