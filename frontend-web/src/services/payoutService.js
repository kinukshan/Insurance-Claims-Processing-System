/**
 * Payout API service — Component D (Kinukshan).
 *
 * All requests go through ASP.NET Core — never directly to the AI service.
 * Financial inputs (CoverageLimit, Deductible) come from the backend,
 * not from this client.
 */

import { API_BASE_URL } from './api';

const PAYOUTS_URL = `${API_BASE_URL}/payouts`;

/**
 * Helper for making API requests with JSON handling.
 */
async function apiRequest(url, options = {}) {
  const response = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
      // TODO: Add JWT Authorization header once auth is wired
    },
    ...options,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(error.error || `API Error: ${response.status}`);
  }

  if (response.status === 204) return null;
  return response.json();
}

/**
 * Calculate and create a payout proposal for a claim.
 * No client-supplied financial inputs — all from backend.
 */
export async function calculatePayout(claimId) {
  return apiRequest(`${PAYOUTS_URL}/calculate/${claimId}`, { method: 'POST' });
}

/**
 * Get a payout by its ID.
 */
export async function getPayoutById(id) {
  return apiRequest(`${PAYOUTS_URL}/${id}`);
}

/**
 * Get payout for a specific claim.
 */
export async function getPayoutByClaim(claimId) {
  return apiRequest(`${PAYOUTS_URL}/claim/${claimId}`);
}

/**
 * Get paginated payout history with filtering and sorting.
 */
export async function getPayoutHistory({ page = 1, pageSize = 20, status, sortBy, sortDescending } = {}) {
  const params = new URLSearchParams({ page, pageSize });
  if (status !== undefined && status !== null && status !== '') params.set('status', status);
  if (sortBy) params.set('sortBy', sortBy);
  if (sortDescending !== undefined) params.set('sortDescending', sortDescending);

  return apiRequest(`${PAYOUTS_URL}/history?${params}`);
}

/**
 * Approve a payout. Reviewer identity from server auth context.
 */
export async function approvePayout(id, comments = '') {
  return apiRequest(`${PAYOUTS_URL}/${id}/approve`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Reject a payout. Reviewer identity from server auth context.
 */
export async function rejectPayout(id, comments = '') {
  return apiRequest(`${PAYOUTS_URL}/${id}/reject`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Request revision on a payout. Reviewer identity from server auth context.
 */
export async function requestRevision(id, comments = '') {
  return apiRequest(`${PAYOUTS_URL}/${id}/request-revision`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Execute an approved payout.
 */
export async function executePayout(id) {
  return apiRequest(`${PAYOUTS_URL}/${id}/execute`, { method: 'POST' });
}

/**
 * Update a draft payout (recalculate from current backend data).
 */
export async function updatePayout(id) {
  return apiRequest(`${PAYOUTS_URL}/${id}`, { method: 'PUT' });
}

/**
 * Delete a draft/invalid payout.
 */
export async function deletePayout(id) {
  return apiRequest(`${PAYOUTS_URL}/${id}`, { method: 'DELETE' });
}
