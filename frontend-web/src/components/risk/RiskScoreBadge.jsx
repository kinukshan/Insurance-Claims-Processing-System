// Risk score badge — reusable component
// Displays a color-coded risk score indicator

import React from 'react'

const LEVELS = {
  Low: 'low',
  Medium: 'medium',
  High: 'high',
  Critical: 'critical',
}

function classifyScore(score) {
  if (score < 30) return 'Low'
  if (score < 60) return 'Medium'
  if (score < 80) return 'High'
  return 'Critical'
}

/**
 * @param {{ score: number, level?: string }} props
 */
function RiskScoreBadge({ score, level }) {
  const displayLevel = level || classifyScore(score)
  const cssClass = LEVELS[displayLevel] || 'medium'

  return (
    <span className={`risk-score-badge ${cssClass}`} title={`Risk Score: ${score}`}>
      <span>{Math.round(score)}</span>
      <span>{displayLevel}</span>
    </span>
  )
}

export default RiskScoreBadge
