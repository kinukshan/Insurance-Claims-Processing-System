// Payout approval — Component D (Kinukshan)
// Staff-facing payout review with approve/reject/revision actions

import React, { useState, useEffect } from 'react'
import PayoutBreakdown from '../../components/payout/PayoutBreakdown'
import PayoutStatusBadge from '../../components/payout/PayoutStatusBadge'
import PayoutApprovalForm from '../../components/payout/PayoutApprovalForm'
import {
  getPayoutById,
  approvePayout,
  rejectPayout,
  requestRevision,
  executePayout,
} from '../../services/payoutService'

function PayoutApproval() {
  const [payoutId, setPayoutId] = useState('')
  const [payout, setPayout] = useState(null)
  const [loading, setLoading] = useState(false)
  const [actionLoading, setActionLoading] = useState(false)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)

  const loadPayout = async () => {
    if (!payoutId.trim()) return
    setLoading(true)
    setError(null)
    setSuccess(null)
    try {
      const result = await getPayoutById(payoutId.trim())
      setPayout(result)
    } catch (err) {
      setError(err.message)
      setPayout(null)
    } finally {
      setLoading(false)
    }
  }

  const handleAction = async (actionFn, id, comments) => {
    setActionLoading(true)
    setError(null)
    setSuccess(null)
    try {
      const result = await actionFn(id, comments)
      setPayout(result)
      setSuccess('Action completed successfully.')
    } catch (err) {
      setError(err.message)
    } finally {
      setActionLoading(false)
    }
  }

  const handleExecute = async () => {
    setActionLoading(true)
    setError(null)
    setSuccess(null)
    try {
      const result = await executePayout(payout.id)
      setPayout(result)
      setSuccess('Payout execution completed.')
    } catch (err) {
      setError(err.message)
    } finally {
      setActionLoading(false)
    }
  }

  const containerStyle = { maxWidth: '700px', margin: '0 auto', padding: '24px' }
  const inputRow = { display: 'flex', gap: '8px', marginBottom: '20px' }
  const inputStyle = {
    flex: 1,
    padding: '10px 14px',
    border: '1px solid #ddd',
    borderRadius: '6px',
    fontSize: '0.95rem',
  }
  const btnStyle = {
    padding: '10px 20px',
    backgroundColor: '#1976d2',
    color: '#fff',
    border: 'none',
    borderRadius: '6px',
    cursor: 'pointer',
    fontWeight: 600,
  }

  return (
    <div style={containerStyle}>
      <h2>Payout Approval</h2>

      <div style={inputRow}>
        <input
          id="payout-lookup-id"
          type="text"
          placeholder="Enter Payout ID"
          value={payoutId}
          onChange={(e) => setPayoutId(e.target.value)}
          style={inputStyle}
        />
        <button id="payout-lookup-btn" onClick={loadPayout} disabled={loading} style={btnStyle}>
          {loading ? 'Loading...' : 'Load Payout'}
        </button>
      </div>

      {error && (
        <div id="payout-approval-error" style={{ padding: '12px', backgroundColor: '#ffebee', color: '#c62828', borderRadius: '6px', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {success && (
        <div id="payout-approval-success" style={{ padding: '12px', backgroundColor: '#e8f5e9', color: '#2e7d32', borderRadius: '6px', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {payout && (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h3 style={{ margin: 0 }}>Payout Details</h3>
            <PayoutStatusBadge status={payout.statusDisplay} />
          </div>

          <PayoutBreakdown payout={payout} />

          <div style={{ marginBottom: '16px', fontSize: '0.9rem', color: '#555' }}>
            <p><strong>Claim ID:</strong> {payout.claimId}</p>
            {payout.approvedBy && <p><strong>Approved By:</strong> {payout.approvedBy}</p>}
            {payout.paymentReference && <p><strong>Payment Ref:</strong> {payout.paymentReference}</p>}
          </div>

          {/* Show approval form only for PendingApproval payouts */}
          {payout.statusDisplay === 'PendingApproval' && (
            <PayoutApprovalForm
              payoutId={payout.id}
              onApprove={(id, c) => handleAction(approvePayout, id, c)}
              onReject={(id, c) => handleAction(rejectPayout, id, c)}
              onRevision={(id, c) => handleAction(requestRevision, id, c)}
              loading={actionLoading}
            />
          )}

          {/* Show execute button only for Approved payouts */}
          {payout.statusDisplay === 'Approved' && (
            <button
              id="payout-execute-btn"
              onClick={handleExecute}
              disabled={actionLoading}
              style={{ ...btnStyle, backgroundColor: '#00695c', marginTop: '16px' }}
            >
              {actionLoading ? 'Executing...' : '▶ Execute Payout'}
            </button>
          )}

          {/* Approval history */}
          {payout.approvals && payout.approvals.length > 0 && (
            <div style={{ marginTop: '24px' }}>
              <h4>Approval History</h4>
              {payout.approvals.map((a) => (
                <div key={a.id} style={{ padding: '10px', borderLeft: '3px solid #1976d2', marginBottom: '8px', backgroundColor: '#fafafa', borderRadius: '4px' }}>
                  <div style={{ fontWeight: 600 }}>{a.decisionDisplay} — {a.reviewerName}</div>
                  <div style={{ fontSize: '0.85rem', color: '#666' }}>{new Date(a.decisionTimestamp).toLocaleString()}</div>
                  {a.comments && <div style={{ marginTop: '4px', fontStyle: 'italic' }}>"{a.comments}"</div>}
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

export default PayoutApproval
