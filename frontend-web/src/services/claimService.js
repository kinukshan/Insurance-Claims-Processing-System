/**
 * Claim Service — Component B (Member 2)
 * Staff-facing API operations for claims management.
 * All requests route through ASP.NET Core.
 */

import { apiFetch } from './api';

/**
 * GET /api/claims — Get all claims with optional filters.
 */
export async function getAllClaims({ status, search } = {}) {
  const params = new URLSearchParams();
  if (status) params.append('status', status);
  if (search) params.append('search', search);
  const query = params.toString();
  return apiFetch(`/claims${query ? `?${query}` : ''}`);
}

/**
 * GET /api/claims/{id} — Get a claim by ID.
 */
export async function getClaim(id) {
  return apiFetch(`/claims/${id}`);
}

/**
 * POST /api/claims — Create a new claim.
 */
export async function createClaim(data) {
  return apiFetch('/claims', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

/**
 * PUT /api/claims/{id} — Update a draft claim.
 */
export async function updateClaim(id, data) {
  return apiFetch(`/claims/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

/**
 * DELETE /api/claims/{id} — Hard delete a draft claim.
 */
export async function deleteClaim(id) {
  return apiFetch(`/claims/${id}`, { method: 'DELETE' });
}

/**
 * POST /api/claims/{id}/withdraw — Withdraw a submitted or under-review claim.
 */
export async function withdrawClaim(id) {
  return apiFetch(`/claims/${id}/withdraw`, { method: 'POST' });
}

/**
 * POST /api/claims/{id}/submit — Submit a draft claim.
 */
export async function submitClaim(id) {
  return apiFetch(`/claims/${id}/submit`, { method: 'POST' });
}

/**
 * DELETE /api/claims/{claimId}/documents/{documentId} — Delete a document from a claim.
 */
export async function deleteDocument(claimId, documentId) {
  return apiFetch(`/claims/${claimId}/documents/${documentId}`, { method: 'DELETE' });
}

/**
 * POST /api/claims/{id}/documents — Upload a document to a claim.
 */
export async function uploadDocument(claimId, file, documentType) {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('documentType', documentType);

  return apiFetch(`/claims/${claimId}/documents`, {
    method: 'POST',
    body: formData,
  });
}

/**
 * GET /api/claims/{id}/documents — Get documents for a claim.
 */
export async function getDocuments(claimId) {
  return apiFetch(`/claims/${claimId}/documents`);
}

/**
 * POST /api/claims/{id}/validate-coverage — Validate claim against policy coverage.
 */
export async function validateCoverage(claimId) {
  return apiFetch(`/claims/${claimId}/validate-coverage`, { method: 'POST' });
}

/**
 * POST /api/claims/{id}/start-workflow — Start the document verification workflow.
 */
export async function startWorkflow(claimId) {
  return apiFetch(`/claims/${claimId}/start-workflow`, { method: 'POST' });
}

/**
 * GET /api/claims/{id}/document-requirements — Get deterministic required documents and completion status.
 */
export async function getDocumentRequirements(claimId) {
  return apiFetch(`/claims/${claimId}/document-requirements`);
}
