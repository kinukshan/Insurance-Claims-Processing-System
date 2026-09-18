import React from 'react'
import PolicyStatusBadge from './PolicyStatusBadge'

/**
 * Reusable card component for displaying a policy in list views.
 */
function PolicyCard({ policy, onSelect }) {
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
      <div style={{ fontSize: '0.8rem', color: '#9ca3af', marginTop: '6px' }}>
        {new Date(policy.startDate).toLocaleDateString()} — {new Date(policy.expiryDate).toLocaleDateString()}
      </div>
    </div>
  )
}

export default PolicyCard
