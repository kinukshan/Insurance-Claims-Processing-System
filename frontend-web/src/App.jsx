import React from 'react'
import { Routes, Route, NavLink, Navigate } from 'react-router-dom'

// Claims (Component B — Member 2)
import ClaimsList from './pages/claims/ClaimsList'
import ClaimDetails from './pages/claims/ClaimDetails'

// Policy (Component A — Member 1)
import PolicyPage from './pages/policy/PolicyPage'

// Risk Assessment / Fraud (Component C — Member 3)
import RiskDashboard from './pages/risk/RiskDashboard'
import FlaggedClaims from './pages/risk/FlaggedClaims'
import RiskAssessmentDetailRoute from './pages/risk/RiskAssessmentDetailRoute'
import FraudCaseHistory from './pages/risk/FraudCaseHistory'

// Payout Processing (Component D — Kinukshan)
import PayoutHistory from './pages/payout/PayoutHistory'
import PayoutCalculation from './pages/payout/PayoutCalculation'
import PayoutApproval from './pages/payout/PayoutApproval'

/**
 * Main App component — integrated by all 4 team members.
 *
 * Navigation: Policies | Claims | Risk | Payouts
 */
function App() {
  return (
    <div className="app-layout">
      <header className="app-header">
        <h1>Insurance Claims Processing System</h1>
        <nav>
          <NavLink to="/policies" className={({ isActive }) => isActive ? 'active' : ''}>
            Policies
          </NavLink>
          <NavLink to="/claims" className={({ isActive }) => isActive ? 'active' : ''}>
            Claims
          </NavLink>
          <NavLink to="/risk" className={({ isActive }) => isActive ? 'active' : ''}>
            Risk
          </NavLink>
          <NavLink to="/payouts" className={({ isActive }) => isActive ? 'active' : ''}>
            Payouts
          </NavLink>
        </nav>
      </header>
      <main className="app-content">
        <Routes>
          {/* Default redirect */}
          <Route path="/" element={<Navigate to="/claims" replace />} />

          {/* Policy Management */}
          <Route path="/policies" element={<PolicyPage />} />

          {/* Claims Management */}
          <Route path="/claims" element={<ClaimsList />} />
          <Route path="/claims/:id" element={<ClaimDetails />} />

          {/* Risk Assessment / Fraud */}
          <Route path="/risk" element={<RiskDashboard />} />
          <Route path="/risk/flagged" element={<FlaggedClaims />} />
          <Route path="/risk/assessment/:claimId" element={<RiskAssessmentDetailRoute />} />
          <Route path="/risk/fraud-history" element={<FraudCaseHistory />} />

          {/* Payout Processing */}
          <Route path="/payouts" element={<PayoutHistory />} />
          <Route path="/payouts/calculate" element={<PayoutCalculation />} />
          <Route path="/payouts/approval" element={<PayoutApproval />} />
        </Routes>
      </main>
    </div>
  )
}

export default App
