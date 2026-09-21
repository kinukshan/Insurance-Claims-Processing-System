/**
 * Base API client configuration.
 * All requests go to ASP.NET Core — never directly to the AI service.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

// Dev-mode user ID — used for testing without JWT auth
const DEV_USER_ID = import.meta.env.VITE_DEV_USER_ID || '00000000-0000-0000-0000-000000000001';

/**
 * Get the stored auth token from sessionStorage.
 */
function getAuthToken() {
  return sessionStorage.getItem('auth_token');
}

/**
 * Reusable fetch wrapper with JSON handling, auth, and error normalization.
 *
 * @param {string} endpoint - API path relative to base URL (e.g., '/claims')
 * @param {object} options - fetch options
 * @returns {Promise<any>} parsed JSON response
 */
export async function apiFetch(endpoint, options = {}) {
  const url = `${API_BASE_URL}${endpoint}`;

  const headers = {
    ...options.headers,
  };

  // Add JWT Bearer token if available
  const token = getAuthToken();
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  } else {
    // Dev fallback: use X-User-Id header when no JWT token
    headers['X-User-Id'] = DEV_USER_ID;
  }

  // Only set Content-Type for JSON bodies (not FormData)
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorBody;
    try {
      errorBody = await response.json();
    } catch {
      errorBody = { error: response.statusText };
    }
    const detailMessage = errorBody?.error && errorBody.error !== 'An unexpected error occurred.'
      ? errorBody.error
      : (errorBody?.message || errorBody?.error || errorBody?.errors?.join('; ') || `API Error: ${response.status}`);
    const error = new Error(detailMessage);
    error.status = response.status;
    error.body = errorBody;
    throw error;
  }

  // 204 No Content
  if (response.status === 204) return null;

  return response.json();
}

export { API_BASE_URL, DEV_USER_ID };
