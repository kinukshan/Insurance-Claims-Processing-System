import React from 'react'
import PolicyStatusBadge from './PolicyStatusBadge'

/**
 * Reusable card component for displaying a policy in list views.
 */
function PolicyCard({ policy, onSelect, currentUser, onDelete }) {
  const isDraft = policy.status === 'Draft'
  const role = currentUser?.role
  const isOwner = Boolean(currentUser?.userId && policy.policyholderId && currentUser.userId === policy.policyholderId)
  const canDelete = isDraft && (
    (role === 'Policyholder' && isOwner) ||
    role === 'Underwriter' ||
    role === 'Admin'
  )

  const handleDelete = (e) => {
    e.stopPropagation()
    if (window.confirm(`Delete Policy?\nAre you sure you want to delete policy ${policy.policyNumber}?`)) {
      if (onDelete) {
        onDelete(policy)
      }
    }
  }

  return (
    <div
      onClick={() => onSelect && onSelect(policy)}
      style={{
        border: '1px solid #e5e7eb',
        borderRadius: '8px',
        padding: '16px',
        marginBottom: '12px',
        cursor: 'pointer',
        backgroundColor: '#fff',
        transition: 'box-shadow 0.2s',
      }}
      onMouseEnter={(e) => (e.currentTarget.style.boxShadow = '0 2px 8px rgba(0,0,0,0.1)')}
      onMouseLeave={(e) => (e.currentTarget.style.boxShadow = 'none')}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
        <strong style={{ fontSize: '1rem' }}>{policy.policyNumber}</strong>
        <PolicyStatusBadge status={policy.status} />
      </div>
      <div style={{ fontSize: '0.9rem', color: '#6b7280', marginBottom: '4px' }}>
        Type: {policy.policyTypeName || 'N/A'}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', color: '#374151' }}>
        <span>Premium: ${Number(policy.premium).toLocaleString()}</span>
        <span>Coverage: ${Number(policy.coverageLimit).toLocaleString()}</span>
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '6px' }}>
        <span style={{ fontSize: '0.8rem', color: '#9ca3af' }}>
          {new Date(policy.startDate).toLocaleDateString()} — {new Date(policy.expiryDate).toLocaleDateString()}
        </span>
        {canDelete && (
          <button
            onClick={handleDelete}
            style={{
              padding: '4px 12px',
              backgroundColor: '#ef4444',
              color: '#fff',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
              fontSize: '0.8rem',
              fontWeight: 600,
            }}
          >
            Delete Policy
          </button>
        )}
      </div>
    </div>
  )
}

export default PolicyCard
