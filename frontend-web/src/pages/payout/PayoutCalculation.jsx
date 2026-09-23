import React, { useState, useEffect, useCallback } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import PayoutBreakdown from '../../components/payout/PayoutBreakdown'
import PayoutStatusBadge from '../../components/payout/PayoutStatusBadge'
import { calculatePayout, getPayoutByClaim } from '../../services/payoutService'
import { getClaim } from '../../services/claimService'
import { getPolicyById } from '../../services/policyService'

/**
 * Staff-facing payout calculation page.
 * Enters a Claim ID or receives it preloaded from Claim Details (?claimId=guid).
 * Preloads authoritative claim & policy context.
 * Detects existing payouts to prevent duplicates and link to Approval Desk.
 * All financial inputs come from the backend — not from this form.
 */
function PayoutCalculation() {
  const [searchParams] = useSearchParams()
  const queryClaimId = searchParams.get('claimId')

  const [claimId, setClaimId] = useState(queryClaimId || '')
  const [payout, setPayout] = useState(null)
  const [claimContext, setClaimContext] = useState(null)
  const [existingPayoutLoaded, setExistingPayoutLoaded] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [errorType, setErrorType] = useState(null)
  const [success, setSuccess] = useState(null)
  const [infoMessage, setInfoMessage] = useState(null)

  const formatCurrency = (val) => {
    if (val == null || isNaN(val)) return '—'
    return `$${Number(val).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
  }

  const loadPreloadedClaim = useCallback(async (targetClaimId) => {
    if (!targetClaimId || !targetClaimId.trim()) return

    const cleanId = targetClaimId.trim()
    setError(null)
    setErrorType(null)
    setSuccess(null)
    setInfoMessage(null)

    // 1. Check for existing payout via getPayoutByClaim
    try {
      const existing = await getPayoutByClaim(cleanId)
      if (existing) {
        setPayout(existing)
        setExistingPayoutLoaded(true)
        setInfoMessage(`Existing payout proposal found for this claim (Status: ${existing.statusDisplay || 'PendingApproval'}). Duplicates cannot be created.`)
      } else {
        setPayout(null)
        setExistingPayoutLoaded(false)
      }
    } catch (err) {
      if (err.status === 404) {
        // Expected: no payout exists yet for this claim
        setPayout(null)
        setExistingPayoutLoaded(false)
      } else if (err.status === 401) {
        setError('Your session has expired. Please log in again.')
        setErrorType('auth')
        setPayout(null)
        setExistingPayoutLoaded(false)
      } else if (err.status === 403) {
        setError('You do not have permission to access payouts.')
        setErrorType('forbidden')
        setPayout(null)
        setExistingPayoutLoaded(false)
      } else {
        // 500 or network failure treated as real error
        setError(err.message || 'Failed to check existing payout.')
        setErrorType('error')
        setPayout(null)
        setExistingPayoutLoaded(false)
      }
    }

    // 2. Fetch authoritative claim context
    try {
      const claimData = await getClaim(cleanId)
      if (claimData) {
        let policyData = null
        if (claimData.policyId) {
          try {
            policyData = await getPolicyById(claimData.policyId)
          } catch {
            // Policy fetch is optional/supplemental
          }
        }
        setClaimContext({
          claimNumber: claimData.claimNumber,
          claimedAmount: claimData.claimedAmount,
          claimType: claimData.claimType,
          status: claimData.status,
          policyNumber: policyData?.policyNumber,
          policyType: policyData?.policyTypeName,
          coverageLimit: policyData?.coverageLimit,
          deductible: policyData?.deductible,
        })
      }
    } catch {
      // Claim context is supplemental; financial calculation remains authoritative from backend
    }
  }, [])

  useEffect(() => {
    if (queryClaimId && queryClaimId.trim()) {
      const trimmed = queryClaimId.trim()
      setClaimId(trimmed)
      loadPreloadedClaim(trimmed)
    }
  }, [queryClaimId, loadPreloadedClaim])

  const handleCalculate = async (e) => {
    if (e) e.preventDefault()
    if (!claimId.trim() || loading) return

    // Prevent duplicate calculation if an existing payout proposal is already loaded
    if (existingPayoutLoaded && payout && payout.statusDisplay !== 'Draft') {
      setError(`A payout proposal already exists for claim '${claimId.trim()}' with status '${payout.statusDisplay}'. Duplicates cannot be created.`)
      setErrorType('conflict')
      return
    }

    setLoading(true)
    setError(null)
    setErrorType(null)
    setSuccess(null)
    setInfoMessage(null)

    try {
      const result = await calculatePayout(claimId.trim())
      setPayout(result)
      setExistingPayoutLoaded(true)
      setSuccess('Payout proposal created successfully.')
    } catch (err) {
      if (err.status === 401) {
        setError('Your session has expired. Please log in again.')
        setErrorType('auth')
      } else if (err.status === 403) {
        setError('You do not have permission to calculate payouts.')
        setErrorType('forbidden')
      } else if (err.status === 409 || err.message?.includes('already exists')) {
        setError(err.message || 'A payout already exists for this claim.')
        setErrorType('conflict')
        // Automatically load the existing payout so user can review it
        try {
          const existing = await getPayoutByClaim(claimId.trim())
          if (existing) {
            setPayout(existing)
            setExistingPayoutLoaded(true)
          }
        } catch {}
      } else {
        setError(err.message || 'Payout calculation failed.')
        setErrorType('error')
      }
    } finally {
      setLoading(false)
    }
  }

  const handleLookup = async () => {
    if (!claimId.trim() || loading) return

    setLoading(true)
    setError(null)
    setErrorType(null)
    setSuccess(null)
    setInfoMessage(null)
    setPayout(null)
    setExistingPayoutLoaded(false)

    try {
      const result = await getPayoutByClaim(claimId.trim())
      setPayout(result)
      setExistingPayoutLoaded(true)
    } catch (err) {
      setExistingPayoutLoaded(false)
      if (err.status === 404) {
        setError('No payout found for this claim. Use "Calculate Payout" to create one.')
        setErrorType('info')
      } else if (err.status === 401) {
        setError('Your session has expired. Please log in again.')
        setErrorType('auth')
      } else if (err.status === 403) {
        setError('You do not have permission to access payouts.')
        setErrorType('forbidden')
      } else {
        setError(err.message || 'Failed to look up payout.')
        setErrorType('error')
      }
    } finally {
      setLoading(false)
    }

    // Also attempt to load authoritative context on manual lookup
    try {
      const claimData = await getClaim(claimId.trim())
      if (claimData) {
        let policyData = null
        if (claimData.policyId) {
          try {
            policyData = await getPolicyById(claimData.policyId)
          } catch {}
        }
        setClaimContext({
          claimNumber: claimData.claimNumber,
          claimedAmount: claimData.claimedAmount,
          claimType: claimData.claimType,
          status: claimData.status,
          policyNumber: policyData?.policyNumber,
          policyType: policyData?.policyTypeName,
          coverageLimit: policyData?.coverageLimit,
          deductible: policyData?.deductible,
        })
      }
    } catch {}
  }

  const validation = payout?.validationResult
  const isBlockedClaim = claimContext && ['Draft', 'Withdrawn', 'Rejected', 'Closed'].includes(claimContext.status)

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
    cursor: loading || isBlockedClaim ? 'not-allowed' : 'pointer',
    fontWeight: 600,
    fontSize: '0.95rem',
    marginTop: '12px',
    opacity: loading || isBlockedClaim ? 0.6 : 1,
  }

  const errorBgMap = { auth: '#fff3e0', forbidden: '#fce4ec', conflict: '#fff8e1', info: '#e3f2fd', error: '#ffebee' }
  const errorColorMap = { auth: '#e65100', forbidden: '#ad1457', conflict: '#f57f17', info: '#1565c0', error: '#c62828' }

  return (
    <div style={containerStyle}>
      <h2>Calculate Payout</h2>
      <p style={{ color: '#666', marginBottom: '20px' }}>
        Enter a Claim ID to calculate the payout proposal.
        All financial values are retrieved from backend data.
      </p>

      {/* Authoritative Claim & Policy Context panel */}
      {(claimContext || payout) && (
        <div id="authoritative-claim-context" style={{ marginBottom: '20px', padding: '16px', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
          <h4 style={{ margin: '0 0 12px', color: '#1e3a8a', fontSize: '0.95rem' }}>Authoritative Claim & Policy Context</h4>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px' }}>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Claim Number</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>{claimContext?.claimNumber || payout?.claimNumber || '—'}</strong>
            </div>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Claimed Amount</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>
                {claimContext?.claimedAmount != null
                  ? formatCurrency(claimContext.claimedAmount)
                  : (payout?.approvedClaimAmount != null ? formatCurrency(payout.approvedClaimAmount) : '—')}
              </strong>
            </div>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Policy Coverage Limit</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>
                {claimContext?.coverageLimit != null
                  ? formatCurrency(claimContext.coverageLimit)
                  : (payout?.coverageLimit != null ? formatCurrency(payout.coverageLimit) : '—')}
              </strong>
            </div>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Deductible</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>
                {claimContext?.deductible != null
                  ? formatCurrency(claimContext.deductible)
                  : (payout?.deductible != null ? formatCurrency(payout.deductible) : '—')}
              </strong>
            </div>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Claim Type</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>{claimContext?.claimType || '—'}</strong>
            </div>
            <div>
              <span style={{ display: 'block', fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Policy Type</span>
              <strong style={{ fontSize: '0.95rem', color: '#0f172a' }}>{claimContext?.policyType || '—'}</strong>
            </div>
          </div>
        </div>
      )}

      {isBlockedClaim && (
        <div id="payout-blocked-claim-notice" style={{ marginBottom: '16px', padding: '12px', backgroundColor: '#fff3e0', color: '#e65100', borderRadius: '6px', fontSize: '0.9rem' }}>
          ⚠️ Claim {claimContext?.claimNumber || claimId} has status "{claimContext?.status}" and is not eligible for payout preparation.
        </div>
      )}

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
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            id="payout-calculate-btn"
            type="submit"
            disabled={loading || isBlockedClaim || existingPayoutLoaded}
            style={btnStyle}
            title={existingPayoutLoaded ? 'A payout proposal already exists' : isBlockedClaim ? 'Claim is not eligible' : 'Calculate Payout'}
          >
            {loading ? 'Processing...' : existingPayoutLoaded ? 'Proposal Already Created' : 'Calculate Payout'}
          </button>
          <button
            id="payout-lookup-btn"
            type="button"
            onClick={handleLookup}
            disabled={loading}
            style={{ ...btnStyle, backgroundColor: '#546e7a', cursor: loading ? 'not-allowed' : 'pointer', opacity: loading ? 0.6 : 1 }}
          >
            Look Up Existing
          </button>
        </div>
      </form>

      {infoMessage && (
        <div id="payout-info-notice" style={{ marginTop: '16px', padding: '12px', backgroundColor: '#e3f2fd', color: '#1565c0', borderRadius: '6px' }}>
          {infoMessage}
        </div>
      )}

      {error && (
        <div id="payout-error" style={{ marginTop: '16px', padding: '12px', backgroundColor: errorBgMap[errorType] || '#ffebee', color: errorColorMap[errorType] || '#c62828', borderRadius: '6px' }}>
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

          {payout.claimNumber && (
            <p style={{ color: '#555', fontSize: '0.85rem' }}>
              Claim: {payout.claimNumber}
            </p>
          )}
          <p style={{ color: '#888', fontSize: '0.85rem' }}>
            Payout ID: <code style={{ fontSize: '0.8rem' }}>{payout.id}</code>
          </p>

          {/* Validation result panel */}
          {validation && (
            <div id="payout-validation-result" style={{ marginTop: '20px', padding: '16px', borderRadius: '8px', border: `2px solid ${validation.valid ? '#4caf50' : '#f44336'}`, backgroundColor: validation.valid ? '#f1f8e9' : '#fff3e0' }}>
              <h4 style={{ margin: '0 0 10px', color: validation.valid ? '#2e7d32' : '#c62828' }}>
                Validation: {validation.valid ? '✓ Passed' : '✕ Failed'}
              </h4>

              {validation.violations && validation.violations.length > 0 && (
                <div style={{ marginBottom: '10px' }}>
                  <strong style={{ fontSize: '0.85rem', color: '#c62828' }}>Violations:</strong>
                  <ul style={{ margin: '4px 0', paddingLeft: '20px' }}>
                    {validation.violations.map((v, i) => (
                      <li key={i} style={{ fontSize: '0.85rem', color: '#c62828', marginBottom: '4px' }}>{v}</li>
                    ))}
                  </ul>
                </div>
              )}

              <div style={{ fontSize: '0.85rem', color: '#555' }}>
                <strong>Requires Human Approval:</strong>{' '}
                <span style={{ color: validation.requiresHumanApproval ? '#e65100' : '#2e7d32', fontWeight: 600 }}>
                  {validation.requiresHumanApproval ? 'Yes' : 'No'}
                </span>
              </div>

              {/* Gemini contextual explanation */}
              {validation.aiUsed && validation.reasoningSummary && (
                <div id="payout-gemini-explanation" style={{ marginTop: '12px', padding: '12px', backgroundColor: '#e8eaf6', borderRadius: '6px', borderLeft: '4px solid #5c6bc0' }}>
                  <div style={{ fontSize: '0.8rem', color: '#283593', fontWeight: 600, marginBottom: '4px' }}>
                    Gemini Contextual Explanation
                    {validation.aiModel && <span style={{ fontWeight: 400, marginLeft: '8px' }}>({validation.aiModel})</span>}
                  </div>
                  <div style={{ fontSize: '0.85rem', color: '#37474f', lineHeight: 1.5 }}>
                    {validation.reasoningSummary}
                  </div>
                </div>
              )}

              {/* Fallback notice */}
              {validation.fallbackUsed && (
                <div id="payout-fallback-notice" style={{ marginTop: '12px', padding: '10px', backgroundColor: '#fff8e1', borderRadius: '6px', borderLeft: '4px solid #f9a825', fontSize: '0.85rem', color: '#f57f17' }}>
                  ⚠ Rule-based validation used (AI contextual analysis unavailable).
                  Deterministic validation result is authoritative.
                </div>
              )}
            </div>
          )}

          {/* Navigation links to Approval Desk & Claim Details */}
          <div style={{ marginTop: '20px', display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
            <Link
              id="go-to-approval-btn"
              to={`/payouts/approval?payoutId=${payout.id}`}
              style={{
                display: 'inline-block',
                padding: '10px 20px',
                backgroundColor: '#2e7d32',
                color: '#fff',
                textDecoration: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '0.9rem',
              }}
            >
              Go to Approval Desk →
            </Link>
            {(payout.claimId || claimId) && (
              <Link
                to={`/claims/${payout.claimId || claimId}`}
                style={{
                  display: 'inline-block',
                  padding: '10px 20px',
                  backgroundColor: '#546e7a',
                  color: '#fff',
                  textDecoration: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                }}
              >
                ← Back to Claim Details
              </Link>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

export default PayoutCalculation
