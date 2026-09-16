// Escalation modal — reusable component
// Modal form for escalating a risk assessment to a fraud case

import React, { useState } from 'react'

/**
 * @param {{ isOpen: boolean, onClose: () => void, onSubmit: (data: object) => void, assessmentId: string }} props
 */
function EscalationModal({ isOpen, onClose, onSubmit, assessmentId }) {
  const [reason, setReason] = useState('')
  const [priority, setPriority] = useState('Medium')
  const [assignedReviewer, setAssignedReviewer] = useState('')
  const [submitting, setSubmitting] = useState(false)

  if (!isOpen) return null

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!reason.trim()) return

    setSubmitting(true)
    try {
      await onSubmit({
        reason: reason.trim(),
        priority,
        assignedReviewer: assignedReviewer.trim() || null,
      })
      setReason('')
      setPriority('Medium')
      setAssignedReviewer('')
      onClose()
    } catch {
      // Error handling done by parent
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h3>Escalate to Fraud Case</h3>
        <form onSubmit={handleSubmit}>
          <div className="modal-field">
            <label htmlFor="escalation-reason">Reason for Escalation *</label>
            <textarea
              id="escalation-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Describe why this claim should be escalated..."
              required
            />
          </div>

          <div className="modal-field">
            <label htmlFor="escalation-priority">Priority</label>
            <select
              id="escalation-priority"
              value={priority}
              onChange={(e) => setPriority(e.target.value)}
            >
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
              <option value="Urgent">Urgent</option>
            </select>
          </div>

          <div className="modal-field">
            <label htmlFor="escalation-reviewer">Assign Reviewer</label>
            <input
              id="escalation-reviewer"
              type="text"
              value={assignedReviewer}
              onChange={(e) => setAssignedReviewer(e.target.value)}
              placeholder="Reviewer name (optional)"
            />
          </div>

          <div className="modal-actions">
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={submitting}>
              Cancel
            </button>
            <button type="submit" className="btn btn-danger" disabled={submitting || !reason.trim()}>
              {submitting ? 'Escalating...' : 'Escalate'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default EscalationModal
