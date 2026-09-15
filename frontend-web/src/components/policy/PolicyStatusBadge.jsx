import React from 'react'

const STATUS_STYLES = {
  Draft: { backgroundColor: '#6b7280', color: '#fff' },
  Active: { backgroundColor: '#10b981', color: '#fff' },
  Expired: { backgroundColor: '#ef4444', color: '#fff' },
  Lapsed: { backgroundColor: '#f59e0b', color: '#fff' },
  Cancelled: { backgroundColor: '#6b7280', color: '#fff' },
}

/**
 * Renders a color-coded status badge.
 */
function PolicyStatusBadge({ status }) {
  const style = STATUS_STYLES[status] || STATUS_STYLES.Draft

  return (
    <span
      style={{
        display: 'inline-block',
        padding: '2px 10px',
        borderRadius: '12px',
        fontSize: '0.8rem',
        fontWeight: 600,
        letterSpacing: '0.02em',
        ...style,
      }}
    >
      {status}
    </span>
  )
}

export default PolicyStatusBadge
