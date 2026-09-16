/**
 * Base API client configuration.
 * All requests go to ASP.NET Core — never directly to the AI service.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

// Dev-mode user ID — used for testing without JWT auth
const DEV_USER_ID = import.meta.env.VITE_DEV_USER_ID || '00000000-0000-0000-0000-000000000001';

/**
 * Reusable fetch wrapper with JSON handling and error normalization.
 *
 * @param {string} endpoint - API path relative to base URL (e.g., '/claims')
 * @param {object} options - fetch options
 * @returns {Promise<any>} parsed JSON response
 */
export async function apiFetch(endpoint, options = {}) {
  const url = `${API_BASE_URL}${endpoint}`;

  const headers = {
    'X-User-Id': DEV_USER_ID,
    ...options.headers,
  };

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
    const error = new Error(
      errorBody?.error || errorBody?.errors?.join('; ') || `API Error: ${response.status}`
    );
    error.status = response.status;
    error.body = errorBody;
    throw error;
  }

  // 204 No Content
  if (response.status === 204) return null;

  return response.json();
}

export { API_BASE_URL, DEV_USER_ID };
