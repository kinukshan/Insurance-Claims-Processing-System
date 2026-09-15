// Policy list — Component A (Member 1)
// Implements policy listing with search, filter by status, loading/empty/error states

import React, { useState, useEffect, useCallback } from 'react'
import PolicyCard from '../../components/policy/PolicyCard'
import PolicyStatusBadge from '../../components/policy/PolicyStatusBadge'
import { getPolicies } from '../../services/policyService'

const STATUS_OPTIONS = ['All', 'Draft', 'Active', 'Expired', 'Lapsed', 'Cancelled']

function PolicyList({ onSelectPolicy, onCreatePolicy }) {
  const [policies, setPolicies] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [statusFilter, setStatusFilter] = useState('All')

  const fetchPolicies = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await getPolicies()
      setPolicies(data)
    } catch (err) {
      setError(err.message || 'Failed to load policies.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchPolicies()
  }, [fetchPolicies])

  const filteredPolicies = policies.filter((p) => {
    const matchesSearch =
      p.policyNumber?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      p.policyTypeName?.toLowerCase().includes(searchTerm.toLowerCase())
    const matchesStatus = statusFilter === 'All' || p.status === statusFilter
    return matchesSearch && matchesStatus
  })

  return (
    <div style={{ maxWidth: '900px', margin: '0 auto', padding: '24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <h2 style={{ margin: 0 }}>Policies</h2>
        {onCreatePolicy && (
          <button
            onClick={onCreatePolicy}
            style={{
              padding: '8px 20px',
              backgroundColor: '#3b82f6',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              cursor: 'pointer',
              fontWeight: 600,
            }}
          >
            + Create Policy
          </button>
        )}
      </div>

      {/* Search and Filter */}
      <div style={{ display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap' }}>
        <input
          type="text"
          placeholder="Search by policy number or type..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          style={{
            flex: 1,
            minWidth: '200px',
            padding: '8px 12px',
            border: '1px solid #d1d5db',
            borderRadius: '6px',
            fontSize: '0.9rem',
          }}
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          style={{
            padding: '8px 12px',
            border: '1px solid #d1d5db',
            borderRadius: '6px',
            fontSize: '0.9rem',
          }}
        >
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>

      {/* Loading */}
      {loading && (
        <div style={{ textAlign: 'center', padding: '40px', color: '#6b7280' }}>
          <p>Loading policies...</p>
        </div>
      )}

      {/* Error */}
      {error && (
        <div
          style={{
            padding: '16px',
            backgroundColor: '#fef2f2',
            border: '1px solid #fecaca',
            borderRadius: '8px',
            color: '#dc2626',
            marginBottom: '16px',
          }}
        >
          <strong>Error:</strong> {error}
          <button
            onClick={fetchPolicies}
            style={{
              marginLeft: '12px',
              padding: '4px 12px',
              backgroundColor: '#dc2626',
              color: '#fff',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
            }}
          >
            Retry
          </button>
        </div>
      )}

      {/* Empty State */}
      {!loading && !error && filteredPolicies.length === 0 && (
        <div style={{ textAlign: 'center', padding: '40px', color: '#9ca3af' }}>
          <p style={{ fontSize: '1.1rem' }}>No policies found.</p>
          {searchTerm || statusFilter !== 'All' ? (
            <p>Try adjusting your search or filter.</p>
          ) : (
            <p>Create your first policy to get started.</p>
          )}
        </div>
      )}

      {/* Policy List */}
      {!loading &&
        filteredPolicies.map((policy) => (
          <PolicyCard key={policy.id} policy={policy} onSelect={onSelectPolicy} />
        ))}

      {/* Summary */}
      {!loading && !error && policies.length > 0 && (
        <div style={{ textAlign: 'right', fontSize: '0.8rem', color: '#9ca3af', marginTop: '8px' }}>
          Showing {filteredPolicies.length} of {policies.length} policies
        </div>
      )}
    </div>
  )
}

export default PolicyList
