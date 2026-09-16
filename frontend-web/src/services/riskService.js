// Risk assessment service — Component C (Member 3)
// All requests go to ASP.NET Core — never directly to the AI service.

import { API_BASE_URL } from './api';

/**
 * Trigger a risk assessment on a claim.
 * @param {string} claimId
 * @param {object} options - { includeAiAnalysis: boolean, notes?: string }
 */
export async function assessClaim(claimId, options = { includeAiAnalysis: true }) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/${claimId}/assess`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(options),
  });
  if (!res.ok) throw new Error(`Assessment failed: ${res.status}`);
  return res.json();
}

/**
 * Get the latest risk assessment for a claim.
 * @param {string} claimId
 */
export async function getAssessment(claimId) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/${claimId}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Failed to fetch assessment: ${res.status}`);
  return res.json();
}

/**
 * Get all claims with unresolved fraud flags.
 */
export async function getFlaggedClaims() {
  const res = await fetch(`${API_BASE_URL}/riskassessments/flagged`);
  if (!res.ok) throw new Error(`Failed to fetch flagged claims: ${res.status}`);
  return res.json();
}

/**
 * Get fraud case history for a policyholder.
 * @param {string} policyholderId
 */
export async function getFraudHistory(policyholderId) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/history/${policyholderId}`);
  if (!res.ok) throw new Error(`Failed to fetch fraud history: ${res.status}`);
  return res.json();
}

/**
 * Escalate a risk assessment to a fraud case.
 * @param {string} assessmentId
 * @param {object} data - { reason: string, priority: string, assignedReviewer?: string }
 */
export async function escalateClaim(assessmentId, data) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/${assessmentId}/escalate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`Escalation failed: ${res.status}`);
  return res.json();
}

/**
 * Get all fraud flags for a specific claim.
 * @param {string} claimId
 */
export async function getFlags(claimId) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/${claimId}/flags`);
  if (!res.ok) throw new Error(`Failed to fetch flags: ${res.status}`);
  return res.json();
}

/**
 * Update a fraud case (status, notes, resolution, assignment).
 * @param {string} fraudCaseId
 * @param {object} data
 */
export async function updateFraudCase(fraudCaseId, data) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/fraud-cases/${fraudCaseId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`Failed to update fraud case: ${res.status}`);
  return res.json();
}

/**
 * Get policyholder-safe review status for a claim.
 * @param {string} claimId
 */
export async function getPolicyholderStatus(claimId) {
  const res = await fetch(`${API_BASE_URL}/riskassessments/${claimId}/status`);
  if (!res.ok) throw new Error(`Failed to fetch status: ${res.status}`);
  return res.json();
}
