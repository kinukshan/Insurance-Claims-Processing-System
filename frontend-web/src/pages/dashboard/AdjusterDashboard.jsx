import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

function AdjusterDashboard() {
  const { user } = useAuth()

  return (
    <div className="dashboard-container fade-in">
      <div className="dashboard-hero">
        <div className="dashboard-welcome">
          <h2>Adjuster Workspace — {user?.firstName || 'Adjuster'}</h2>
          <p className="dashboard-subtitle">Review incoming claims, investigate fraud indicators, and approve settlements.</p>
        </div>
        <div className="role-tag-pill adjuster">
          <span className="role-dot adjuster" />
          Claims Adjuster
        </div>
      </div>

      <div className="dashboard-cards-grid">
        <div className="dashboard-card">
          <div className="dashboard-card-icon">🔍</div>
          <h3>Claims Queue</h3>
          <p>Inspect active claims submissions, verify policy coverage, and advance workflows through investigation stages.</p>
          <div className="dashboard-card-actions">
            <Link to="/claims" className="btn btn--primary">Manage Claims</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">⚠️</div>
          <h3>Risk & Fraud Analysis</h3>
          <p>Examine AI-generated fraud scores, inspect anomaly flags, and review historical fraud patterns.</p>
          <div className="dashboard-card-actions">
            <Link to="/risk" className="btn btn--primary">Risk Dashboard</Link>
            <Link to="/risk/flagged" className="btn btn--secondary">Flagged Claims</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">💰</div>
          <h3>Payout Management</h3>
          <p>Calculate compensation amounts based on coverage and deductible, and track verified claim disbursements.</p>
          <div className="dashboard-card-actions">
            <Link to="/payouts" className="btn btn--primary">Payout History</Link>
            <Link to="/payouts/calculate" className="btn btn--secondary">Calculate Payout</Link>
          </div>
        </div>
      </div>
    </div>
  )
}

export default AdjusterDashboard
