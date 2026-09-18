import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

function AdminDashboard() {
  const { user, role } = useAuth()

  return (
    <div className="dashboard-container fade-in">
      <div className="dashboard-hero">
        <div className="dashboard-welcome">
          <h2>Operations Command Center</h2>
          <p className="dashboard-subtitle">Full operational visibility across all insurance modules, staff workflows, and financial settlements.</p>
        </div>
        <div className="role-tag-pill admin">
          <span className="role-dot admin" />
          {role || 'Administrator'}
        </div>
      </div>

      <div className="dashboard-cards-grid">
        <div className="dashboard-card">
          <div className="dashboard-card-icon">📑</div>
          <h3>Policy Administration</h3>
          <p>Portfolio-wide policy oversight, terms management, coverage limits, and status transitions.</p>
          <div className="dashboard-card-actions">
            <Link to="/policies" className="btn btn--primary">Manage Policies</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">📋</div>
          <h3>Claims Operations</h3>
          <p>Supervise end-to-end lifecycle of all customer claims, document submissions, and validation stages.</p>
          <div className="dashboard-card-actions">
            <Link to="/claims" className="btn btn--primary">All Claims</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">🛡️</div>
          <h3>Risk & Fraud Intelligence</h3>
          <p>Review comprehensive risk dashboards, anomalous transactions, and system-wide fraud trends.</p>
          <div className="dashboard-card-actions">
            <Link to="/risk" className="btn btn--primary">Risk Overview</Link>
            <Link to="/risk/fraud-history" className="btn btn--secondary">Fraud History</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">🏛️</div>
          <h3>Financial Settlements</h3>
          <p>Inspect ledger of processed payouts, authorize high-value disbursements, and review audit records.</p>
          <div className="dashboard-card-actions">
            <Link to="/payouts" className="btn btn--primary">Payout History</Link>
            <Link to="/payouts/approval" className="btn btn--secondary">Approval Desk</Link>
          </div>
        </div>
      </div>
    </div>
  )
}

export default AdminDashboard
