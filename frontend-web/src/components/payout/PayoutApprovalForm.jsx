import React, { useState } from 'react'

/**
 * Approval action form for approve/reject/revision.
 * Only comments come from the client. Reviewer identity is derived server-side.
 */
function PayoutApprovalForm({ payoutId, onApprove, onReject, onRevision, loading }) {
  const [comments, setComments] = useState('')

  const btnBase = {
    padding: '10px 20px',
    border: 'none',
    borderRadius: '6px',
    cursor: 'pointer',
    fontWeight: 600,
    fontSize: '0.9rem',
    marginRight: '8px',
    opacity: loading ? 0.6 : 1,
  }

  return (
    <div style={{ background: '#fff', borderRadius: '8px', padding: '16px', border: '1px solid #e0e0e0' }}>
      <h4 style={{ margin: '0 0 12px', color: '#333' }}>Review Decision</h4>

      <textarea
        id="payout-approval-comments"
        placeholder="Add comments or reason for decision..."
        value={comments}
        onChange={(e) => setComments(e.target.value)}
        disabled={loading}
        style={{
          width: '100%',
          minHeight: '80px',
          padding: '10px',
          border: '1px solid #ddd',
          borderRadius: '6px',
          fontSize: '0.9rem',
          resize: 'vertical',
          boxSizing: 'border-box',
          marginBottom: '12px',
        }}
      />

      <div style={{ display: 'flex', gap: '8px' }}>
        <button
          id="payout-approve-btn"
          onClick={() => onApprove(payoutId, comments)}
          disabled={loading}
          style={{ ...btnBase, backgroundColor: '#2e7d32', color: '#fff' }}
        >
          ✓ Approve
        </button>

        <button
          id="payout-reject-btn"
          onClick={() => onReject(payoutId, comments)}
          disabled={loading}
          style={{ ...btnBase, backgroundColor: '#c62828', color: '#fff' }}
        >
          ✕ Reject
        </button>

        <button
          id="payout-revision-btn"
          onClick={() => onRevision(payoutId, comments)}
          disabled={loading}
          style={{ ...btnBase, backgroundColor: '#f57f17', color: '#fff' }}
        >
          ↻ Request Revision
        </button>
      </div>
    </div>
  )
}

export default PayoutApprovalForm
