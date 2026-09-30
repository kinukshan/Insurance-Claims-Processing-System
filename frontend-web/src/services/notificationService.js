/**
 * Notification Service — Frontend API client for notification history.
 *
 * All requests go to ASP.NET Core — never directly to an email provider.
 */
import { apiFetch } from './api';

/**
 * Get notification history with optional filtering.
 *
 * @param {object} params - Query parameters
 * @param {string} [params.userId] - Filter by user ID
 * @param {string} [params.claimId] - Filter by claim ID
 * @param {string} [params.payoutId] - Filter by payout ID
 * @param {number} [params.page] - Page number (default: 1)
 * @param {number} [params.pageSize] - Page size (default: 50)
 * @returns {Promise<Array>} List of notification log DTOs
 */
export async function getNotifications({ userId, claimId, payoutId, page = 1, pageSize = 50 } = {}) {
  const params = new URLSearchParams();
  if (userId) params.set('userId', userId);
  if (claimId) params.set('claimId', claimId);
  if (payoutId) params.set('payoutId', payoutId);
  if (page) params.set('page', String(page));
  if (pageSize) params.set('pageSize', String(pageSize));

  const query = params.toString();
  return apiFetch(`/notifications${query ? `?${query}` : ''}`);
}

/**
 * Get notification history for a specific claim.
 *
 * @param {string} claimId - The claim ID to query
 * @returns {Promise<Array>} List of notification log DTOs for the claim
 */
export async function getNotificationsByClaimId(claimId) {
  return apiFetch(`/notifications/claim/${claimId}`);
}

/**
 * Get notification history for a specific payout.
 *
 * @param {string} payoutId - The payout ID to query
 * @returns {Promise<Array>} List of notification log DTOs for the payout
 */
export async function getNotificationsByPayoutId(payoutId) {
  return apiFetch(`/notifications/payout/${payoutId}`);
}
