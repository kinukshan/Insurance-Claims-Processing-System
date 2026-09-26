import React from 'react'

/**
 * Reusable calculation breakdown component.
 * Shows each step of the deterministic payout formula.
 */
function PayoutBreakdown({ payout }) {
  if (!payout) return null

  const eligible = Math.min(payout.approvedClaimAmount, payout.coverageLimit)

  const rowStyle = {
    display: 'flex',
    justifyContent: 'space-between',
    padding: '8px 0',
    borderBottom: '1px solid #f0f0f0',
  }

  const labelStyle = { color: '#555', fontSize: '0.9rem' }
  const valueStyle = { fontWeight: 600, fontFamily: 'monospace', fontSize: '0.95rem' }
  const highlightRow = { ...rowStyle, borderBottom: '2px solid #1976d2', paddingTop: '12px' }
  const highlightValue = { ...valueStyle, color: '#1976d2', fontSize: '1.1rem' }

  const fmt = (n) => `$${Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 })}`

  return (
    <div style={{ background: '#fafafa', borderRadius: '8px', padding: '16px', marginBottom: '16px' }}>
      <h4 style={{ margin: '0 0 12px', color: '#333' }}>Calculation Breakdown</h4>

      <div style={rowStyle}>
        <span style={labelStyle}>Approved Claim Amount</span>
        <span style={valueStyle}>{fmt(payout.approvedClaimAmount)}</span>
      </div>

      <div style={rowStyle}>
        <span style={labelStyle}>Coverage Limit</span>
        <span style={valueStyle}>{fmt(payout.coverageLimit)}</span>
      </div>

      <div style={rowStyle}>
        <span style={labelStyle}>Eligible Amount <small>(min of claim, coverage)</small></span>
        <span style={valueStyle}>{fmt(eligible)}</span>
      </div>

      <div style={rowStyle}>
        <span style={labelStyle}>Deductible</span>
        <span style={{ ...valueStyle, color: '#c62828' }}>− {fmt(payout.deductible)}</span>
      </div>

      <div style={highlightRow}>
        <span style={{ ...labelStyle, fontWeight: 600, color: '#1976d2' }}>Final Payout</span>
        <span style={highlightValue}>{fmt(payout.finalPayout)}</span>
      </div>

      {((payout.finalPayout <= 0 || payout.proposedPayout <= 0) && payout.deductible > 0 && eligible <= payout.deductible) && (
        <div
          id="payout-zero-deductible-notice"
          style={{
            marginTop: '14px',
            padding: '12px 14px',
            backgroundColor: '#eff6ff',
            border: '1px solid #bfdbfe',
            borderRadius: '6px',
            color: '#1e40af',
            fontSize: '0.9rem',
            lineHeight: 1.4,
          }}
        >
          Your eligible claim amount does not exceed your policy deductible. No insurance payout is payable for this claim.
        </div>
      )}
    </div>
  )
}

export default PayoutBreakdown
