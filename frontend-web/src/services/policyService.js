/**
 * Policy service — Component A (Member 1)
 * All requests go to ASP.NET Core — never directly to the database or AI service.
 */

import { API_BASE_URL } from './api';

const POLICIES_URL = `${API_BASE_URL}/policies`;

/**
 * Fetch all policies.
 */
export async function getPolicies() {
  const response = await fetch(POLICIES_URL);
  if (!response.ok) throw new Error(`Failed to fetch policies: ${response.statusText}`);
  return response.json();
}

/**
 * Fetch a single policy by ID.
 */
export async function getPolicyById(id) {
  const response = await fetch(`${POLICIES_URL}/${id}`);
  if (!response.ok) throw new Error(`Failed to fetch policy: ${response.statusText}`);
  return response.json();
}

/**
 * Fetch policies for a specific policyholder.
 */
export async function getPoliciesByPolicyholder(policyholderId) {
  const response = await fetch(`${POLICIES_URL}/policyholder/${policyholderId}`);
  if (!response.ok) throw new Error(`Failed to fetch policyholder policies: ${response.statusText}`);
  return response.json();
}

/**
 * Create a new policy.
 */
export async function createPolicy(policyData) {
  const response = await fetch(POLICIES_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(policyData),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || `Failed to create policy: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Update an existing policy.
 */
export async function updatePolicy(id, policyData) {
  const response = await fetch(`${POLICIES_URL}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(policyData),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || `Failed to update policy: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Delete a draft policy.
 */
export async function deletePolicy(id) {
  const response = await fetch(`${POLICIES_URL}/${id}`, {
    method: 'DELETE',
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || `Failed to delete policy: ${response.statusText}`);
  }
  return true;
}

/**
 * Calculate premium for a policy.
 */
export async function calculatePremium(id) {
  const response = await fetch(`${POLICIES_URL}/${id}/calculate-premium`, {
    method: 'POST',
  });
  if (!response.ok) throw new Error(`Failed to calculate premium: ${response.statusText}`);
  return response.json();
}

/**
 * Get coverage details for a policy.
 */
export async function getCoverage(id) {
  const response = await fetch(`${POLICIES_URL}/${id}/coverage`);
  if (!response.ok) throw new Error(`Failed to fetch coverage: ${response.statusText}`);
  return response.json();
}

/**
 * Renew a policy.
 */
export async function renewPolicy(id) {
  const response = await fetch(`${POLICIES_URL}/${id}/renew`, {
    method: 'POST',
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || `Failed to renew policy: ${response.statusText}`);
  }
  return response.json();
}
