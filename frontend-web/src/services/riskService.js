// Risk assessment service — Component C (Member 3)
// All requests go to ASP.NET Core — never directly to the AI service.

import { apiFetch } from './api';

/**
 * Trigger a risk assessment on a claim.
 * @param {string} claimId
 * @param {object} options - { includeAiAnalysis: boolean, notes?: string }
 */
export async function assessClaim(claimId, options = { includeAiAnalysis: true }) {
  return apiFetch(`/riskassessments/${claimId}/assess`, {
    method: 'POST',
    body: JSON.stringify(options),
  });
}

/**
 * Get the latest risk assessment for a claim.
 * @param {string} claimId
 */
export async function getAssessment(claimId) {
  try {
    return await apiFetch(`/riskassessments/${claimId}`);
  } catch (err) {
    if (err.status === 404) return null;
    throw err;
  }
}

/**
 * Get all risk assessments across all claims.
 * Authoritative source for dashboard summary metrics and complete assessment list.
 */
export async function getAllAssessments() {
  return apiFetch('/riskassessments');
}

/**
 * Get all claims with unresolved fraud flags.
 */
export async function getFlaggedClaims() {
  return apiFetch('/riskassessments/flagged');
}

/**
 * Get fraud case history for a policyholder.
 * @param {string} policyholderId
 */
export async function getFraudHistory(policyholderId) {
  return apiFetch(`/riskassessments/history/${policyholderId}`);
}

/**
 * Escalate a risk assessment to a fraud case.
 * @param {string} assessmentId
 * @param {object} data - { reason: string, priority: string, assignedReviewer?: string }
 */
export async function escalateClaim(assessmentId, data) {
  return apiFetch(`/riskassessments/${assessmentId}/escalate`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

/**
 * Get all fraud flags for a specific claim.
 * @param {string} claimId
 */
export async function getFlags(claimId) {
  return apiFetch(`/riskassessments/${claimId}/flags`);
}

/**
 * Update a fraud case (status, notes, resolution, assignment).
 * @param {string} fraudCaseId
 * @param {object} data
 */
export async function updateFraudCase(fraudCaseId, data) {
  return apiFetch(`/riskassessments/fraud-cases/${fraudCaseId}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

/**
 * Get policyholder-safe review status for a claim.
 * @param {string} claimId
 */
export async function getPolicyholderStatus(claimId) {
  return apiFetch(`/riskassessments/${claimId}/status`);
}
