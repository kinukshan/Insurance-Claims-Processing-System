/**
 * Risk Assessment Detail — Route wrapper.
 *
 * Reads claimId from the URL and passes it to the existing
 * prop-driven RiskAssessmentDetail component.
 */

import React from 'react'
import { useParams } from 'react-router-dom'
import RiskAssessmentDetail from './RiskAssessmentDetail'

function RiskAssessmentDetailRoute() {
  const { claimId } = useParams()
  return <RiskAssessmentDetail claimId={claimId} />
}

export default RiskAssessmentDetailRoute
