import React, { useState } from 'react'
import PayoutBreakdown from '../../components/payout/PayoutBreakdown'
import PayoutStatusBadge from '../../components/payout/PayoutStatusBadge'
import { calculatePayout } from '../../services/payoutService'

/**
 * Staff-facing payout calculation page.
 * Enters a Claim ID, triggers backend calculation.
 * All financial inputs come from the backend — not from this form.
 */
function PayoutCalculation() {
  const [claimId, setClaimId] = useState('')
  const [payout, setPayout] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)

  const handleCalculate = async (e) => {
    e.preventDefault()
    if (!claimId.trim()) return

    setLoading(true)
    setError(null)
    setSuccess(null)
    setPayout(null)

    try {
      const result = await calculatePayout(claimId.trim())
      setPayout(result)
      setSuccess('Payout proposal created successfully.')
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const containerStyle = {
    maxWidth: '700px',
    margin: '0 auto',
    padding: '24px',
  }

  const inputStyle = {
    width: '100%',
    padding: '10px 14px',
    border: '1px solid #ddd',
    borderRadius: '6px',
    fontSize: '0.95rem',
    boxSizing: 'border-box',
  }

  const btnStyle = {
    padding: '10px 24px',
    backgroundColor: '#1976d2',
    color: '#fff',
    border: 'none',
    borderRadius: '6px',
    cursor: 'pointer',
    fontWeight: 600,
    fontSize: '0.95rem',
    marginTop: '12px',
    opacity: loading ? 0.6 : 1,
  }

  return (
    <div style={containerStyle}>
      <h2>Calculate Payout</h2>
      <p style={{ color: '#666', marginBottom: '20px' }}>
        Enter a Claim ID to calculate the payout proposal.
        All financial values are retrieved from backend data.
      </p>

      <form onSubmit={handleCalculate}>
        <label htmlFor="payout-claim-id" style={{ fontWeight: 600, display: 'block', marginBottom: '6px' }}>
          Claim ID
        </label>
        <input
          id="payout-claim-id"
          type="text"
          placeholder="Enter Claim ID (GUID)"
          value={claimId}
          onChange={(e) => setClaimId(e.target.value)}
          disabled={loading}
          style={inputStyle}
        />
        <button id="payout-calculate-btn" type="submit" disabled={loading} style={btnStyle}>
          {loading ? 'Calculating...' : 'Calculate Payout'}
        </button>
      </form>

      {error && (
        <div id="payout-error" style={{ marginTop: '16px', padding: '12px', backgroundColor: '#ffebee', color: '#c62828', borderRadius: '6px' }}>
          {error}
        </div>
      )}

      {success && (
        <div id="payout-success" style={{ marginTop: '16px', padding: '12px', backgroundColor: '#e8f5e9', color: '#2e7d32', borderRadius: '6px' }}>
          {success}
        </div>
      )}

      {payout && (
        <div style={{ marginTop: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h3 style={{ margin: 0 }}>Payout Proposal</h3>
            <PayoutStatusBadge status={payout.statusDisplay} />
          </div>
          <PayoutBreakdown payout={payout} />
          <p style={{ color: '#888', fontSize: '0.85rem' }}>
            Payout ID: {payout.id}
          </p>
        </div>
      )}
    </div>
  )
}

export default PayoutCalculation
