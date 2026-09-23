/**
 * Payout API service — Component D (Kinukshan).
 *
 * All requests go through ASP.NET Core — never directly to the AI service.
 * Financial inputs (CoverageLimit, Deductible) come from the backend,
 * not from this client.
 */

import { apiFetch } from './api';

/**
 * Calculate and create a payout proposal for a claim.
 * No client-supplied financial inputs — all from backend.
 */
export async function calculatePayout(claimId) {
  return apiFetch(`/payouts/calculate/${claimId}`, { method: 'POST' });
}

/**
 * Get a payout by its ID.
 */
export async function getPayoutById(id) {
  return apiFetch(`/payouts/${id}`);
}

/**
 * Get payout for a specific claim.
 */
export async function getPayoutByClaim(claimId) {
  return apiFetch(`/payouts/claim/${claimId}`);
}

/**
 * Get paginated payout history with filtering and sorting.
 */
export async function getPayoutHistory({ page = 1, pageSize = 20, status, sortBy, sortDescending } = {}) {
  const params = new URLSearchParams({ page, pageSize });
  if (status !== undefined && status !== null && status !== '') params.set('status', status);
  if (sortBy) params.set('sortBy', sortBy);
  if (sortDescending !== undefined) params.set('sortDescending', sortDescending);

  return apiFetch(`/payouts/history?${params}`);
}

/**
 * Approve a payout. Reviewer identity from server auth context.
 */
export async function approvePayout(id, comments = '') {
  return apiFetch(`/payouts/${id}/approve`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Reject a payout. Reviewer identity from server auth context.
 */
export async function rejectPayout(id, comments = '') {
  return apiFetch(`/payouts/${id}/reject`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Request revision on a payout. Reviewer identity from server auth context.
 */
export async function requestRevision(id, comments = '') {
  return apiFetch(`/payouts/${id}/request-revision`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

/**
 * Execute an approved payout.
 */
export async function executePayout(id) {
  return apiFetch(`/payouts/${id}/execute`, { method: 'POST' });
}

/**
 * Update a draft payout (recalculate from current backend data).
 */
export async function updatePayout(id) {
  return apiFetch(`/payouts/${id}`, { method: 'PUT' });
}

/**
 * Delete a draft/invalid payout.
 */
export async function deletePayout(id) {
  return apiFetch(`/payouts/${id}`, { method: 'DELETE' });
}
