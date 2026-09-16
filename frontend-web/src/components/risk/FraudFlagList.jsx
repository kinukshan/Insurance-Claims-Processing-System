// Fraud flag list — reusable component
// Displays a list of fraud flags with severity indicators

import React from 'react'

const SEVERITY_ICONS = {
  Low: 'ℹ️',
  Medium: '⚠️',
  High: '🔶',
  Critical: '🔴',
}

/**
 * @param {{ flags: Array<{ id: string, flagTypeDisplay: string, description: string, severityDisplay: string, sourceDisplay: string, isResolved: boolean }> }} props
 */
function FraudFlagList({ flags }) {
  if (!flags || flags.length === 0) {
    return (
      <div className="state-message">
        <p>No fraud flags recorded.</p>
      </div>
    )
  }

  return (
    <ul className="fraud-flag-list">
      {flags.map((flag) => (
        <li key={flag.id} className="fraud-flag-item">
          <div className={`flag-icon ${(flag.severityDisplay || 'medium').toLowerCase()}`}>
            {SEVERITY_ICONS[flag.severityDisplay] || '⚠️'}
          </div>
          <div className="flag-content">
            <div className="flag-type">
              {flag.flagTypeDisplay}
              <span className={`severity-badge ${(flag.severityDisplay || 'medium').toLowerCase()}`} style={{ marginLeft: '0.5rem' }}>
                {flag.severityDisplay}
              </span>
              {flag.isResolved && (
                <span className="status-badge resolved" style={{ marginLeft: '0.5rem' }}>Resolved</span>
              )}
            </div>
            <div className="flag-desc">{flag.description}</div>
            <div className="flag-desc" style={{ fontStyle: 'italic' }}>
              Source: {flag.sourceDisplay}
            </div>
          </div>
        </li>
      ))}
    </ul>
  )
}

export default FraudFlagList
