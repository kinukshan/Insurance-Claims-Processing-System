/**
 * Policy service — Component A (Member 1)
 * All requests go to ASP.NET Core — never directly to the database or AI service.
 */

import { apiFetch } from './api';

/**
 * Fetch all policy types (reference data, public).
 */
export async function getPolicyTypes() {
  return apiFetch('/policytypes');
}

/**
 * Fetch all policies.
 */
export async function getPolicies() {
  return apiFetch('/policies');
}

/**
 * Fetch a single policy by ID.
 */
export async function getPolicyById(id) {
  return apiFetch(`/policies/${id}`);
}

/**
 * Fetch policies for a specific policyholder.
 */
export async function getPoliciesByPolicyholder(policyholderId) {
  return apiFetch(`/policies/policyholder/${policyholderId}`);
}

/**
 * Create a new policy.
 */
export async function createPolicy(policyData) {
  return apiFetch('/policies', {
    method: 'POST',
    body: JSON.stringify(policyData),
  });
}

/**
 * Update an existing policy.
 */
export async function updatePolicy(id, policyData) {
  return apiFetch(`/policies/${id}`, {
    method: 'PUT',
    body: JSON.stringify(policyData),
  });
}

/**
 * Delete a draft policy.
 */
export async function deletePolicy(id) {
  await apiFetch(`/policies/${id}`, {
    method: 'DELETE',
  });
  return true;
}

/**
 * Calculate premium for a policy.
 */
export async function calculatePremium(id) {
  return apiFetch(`/policies/${id}/calculate-premium`, {
    method: 'POST',
  });
}

/**
 * Get coverage details for a policy.
 */
export async function getCoverage(id) {
  return apiFetch(`/policies/${id}/coverage`);
}

/**
 * Renew a policy.
 */
export async function renewPolicy(id) {
  return apiFetch(`/policies/${id}/renew`, {
    method: 'POST',
  });
}
