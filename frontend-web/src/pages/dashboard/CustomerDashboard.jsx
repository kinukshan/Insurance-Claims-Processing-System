import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

function CustomerDashboard() {
  const { user } = useAuth()

  return (
    <div className="dashboard-container fade-in">
      <div className="dashboard-hero">
        <div className="dashboard-welcome">
          <h2>Welcome, {user?.firstName || 'Policyholder'}!</h2>
          <p className="dashboard-subtitle">Manage your insurance policies, track claims, and monitor payouts in real-time.</p>
        </div>
        <div className="role-tag-pill">
          <span className="role-dot" />
          Policyholder Portal
        </div>
      </div>

      <div className="dashboard-cards-grid">
        <div className="dashboard-card">
          <div className="dashboard-card-icon">📋</div>
          <h3>My Policies</h3>
          <p>Browse your existing insurance policies, check coverage limits and deductibles, or create a new policy.</p>
          <div className="dashboard-card-actions">
            <Link to="/policies" className="btn btn--primary">View Policies</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">📁</div>
          <h3>My Claims</h3>
          <p>File a new claim, upload required documents, and track investigation and approval progress in real-time.</p>
          <div className="dashboard-card-actions">
            <Link to="/claims" className="btn btn--primary">View Claims</Link>
          </div>
        </div>

        <div className="dashboard-card">
          <div className="dashboard-card-icon">💳</div>
          <h3>Payout Tracking</h3>
          <p>Monitor scheduled settlements, payment method status, and complete transaction history for your claims.</p>
          <div className="dashboard-card-actions">
            <Link to="/payouts" className="btn btn--primary">View Payouts</Link>
          </div>
        </div>
      </div>
    </div>
  )
}

export default CustomerDashboard
