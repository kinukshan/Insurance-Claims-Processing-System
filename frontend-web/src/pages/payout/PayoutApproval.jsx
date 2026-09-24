// Payout approval — Component D (Kinukshan)
// Staff-facing payout review with approve/reject/revision actions

import React, { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import PayoutBreakdown from '../../components/payout/PayoutBreakdown'
import PayoutStatusBadge from '../../components/payout/PayoutStatusBadge'
import PayoutApprovalForm from '../../components/payout/PayoutApprovalForm'
import {
  getPayoutById,
  getPayoutByClaim,
  approvePayout,
  rejectPayout,
  requestRevision,
  executePayout,
  syncPaymentStatus,
  getPaymentProviderInfo,
} from '../../services/payoutService'

function PayoutApproval() {
  const { role } = useAuth()
  const [payoutId, setPayoutId] = useState('')
  const [payout, setPayout] = useState(null)
  const [loading, setLoading] = useState(false)
  const [actionLoading, setActionLoading] = useState(false)
  const [syncLoading, setSyncLoading] = useState(false)
  const [error, setError] = useState(null)
  const [errorType, setErrorType] = useState(null)
  const [success, setSuccess] = useState(null)
  const [provider, setProvider] = useState('Mock')

  React.useEffect(() => {
    if (typeof getPaymentProviderInfo === 'function') {
      getPaymentProviderInfo()
        .then((info) => {
          if (info?.provider) {
            setProvider(info.provider)
          }
        })
        .catch(() => {})
    }
  }, [])

  const canApprove = role === 'Underwriter' || role === 'Admin'
  const canExecute = role === 'Admin'

  const sanitizeErrorMessage = (msg) => {
    if (!msg || typeof msg !== 'string') return 'An unexpected error occurred while executing the payout.'
    const lower = msg.toLowerCase()
    // Do not expose stack traces, SQL details, connection strings, secrets, or internal provider credentials
    if (
      lower.includes('exception') ||
      lower.includes('stack trace') ||
      lower.includes('sql') ||
      lower.includes('npgsql') ||
      lower.includes('relation') ||
      lower.includes('column') ||
      lower.includes('table') ||
      lower.includes('connection') ||
      lower.includes('host=') ||
      lower.includes('password') ||
      lower.includes('secret') ||
      lower.includes('credential') ||
      lower.includes('bearer ') ||
      lower.includes('at insuranceclaims') ||
      lower.includes('internal server error')
    ) {
      return 'Payment execution failed due to an internal system error. Please contact an administrator.'
    }
    return msg
  }

  const handleError = (err) => {
    if (err.status === 401) {
      setError('Your session has expired. Please log in again.')
      setErrorType('auth')
    } else if (err.status === 403) {
      setError('You do not have permission to perform this action.')
      setErrorType('forbidden')
    } else if (err.status === 404) {
      setError('Payout not found.')
      setErrorType('notfound')
    } else if (err.status === 409) {
      setError(sanitizeErrorMessage(err.message) || 'This payout was modified or already processed by another user. Please reload.')
      setErrorType('conflict')
    } else if (err.status === 502) {
      setError(sanitizeErrorMessage(err.message) || 'Payment gateway encountered an error. The payout was not completed.')
      setErrorType('gateway')
    } else {
      setError(sanitizeErrorMessage(err.message))
      setErrorType('error')
    }
  }

  const loadPayout = async () => {
    if (!payoutId.trim()) return
    setLoading(true)
    setError(null)
    setErrorType(null)
    setSuccess(null)
    try {
      // Try loading by payout ID first, then by claim ID
      let result
      try {
        result = await getPayoutById(payoutId.trim())
      } catch (firstErr) {
        if (firstErr.status === 404) {
          result = await getPayoutByClaim(payoutId.trim())
        } else {
          throw firstErr
        }
      }
      setPayout(result)
      if (result.paymentProvider) {
        setProvider(result.paymentProvider)
      }
    } catch (err) {
      handleError(err)
      setPayout(null)
    } finally {
      setLoading(false)
    }
  }

  const handleAction = async (actionFn, id, comments) => {
    if (actionLoading) return
    setActionLoading(true)
    setError(null)
    setErrorType(null)
    setSuccess(null)
    try {
      const result = await actionFn(id, comments)
      setPayout(result)
      setSuccess('Action completed successfully.')
    } catch (err) {
      handleError(err)
    } finally {
      setActionLoading(false)
    }
  }

  const handleExecute = async () => {
    if (actionLoading) return
    setActionLoading(true)
    setError(null)
    setErrorType(null)
    setSuccess(null)
    try {
      const result = await executePayout(payout.id)
      if (result && result.status) {
        if (result.provider) {
          setProvider(result.provider)
        }
        try {
          const refreshed = await getPayoutById(payout.id)
          setPayout(refreshed)
          if (refreshed.paymentProvider) {
            setProvider(refreshed.paymentProvider)
          }
        } catch {
          setPayout(prev => ({
            ...prev,
            statusDisplay: result.status === 'Succeeded' ? 'Paid' : result.status,
            paymentReference: result.providerTransactionId || prev.paymentReference
          }))
        }
        setSuccess(result.message || `Payout execution ${result.status.toLowerCase()}.`)
      } else {
        setPayout(result)
        setSuccess('Payout execution completed.')
      }
    } catch (err) {
      handleError(err)
    } finally {
      setActionLoading(false)
    }
  }

  const handleSync = async () => {
    if (syncLoading || !payout) return
    setSyncLoading(true)
    setError(null)
    setSuccess(null)
    try {
      const res = await syncPaymentStatus(payout.id)
      if (res?.provider) {
        setProvider(res.provider)
      }
      const refreshed = await getPayoutById(payout.id)
      setPayout(refreshed)
      if (refreshed.paymentProvider) {
        setProvider(refreshed.paymentProvider)
      }
      setSuccess(res.message || 'Payment status synchronized.')
    } catch (err) {
      handleError(err)
    } finally {
      setSyncLoading(false)
    }
  }

  const getAdminStateLabel = (statusDisplay) => {
    switch (statusDisplay) {
      case 'Processing':
        return 'Payment Processing'
      case 'Paid':
        return 'Payment Completed'
      case 'Failed':
        return 'Payment Failed'
      case 'Unclaimed':
        return 'Awaiting Recipient'
      case 'OnHold':
      case 'Blocked':
        return 'Payment On Hold / Review Required'
      default:
        return null
    }
  }

  const effectiveProvider = payout?.paymentProvider || provider
  const isPayPal = effectiveProvider && effectiveProvider.toLowerCase().includes('paypal')

  const getProviderLabel = (p) => {
    if (!p) return 'Mock Payment Gateway'
    const lower = p.toLowerCase()
    if (lower.includes('paypal')) return 'PayPal Sandbox'
    if (lower.includes('mock')) return 'Mock Payment Gateway'
    return `${p} Payment Gateway`
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

  const errorBgMap = { auth: '#fff3e0', forbidden: '#fce4ec', notfound: '#e3f2fd', conflict: '#fff8e1', gateway: '#fff3e0', error: '#ffebee' }
  const errorColorMap = { auth: '#e65100', forbidden: '#ad1457', notfound: '#1565c0', conflict: '#f57f17', gateway: '#e65100', error: '#c62828' }

  return (
    <div style={containerStyle}>
      <h2>Payout Approval</h2>

      <div style={inputRow}>
        <input
          id="payout-lookup-id"
          type="text"
          placeholder="Enter Payout ID or Claim ID"
          value={payoutId}
          onChange={(e) => setPayoutId(e.target.value)}
          style={inputStyle}
        />
        <button id="payout-lookup-btn" onClick={loadPayout} disabled={loading} style={btnStyle}>
          {loading ? 'Loading...' : 'Load Payout'}
        </button>
      </div>

      {error && (
        <div id="payout-approval-error" style={{ padding: '12px', backgroundColor: errorBgMap[errorType] || '#ffebee', color: errorColorMap[errorType] || '#c62828', borderRadius: '6px', marginBottom: '16px' }}>
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
            <p><strong>Claim ID:</strong> <code style={{ fontSize: '0.8rem' }}>{payout.claimId}</code></p>
            {payout.claimNumber && <p><strong>Claim:</strong> {payout.claimNumber}</p>}
            {payout.approvedBy && <p><strong>Approved By:</strong> {payout.approvedBy}</p>}
            <p><strong>Provider:</strong> {getProviderLabel(effectiveProvider)}</p>
            {getAdminStateLabel(payout.statusDisplay) && (
              <p><strong>Payment Status:</strong> <span style={{ fontWeight: 600 }}>{getAdminStateLabel(payout.statusDisplay)}</span></p>
            )}
            {payout.paymentReference && <p><strong>Payment Ref:</strong> {payout.paymentReference}</p>}
          </div>

          {/* Show approval form only for PendingApproval payouts AND authorized roles */}
          {payout.statusDisplay === 'PendingApproval' && canApprove && (
            <PayoutApprovalForm
              payoutId={payout.id}
              onApprove={(id, c) => handleAction(approvePayout, id, c)}
              onReject={(id, c) => handleAction(rejectPayout, id, c)}
              onRevision={(id, c) => handleAction(requestRevision, id, c)}
              loading={actionLoading}
            />
          )}

          {/* Show message when ClaimsAdjuster views a pending payout */}
          {payout.statusDisplay === 'PendingApproval' && !canApprove && (
            <div style={{ padding: '12px', backgroundColor: '#fff3e0', color: '#e65100', borderRadius: '6px', marginTop: '16px' }}>
              This payout is pending approval. An Underwriter or Admin must approve or reject it.
            </div>
          )}

          {/* Show execute button only for Approved payouts AND Admin role */}
          {payout.statusDisplay === 'Approved' && canExecute && (
            <div style={{ marginTop: '16px' }}>
              <button
                id="payout-execute-btn"
                onClick={handleExecute}
                disabled={actionLoading}
                style={{ ...btnStyle, backgroundColor: '#00695c', opacity: actionLoading ? 0.6 : 1 }}
              >
                {actionLoading ? 'Executing...' : '▶ Execute Payout'}
              </button>
              {actionLoading && (
                <div style={{ marginTop: '6px', fontSize: '0.85rem', color: '#555' }}>
                  {isPayPal ? 'Sending to PayPal Sandbox...' : 'Processing Payment...'}
                </div>
              )}
            </div>
          )}

          {/* Show sync button when payout is in Processing/Unclaimed/OnHold for Admin */}
          {(payout.statusDisplay === 'Processing' || payout.statusDisplay === 'Unclaimed' || payout.statusDisplay === 'OnHold') && canExecute && (
            <div style={{ marginTop: '16px' }}>
              <button
                id="payout-sync-btn"
                onClick={handleSync}
                disabled={syncLoading}
                style={{ ...btnStyle, backgroundColor: '#455a64', opacity: syncLoading ? 0.6 : 1 }}
              >
                {syncLoading ? 'Syncing...' : '↻ Refresh Payment Status'}
              </button>
            </div>
          )}

          {/* Show message when non-Admin views an approved payout */}
          {payout.statusDisplay === 'Approved' && !canExecute && (
            <div style={{ padding: '12px', backgroundColor: '#e3f2fd', color: '#1565c0', borderRadius: '6px', marginTop: '16px' }}>
              This payout is approved and ready for execution. An Admin must execute it.
            </div>
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
