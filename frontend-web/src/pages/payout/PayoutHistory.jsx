// Payout history — Component D (Kinukshan)
// Paginated, filterable, sortable payout history for staff and policyholders

import React, { useState, useEffect, useCallback } from 'react'
import PayoutStatusBadge from '../../components/payout/PayoutStatusBadge'
import { getPayoutHistory, getMyPayouts } from '../../services/payoutService'
import { useAuth } from '../../context/AuthContext'

function useOptionalAuth() {
  try {
    const auth = useAuth()
    return {
      role: auth.role || auth.user?.role || null,
      user: auth.user || null,
    }
  } catch {
    return { role: null, user: null }
  }
}

const STATUS_OPTIONS = [
  { value: '', label: 'All Statuses' },
  { value: '0', label: 'Draft' },
  { value: '1', label: 'Pending Approval' },
  { value: '2', label: 'Approved' },
  { value: '3', label: 'Rejected' },
  { value: '4', label: 'Revision Requested' },
  { value: '5', label: 'Processing' },
  { value: '6', label: 'Paid' },
  { value: '7', label: 'Failed' },
]

function PayoutHistory() {
  const { role } = useOptionalAuth()
  const isPolicyholder = role === 'Policyholder'

  const [data, setData] = useState({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState('')
  const [sortBy, setSortBy] = useState('CreatedAt')
  const [sortDesc, setSortDesc] = useState(true)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [errorType, setErrorType] = useState(null) // 'auth' | 'forbidden' | 'network' | 'error'

  const loadHistory = useCallback(async () => {
    setLoading(true)
    setError(null)
    setErrorType(null)
    try {
      const fetchFn = isPolicyholder ? getMyPayouts : getPayoutHistory
      const result = await fetchFn({
        page,
        pageSize: 20,
        status: statusFilter || undefined,
        sortBy,
        sortDescending: sortDesc,
      })
      setData(result)
    } catch (err) {
      if (err.status === 401) {
        setError('Your session has expired. Please log in again.')
        setErrorType('auth')
      } else if (err.status === 403) {
        setError(isPolicyholder ? 'You do not have permission to view these payouts.' : 'You do not have permission to view payout history.')
        setErrorType('forbidden')
      } else {
        setError(err.message || 'Failed to load payout history.')
        setErrorType('error')
      }
    } finally {
      setLoading(false)
    }
  }, [page, statusFilter, sortBy, sortDesc, isPolicyholder])

  useEffect(() => { loadHistory() }, [loadHistory])

  const fmt = (n) => `$${Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 })}`
  const fmtDate = (d) => new Date(d).toLocaleString()

  const containerStyle = { maxWidth: '900px', margin: '0 auto', padding: '24px' }
  const filterRow = { display: 'flex', gap: '12px', marginBottom: '20px', flexWrap: 'wrap', alignItems: 'center' }
  const selectStyle = { padding: '8px 12px', borderRadius: '6px', border: '1px solid #ddd', fontSize: '0.9rem' }
  const thStyle = { textAlign: 'left', padding: '10px 12px', borderBottom: '2px solid #e0e0e0', fontSize: '0.85rem', color: '#555' }
  const tdStyle = { padding: '10px 12px', borderBottom: '1px solid #f0f0f0', fontSize: '0.9rem' }
  const paginationStyle = { display: 'flex', justifyContent: 'center', gap: '8px', marginTop: '20px' }
  const pageBtnStyle = (active) => ({
    padding: '6px 14px',
    border: active ? '2px solid #1976d2' : '1px solid #ddd',
    borderRadius: '6px',
    cursor: 'pointer',
    backgroundColor: active ? '#e3f2fd' : '#fff',
    fontWeight: active ? 700 : 400,
  })

  const errorBgColor = errorType === 'auth' ? '#fff3e0' : errorType === 'forbidden' ? '#fce4ec' : '#ffebee'
  const errorTextColor = errorType === 'auth' ? '#e65100' : errorType === 'forbidden' ? '#ad1457' : '#c62828'

  return (
    <div style={containerStyle}>
      <h2>{isPolicyholder ? 'My Payouts' : 'Payout History'}</h2>
      {isPolicyholder && (
        <p style={{ color: '#666', marginTop: '-8px', marginBottom: '16px', fontSize: '0.95rem' }}>
          Track settlements and payment status for your filed claims.
        </p>
      )}

      <div style={filterRow}>
        <select id="payout-status-filter" value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1) }} style={selectStyle}>
          {STATUS_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>

        <select id="payout-sort-by" value={sortBy} onChange={(e) => setSortBy(e.target.value)} style={selectStyle}>
          <option value="CreatedAt">Date</option>
          <option value="FinalPayout">Amount</option>
          <option value="Status">Status</option>
        </select>

        <button onClick={() => setSortDesc(!sortDesc)} style={{ ...selectStyle, cursor: 'pointer' }}>
          {sortDesc ? '↓ Desc' : '↑ Asc'}
        </button>

        {!error && (
          <span style={{ fontSize: '0.85rem', color: '#888' }}>
            {data.totalCount} total records
          </span>
        )}
      </div>

      {error && (
        <div id="payout-history-error" style={{ padding: '12px', backgroundColor: errorBgColor, color: errorTextColor, borderRadius: '6px', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#888' }}>Loading...</div>
      ) : !error ? (
        <>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={thStyle}>Claim</th>
                <th style={thStyle}>Final Payout</th>
                <th style={thStyle}>Status</th>
                <th style={thStyle}>Created</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 ? (
                <tr><td colSpan={4} style={{ ...tdStyle, textAlign: 'center', color: '#888' }}>No payouts found.</td></tr>
              ) : (
                data.items.map((p) => (
                  <tr key={p.id}>
                    <td style={{ ...tdStyle, fontSize: '0.8rem', fontFamily: 'monospace' }}>
                      {p.claimNumber || p.claimId.substring(0, 8) + '...'}
                    </td>
                    <td style={tdStyle}>{fmt(p.finalPayout)}</td>
                    <td style={tdStyle}><PayoutStatusBadge status={p.statusDisplay} /></td>
                    <td style={{ ...tdStyle, fontSize: '0.85rem', color: '#666' }}>{fmtDate(p.createdAt)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>

          {data.totalPages > 1 && (
            <div style={paginationStyle}>
              <button disabled={page <= 1} onClick={() => setPage(page - 1)} style={pageBtnStyle(false)}>← Prev</button>
              {Array.from({ length: Math.min(data.totalPages, 5) }, (_, i) => i + 1).map((p) => (
                <button key={p} onClick={() => setPage(p)} style={pageBtnStyle(p === page)}>{p}</button>
              ))}
              <button disabled={!data.hasNextPage} onClick={() => setPage(page + 1)} style={pageBtnStyle(false)}>Next →</button>
            </div>
          )}
        </>
      ) : null}
    </div>
  )
}

export default PayoutHistory
