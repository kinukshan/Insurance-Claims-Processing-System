/**
 * Policy Edit — Route wrapper.
 *
 * Reads id from the URL params and passes it to the
 * PolicyEdit component. Protected by ProtectedRoute roles Underwriter & Admin.
 */

import React from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import PolicyEdit from './PolicyEdit'

function PolicyEditRoute() {
  const { id } = useParams()
  const navigate = useNavigate()

  return (
    <PolicyEdit
      policyId={id}
      onBack={() => navigate('/policies')}
      onUpdated={() => navigate('/policies')}
    />
  )
}

export default PolicyEditRoute
