// Flagged claims — Component C (Member 3)
// Staff-facing list of claims with unresolved fraud flags, with filter/sort and escalation

import React, { useState, useEffect } from 'react'
import RiskScoreBadge from '../../components/risk/RiskScoreBadge'
import EscalationModal from '../../components/risk/EscalationModal'
import { getFlaggedClaims, escalateClaim } from '../../services/riskService'
import './risk.css'

function FlaggedClaims() {
  const [claims, setClaims] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [severityFilter, setSeverityFilter] = useState('')
  const [sortField, setSortField] = useState('riskScore')
  const [sortDir, setSortDir] = useState('desc')
  const [escalationTarget, setEscalationTarget] = useState(null)

  useEffect(() => {
    loadData()
  }, [])

  async function loadData() {
    setLoading(true)
    setError(null)
    try {
      const data = await getFlaggedClaims()
      setClaims(data || [])
    } catch (err) {
      setError(err.message || 'Failed to load flagged claims')
    } finally {
      setLoading(false)
    }
  }

  async function handleEscalate(data) {
    if (!escalationTarget) return
    try {
      await escalateClaim(escalationTarget, data)
      setEscalationTarget(null)
      loadData()
    } catch (err) {
      setError(err.message || 'Escalation failed')
    }
  }

  // ── Filter & Sort ─────────────────────────────────────────
  let filtered = [...claims]

  if (severityFilter) {
    filtered = filtered.filter(c =>
      c.riskLevelDisplay?.toLowerCase() === severityFilter.toLowerCase()
    )
  }

  filtered.sort((a, b) => {
    let aVal = a[sortField]
    let bVal = b[sortField]
    if (typeof aVal === 'string') aVal = aVal.toLowerCase()
    if (typeof bVal === 'string') bVal = bVal.toLowerCase()
    if (aVal < bVal) return sortDir === 'asc' ? -1 : 1
    if (aVal > bVal) return sortDir === 'asc' ? 1 : -1
    return 0
  })

  function toggleSort(field) {
    if (sortField === field) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc')
    } else {
      setSortField(field)
      setSortDir('desc')
    }
  }

  if (loading) {
    return (
      <div className="risk-dashboard">
        <h2>Flagged Claims</h2>
        <div className="loading-spinner"><div className="spinner" /></div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="risk-dashboard">
        <h2>Flagged Claims</h2>
        <div className="state-message error">
          <div className="icon">⚠️</div>
          <p>{error}</p>
          <button className="btn btn-primary" onClick={loadData} style={{ marginTop: '1rem' }}>Retry</button>
        </div>
      </div>
    )
  }

  return (
    <div className="risk-dashboard">
      <h2>Flagged Claims</h2>

      {/* Filters */}
      <div className="filter-bar">
        <select
          id="filter-severity"
          value={severityFilter}
          onChange={(e) => setSeverityFilter(e.target.value)}
        >
          <option value="">All Severity Levels</option>
          <option value="Low">Low</option>
          <option value="Medium">Medium</option>
          <option value="High">High</option>
          <option value="Critical">Critical</option>
        </select>

        <select
          id="sort-field"
          value={sortField}
          onChange={(e) => setSortField(e.target.value)}
        >
          <option value="riskScore">Sort by Risk Score</option>
          <option value="fraudFlagCount">Sort by Flag Count</option>
          <option value="assessmentTimestamp">Sort by Date</option>
        </select>

        <button
          className="btn btn-secondary"
          onClick={() => toggleSort(sortField)}
        >
          {sortDir === 'desc' ? '↓ Desc' : '↑ Asc'}
        </button>
      </div>

      {/* Table */}
      {filtered.length === 0 ? (
        <div className="state-message">
          <div className="icon">✅</div>
          <p>{severityFilter ? 'No flagged claims match the filter.' : 'No flagged claims. All clear!'}</p>
        </div>
      ) : (
        <div className="risk-table-wrapper">
          <table className="risk-table" id="flagged-claims-table">
            <thead>
              <tr>
                <th onClick={() => toggleSort('claimId')} style={{ cursor: 'pointer' }}>Claim ID</th>
                <th onClick={() => toggleSort('riskScore')} style={{ cursor: 'pointer' }}>Risk Score</th>
                <th>Level</th>
                <th onClick={() => toggleSort('fraudFlagCount')} style={{ cursor: 'pointer' }}>Flags</th>
                <th>Recommendation</th>
                <th>Fraud Case</th>
                <th onClick={() => toggleSort('assessmentTimestamp')} style={{ cursor: 'pointer' }}>Date</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td title={a.claimId}>{a.claimId?.substring(0, 8)}...</td>
                  <td><RiskScoreBadge score={a.riskScore} level={a.riskLevelDisplay} /></td>
                  <td>{a.riskLevelDisplay}</td>
                  <td>{a.fraudFlagCount}</td>
                  <td>
                    <span className={`status-badge ${a.recommendationDisplay === 'Escalate' ? 'under-investigation' : 'resolved'}`}>
                      {a.recommendationDisplay}
                    </span>
                  </td>
                  <td>{a.hasFraudCase ? '🔍 Yes' : '—'}</td>
                  <td>{new Date(a.assessmentTimestamp).toLocaleDateString()}</td>
                  <td>
                    {!a.hasFraudCase && (
                      <button
                        className="btn btn-danger"
                        onClick={() => setEscalationTarget(a.id)}
                        id={`escalate-${a.id}`}
                      >
                        Escalate
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Escalation Modal */}
      <EscalationModal
        isOpen={!!escalationTarget}
        onClose={() => setEscalationTarget(null)}
        onSubmit={handleEscalate}
        assessmentId={escalationTarget}
      />
    </div>
  )
}

export default FlaggedClaims
