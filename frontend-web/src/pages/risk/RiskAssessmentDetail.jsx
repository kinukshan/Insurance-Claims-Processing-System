// Risk assessment detail — Component C (Member 3)
// Shows full assessment details, fraud flags, and fraud case info for a specific claim

import React, { useState, useEffect } from 'react'
import RiskScoreBadge from '../../components/risk/RiskScoreBadge'
import FraudFlagList from '../../components/risk/FraudFlagList'
import EscalationModal from '../../components/risk/EscalationModal'
import { getAssessment, getFlags, escalateClaim } from '../../services/riskService'
import './risk.css'

/**
 * @param {{ claimId: string }} props
 */
function RiskAssessmentDetail({ claimId }) {
  const [assessment, setAssessment] = useState(null)
  const [flags, setFlags] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showEscalation, setShowEscalation] = useState(false)

  useEffect(() => {
    if (claimId) loadData()
  }, [claimId])

  async function loadData() {
    setLoading(true)
    setError(null)
    try {
      const [assessmentData, flagsData] = await Promise.all([
        getAssessment(claimId),
        getFlags(claimId),
      ])
      setAssessment(assessmentData)
      setFlags(flagsData || [])
    } catch (err) {
      setError(err.message || 'Failed to load assessment details')
    } finally {
      setLoading(false)
    }
  }

  async function handleEscalate(data) {
    if (!assessment) return
    try {
      await escalateClaim(assessment.id, data)
      setShowEscalation(false)
      loadData()
    } catch (err) {
      setError(err.message || 'Escalation failed')
    }
  }

  if (loading) {
    return (
      <div className="risk-detail-panel">
        <h2>Risk Assessment Detail</h2>
        <div className="loading-spinner"><div className="spinner" /></div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="risk-detail-panel">
        <h2>Risk Assessment Detail</h2>
        <div className="state-message error">
          <div className="icon">⚠️</div>
          <p>{error}</p>
          <button className="btn btn-primary" onClick={loadData} style={{ marginTop: '1rem' }}>Retry</button>
        </div>
      </div>
    )
  }

  if (!assessment) {
    return (
      <div className="risk-detail-panel">
        <h2>Risk Assessment Detail</h2>
        <div className="state-message">
          <div className="icon">📋</div>
          <p>No risk assessment found for this claim.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="risk-detail-panel">
      <div className="risk-detail-header">
        <h2>Risk Assessment Detail</h2>
        {!assessment.hasFraudCase && (
          <button
            className="btn btn-danger"
            onClick={() => setShowEscalation(true)}
            id="btn-escalate-detail"
          >
            Escalate to Fraud Case
          </button>
        )}
      </div>

      {/* Score & Overview */}
      <div className="risk-detail-section" id="section-overview">
        <h3>Assessment Overview</h3>
        <div className="detail-grid">
          <div className="detail-item">
            <span className="label">Risk Score</span>
            <span className="value"><RiskScoreBadge score={assessment.riskScore} level={assessment.riskLevelDisplay} /></span>
          </div>
          <div className="detail-item">
            <span className="label">Risk Level</span>
            <span className="value">{assessment.riskLevelDisplay}</span>
          </div>
          <div className="detail-item">
            <span className="label">Recommendation</span>
            <span className="value">
              <span className={`status-badge ${assessment.recommendationDisplay === 'Escalate' ? 'under-investigation' : 'resolved'}`}>
                {assessment.recommendationDisplay}
              </span>
            </span>
          </div>
          <div className="detail-item">
            <span className="label">Assessor</span>
            <span className="value">{assessment.assessorType === 1 ? 'AI Agent' : assessment.assessorType === 2 ? 'Manual' : 'System Rules'}</span>
          </div>
          <div className="detail-item">
            <span className="label">Claim ID</span>
            <span className="value" title={assessment.claimId}>{assessment.claimId}</span>
          </div>
          <div className="detail-item">
            <span className="label">Assessment Date</span>
            <span className="value">{new Date(assessment.assessmentTimestamp).toLocaleString()}</span>
          </div>
        </div>
      </div>

      {/* Summary */}
      <div className="risk-detail-section" id="section-summary">
        <h3>Assessment Summary</h3>
        <p style={{ color: '#475569', fontSize: '0.9rem', lineHeight: 1.6 }}>{assessment.summary}</p>
      </div>

      {/* Fraud Flags */}
      <div className="risk-detail-section" id="section-flags">
        <h3>Fraud Flags ({flags.length})</h3>
        <FraudFlagList flags={flags} />
      </div>

      {/* Fraud Case */}
      {assessment.hasFraudCase && (
        <div className="risk-detail-section" id="section-fraud-case">
          <h3>🔍 Fraud Case</h3>
          <p style={{ color: '#475569', fontSize: '0.9rem' }}>
            This assessment has been escalated to a fraud case for investigation.
          </p>
        </div>
      )}

      {/* Escalation Modal */}
      <EscalationModal
        isOpen={showEscalation}
        onClose={() => setShowEscalation(false)}
        onSubmit={handleEscalate}
        assessmentId={assessment.id}
      />
    </div>
  )
}

export default RiskAssessmentDetail
