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

function PolicyPage() {
  const [view, setView] = useState('list')
  const [selectedPolicyId, setSelectedPolicyId] = useState(null)

  switch (view) {
    case 'details':
      return (
        <PolicyDetails
          policyId={selectedPolicyId}
          onBack={() => setView('list')}
          onEdit={() => setView('edit')}
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
            setSelectedPolicyId(policy.id)
            setView('details')
          }}
          onCreatePolicy={() => setView('create')}
        />
      )
  }
}

export default PolicyPage
