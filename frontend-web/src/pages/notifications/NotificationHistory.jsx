import React, { useState, useEffect, useCallback } from 'react'
import { getNotifications } from '../../services/notificationService'
import { useAuth } from '../../context/AuthContext'

/**
 * NotificationHistory — Displays a paginated, sortable list of notification logs.
 * Staff users see all notifications; policyholders see only their own.
 */
function NotificationHistory() {
  const { user, role } = useAuth()
  const [notifications, setNotifications] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [page, setPage] = useState(1)
  const pageSize = 25

  const isStaff = role === 'ClaimsAdjuster' || role === 'Underwriter' || role === 'Admin'

  const fetchNotifications = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const params = { page, pageSize }
      // Policyholders only see their own notifications
      if (!isStaff && user?.id) {
        params.userId = user.id
      }
      const data = await getNotifications(params)
      setNotifications(data || [])
    } catch (err) {
      setError('Unable to load notifications. Please try again.')
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, isStaff, user?.id])

  useEffect(() => {
    fetchNotifications()
  }, [fetchNotifications])

  const getStatusIcon = (success, status) => {
    if (status === 'Accepted') return '📨'  // Provider accepted, delivery not confirmed
    if (success) return '✅'
    return '❌'
  }

  const getTypeLabel = (type) => {
    const labels = {
      ClaimSubmitted: 'Claim Submitted',
      ClaimApproved: 'Claim Approved',
      ClaimRejected: 'Claim Rejected',
      ClaimWithdrawn: 'Claim Withdrawn',
      DocumentVerificationComplete: 'Doc Verification',
      DocumentsVerified: 'Docs Verified',
      DocumentsNeedReview: 'Docs Need Review',
      RiskAssessmentComplete: 'Risk Assessment',
      RiskAssessmentNeedsReview: 'Risk Review',
      PayoutPendingApproval: 'Payout Pending Approval',
      PayoutApproved: 'Payout Approved',
      PayoutRejected: 'Payout Rejected',
      PayoutCompleted: 'Payout Completed',
      PayoutFailed: 'Payout Failed',
      AdditionalDocumentsRequired: 'Docs Required',
    }
    return labels[type] || type
  }

  const getTypeBadgeClass = (type) => {
    if (type.includes('Failed') || type.includes('Rejected')) return 'notification-badge danger'
    if (type.includes('Review') || type.includes('Withdrawn') || type.includes('Pending') || type.includes('Required')) return 'notification-badge warning'
    if (type.includes('Approved') || type.includes('Completed') || type.includes('Submitted') || type.includes('Verified')) {
      return 'notification-badge success'
    }
    return 'notification-badge info'
  }

  const formatDate = (dateStr) => {
    if (!dateStr) return '—'
    const d = new Date(dateStr)
    return d.toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  }

  return (
    <div className="page-container" id="notification-history-page">
      <div className="page-header">
        <h2>Notification History</h2>
        <p className="page-subtitle">
          {isStaff ? 'System-wide notification audit log' : 'Your notification history'}
        </p>
      </div>

      {error ? (
        <div className="error-state" role="alert" id="notifications-error-state">
          <p>⚠️ {error}</p>
          <button
            className="btn btn--secondary btn--sm"
            onClick={fetchNotifications}
            id="retry-notifications-btn"
            style={{ marginTop: '0.75rem' }}
          >
            Retry
          </button>
        </div>
      ) : loading ? (
        <div className="loading-container">
          <div className="loading-spinner" />
          <p>Loading notifications…</p>
        </div>
      ) : notifications.length === 0 ? (
        <div className="empty-state" id="notifications-empty-state">
          <div className="empty-icon">🔔</div>
          <p>No notifications found.</p>
        </div>
      ) : (
        <>
          <div className="table-container">
            <table className="data-table" id="notifications-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Type</th>
                  <th>Recipient</th>
                  <th>Subject</th>
                  <th>Provider</th>
                  <th>Sent At</th>
                  {!notifications.every(n => n.success) && <th>Error</th>}
                </tr>
              </thead>
              <tbody>
                {notifications.map((n) => (
                  <tr key={n.id} id={`notification-row-${n.id}`}>
                    <td title={n.status || (n.success ? 'Sent' : 'Failed')}>{getStatusIcon(n.success, n.status)}</td>
                    <td>
                      <span className={getTypeBadgeClass(n.notificationType)}>
                        {getTypeLabel(n.notificationType)}
                      </span>
                    </td>
                    <td className="text-mono">{n.recipient}</td>
                    <td>{n.subject}</td>
                    <td>
                      <span className="provider-badge">{n.provider}</span>
                    </td>
                    <td>{formatDate(n.sentAt)}</td>
                    {!notifications.every(nn => nn.success) && (
                      <td className="text-error">{n.errorMessage || '—'}</td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pagination-controls">
            <button
              className="btn btn-secondary"
              disabled={page <= 1}
              onClick={() => setPage(p => Math.max(1, p - 1))}
              id="notifications-prev-page"
            >
              ← Previous
            </button>
            <span className="page-indicator">Page {page}</span>
            <button
              className="btn btn-secondary"
              disabled={notifications.length < pageSize}
              onClick={() => setPage(p => p + 1)}
              id="notifications-next-page"
            >
              Next →
            </button>
          </div>
        </>
      )}
    </div>
  )
}

export default NotificationHistory
