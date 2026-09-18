import React from 'react'
import { useAuth } from '../../context/AuthContext'
import CustomerDashboard from './CustomerDashboard'
import AdjusterDashboard from './AdjusterDashboard'
import AdminDashboard from './AdminDashboard'

/**
 * Main dashboard router — dynamically displays role-appropriate dashboard.
 */
function Dashboard() {
  const { role } = useAuth()

  switch (role) {
    case 'ClaimsAdjuster':
      return <AdjusterDashboard />
    case 'Underwriter':
    case 'Admin':
      return <AdminDashboard />
    case 'Policyholder':
    default:
      return <CustomerDashboard />
  }
}

export default Dashboard
