import React from 'react'

const STATUS_COLORS = {
  Draft: { bg: '#e8eaf6', text: '#283593', label: 'Draft' },
  PendingApproval: { bg: '#fff3e0', text: '#e65100', label: 'Pending Approval' },
  Approved: { bg: '#e8f5e9', text: '#2e7d32', label: 'Approved' },
  Rejected: { bg: '#ffebee', text: '#c62828', label: 'Rejected' },
  RevisionRequested: { bg: '#fff8e1', text: '#f57f17', label: 'Revision Requested' },
  Processing: { bg: '#e3f2fd', text: '#1565c0', label: 'Processing' },
  Paid: { bg: '#e0f2f1', text: '#00695c', label: 'Paid' },
  Failed: { bg: '#fce4ec', text: '#ad1457', label: 'Failed' },
  Unclaimed: { bg: '#fff3e0', text: '#d97706', label: 'Awaiting Recipient' },
  AwaitingRecipient: { bg: '#fff3e0', text: '#d97706', label: 'Awaiting Recipient' },
  OnHold: { bg: '#fff8e1', text: '#b45309', label: 'Payment On Hold' },
  PaymentOnHold: { bg: '#fff8e1', text: '#b45309', label: 'Payment On Hold' },
  Blocked: { bg: '#fee2e2', text: '#b91c1c', label: 'Payment Blocked' },
  Returned: { bg: '#f3e8ff', text: '#6b21a8', label: 'Returned' },
  Refunded: { bg: '#f1f5f9', text: '#475569', label: 'Refunded' },
  Reversed: { bg: '#f1f5f9', text: '#475569', label: 'Reversed' },
  Cancelled: { bg: '#f5f5f5', text: '#757575', label: 'Cancelled' },
}

/**
 * Color-coded payout status badge.
 */
function PayoutStatusBadge({ status }) {
  const normalizedKey = status ? String(status).replace(/\s+/g, '') : 'Draft'
  const config = STATUS_COLORS[normalizedKey] || STATUS_COLORS[status] || STATUS_COLORS.Draft

  const style = {
    display: 'inline-block',
    padding: '4px 12px',
    borderRadius: '12px',
    fontSize: '0.8rem',
    fontWeight: 600,
    backgroundColor: config.bg,
    color: config.text,
  }

  return <span style={style}>{config.label}</span>
}

export default PayoutStatusBadge
