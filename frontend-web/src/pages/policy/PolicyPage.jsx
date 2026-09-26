/**
 * Policy Page — Integration wrapper for prop-driven Policy components.
 *
 * Manages internal view state (list / details / create / edit) and
 * delegates to existing Policy pages via their callback props.
 * This avoids refactoring the original prop-driven Policy components
 * to use React Router params.
 */

import React, { useState } from 'react'
import PolicyList from './PolicyList'
import PolicyDetails from './PolicyDetails'
import PolicyCreate from './PolicyCreate'
import PolicyEdit from './PolicyEdit'
import { useAuth } from '../../context/AuthContext'

function useOptionalAuth() {
  try {
    const auth = useAuth()
    return {
      role: auth.role || auth.user?.role || null,
      user: auth.user || null,
    }
  } catch {
    return { role: null, user: null }
  }
}

function PolicyPage() {
  const { role, user } = useOptionalAuth()
  const currentRole = role || user?.role
  const canEdit = currentRole === 'Underwriter' || currentRole === 'Admin'

  const [view, setView] = useState('list')
  const [selectedPolicyId, setSelectedPolicyId] = useState(null)

  switch (view) {
    case 'details':
      return (
        <PolicyDetails
          policyId={selectedPolicyId}
          onBack={() => setView('list')}
          onEdit={canEdit ? () => setView('edit') : undefined}
        />
      )
    case 'create':
      return (
        <PolicyCreate
          onBack={() => setView('list')}
          onCreated={(newPolicy) => {
            setSelectedPolicyId(newPolicy?.id || null)
            setView(newPolicy?.id ? 'details' : 'list')
          }}
        />
      )
    case 'edit':
      if (!canEdit) {
        return (
          <div style={{ maxWidth: '600px', margin: '40px auto', padding: '24px', textAlign: 'center' }}>
            <h2>Access Denied</h2>
            <p style={{ color: '#dc2626' }}>You do not have permission to edit policies.</p>
            <button
              onClick={() => setView('list')}
              style={{
                marginTop: '16px',
                padding: '8px 16px',
                cursor: 'pointer',
                backgroundColor: '#3b82f6',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 500,
              }}
            >
              Back to Policies
            </button>
          </div>
        )
      }
      return (
        <PolicyEdit
          policyId={selectedPolicyId}
          onBack={() => setView('details')}
          onUpdated={() => setView('details')}
        />
      )
    default:
      return (
        <PolicyList
          onSelectPolicy={(policy) => {
            setSelectedPolicyId(policy?.id || policy)
            setView('details')
          }}
          onCreatePolicy={() => setView('create')}
        />
      )
  }
}

export default PolicyPage
