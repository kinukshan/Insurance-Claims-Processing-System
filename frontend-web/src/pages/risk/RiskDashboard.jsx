// Risk dashboard — Component C (Member 3)
// Staff-facing risk assessment overview with summary metrics and recent assessments

import React, { useState, useEffect } from 'react'
import RiskScoreBadge from '../../components/risk/RiskScoreBadge'
import { getAllAssessments } from '../../services/riskService'
import './risk.css'

function RiskDashboard() {
  const [assessments, setAssessments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [searchTerm, setSearchTerm] = useState('')

  useEffect(() => {
    loadData()
  }, [])

  async function loadData() {
    setLoading(true)
    setError(null)
    try {
      const data = await getAllAssessments()
      setAssessments(data || [])
    } catch (err) {
      setError(err.message || 'Failed to load risk data')
    } finally {
      setLoading(false)
    }
  }

  // ── Computed metrics ──────────────────────────────────────
  const totalAssessments = assessments.length
  const flaggedCount = assessments.filter(a => a.fraudFlagCount > 0).length
  const openCases = assessments.filter(a => a.hasFraudCase).length
  const avgScore = totalAssessments > 0
    ? (assessments.reduce((sum, a) => sum + a.riskScore, 0) / totalAssessments).toFixed(1)
    : 0

  // ── Filtered list ─────────────────────────────────────────
  const filtered = assessments.filter(a =>
    !searchTerm ||
    a.claimId?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    a.claimNumber?.toLowerCase().includes(searchTerm.toLowerCase())
  )

  if (loading) {
    return (
      <div className="risk-dashboard">
        <h2>Risk Assessment Dashboard</h2>
        <div className="loading-spinner"><div className="spinner" /></div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="risk-dashboard">
        <h2>Risk Assessment Dashboard</h2>
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
      <h2>Risk Assessment Dashboard</h2>

      {/* Summary Cards */}
      <div className="risk-summary-cards">
        <div className="summary-card" id="card-total-assessments">
          <div className="card-label">Total Assessments</div>
          <div className="card-value">{totalAssessments}</div>
        </div>
        <div className="summary-card flagged" id="card-flagged-claims">
          <div className="card-label">Flagged Claims</div>
          <div className="card-value">{flaggedCount}</div>
        </div>
        <div className="summary-card open-cases" id="card-open-cases">
          <div className="card-label">Open Fraud Cases</div>
          <div className="card-value">{openCases}</div>
        </div>
        <div className="summary-card avg-score" id="card-avg-score">
          <div className="card-label">Avg Risk Score</div>
          <div className="card-value">{avgScore}</div>
        </div>
      </div>

      {/* Search */}
      <div className="risk-search-bar">
        <input
          id="search-claim-id"
          type="text"
          placeholder="Search by Claim ID..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
        <button onClick={() => setSearchTerm('')}>Clear</button>
      </div>

      {/* Table */}
      {filtered.length === 0 ? (
        <div className="state-message">
          <div className="icon">📋</div>
          <p>{searchTerm ? 'No matching assessments found.' : 'No risk assessments recorded yet.'}</p>
        </div>
      ) : (
        <div className="risk-table-wrapper">
          <table className="risk-table" id="assessments-table">
            <thead>
              <tr>
                <th>Claim</th>
                <th>Risk Score</th>
                <th>Level</th>
                <th>Recommendation</th>
                <th>Flags</th>
                <th>Assessor</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td title={a.claimId}>
                    {a.claimNumber ? <strong>{a.claimNumber}</strong> : `${a.claimId?.substring(0, 8)}...`}
                  </td>
                  <td><RiskScoreBadge score={a.riskScore} level={a.riskLevelDisplay} /></td>
                  <td>{a.riskLevelDisplay}</td>
                  <td>
                    <span className={`status-badge ${a.recommendationDisplay === 'Escalate' ? 'under-investigation' : 'resolved'}`}>
                      {a.recommendationDisplay}
                    </span>
                  </td>
                  <td>{a.fraudFlagCount}</td>
                  <td>{a.assessorType === 1 ? 'AI' : a.assessorType === 2 ? 'Manual' : 'System'}</td>
                  <td>{new Date(a.assessmentTimestamp).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

export default RiskDashboard
