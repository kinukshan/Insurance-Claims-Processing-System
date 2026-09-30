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
/**
 * Safely extracts and normalizes a user-facing error message from an error response body.
 *
 * Handles:
 * - errors as an array: { errors: ["msg1", "msg2"] }
 * - errors as an ASP.NET validation dictionary: { errors: { field: ["msg1"] } }
 * - error as a string: { error: "msg" }
 * - detail/title from ProblemDetails (RFC 7807): { title: "...", detail: "..." }
 * - message as a string: { message: "msg" }
 * - empty or unexpected responses: fallback to status code / status text
 * - sanitization: blocks raw SQL errors, stack traces, and sensitive internal exceptions
 *
 * @param {any} errorBody - Parsed response body or fallback object
 * @param {Response} response - Fetch response object
 * @returns {string} Sanitized, user-facing error string
 */
export function extractErrorMessage(errorBody, response) {
  const isSensitive = (str) => {
    if (typeof str !== 'string') return false;
    const lower = str.toLowerCase();
    return [
      'exception',
      'stacktrace',
      'stack trace',
      'npgsql',
      'postgres',
      'sql',
      'syntax error',
      'at system.',
      'at microsoft.',
      'at insuranceclaims.',
      'relation "',
      'column "',
      'table "',
      'password',
      'secret',
      'connectionstring',
      'connection string',
    ].some((pattern) => lower.includes(pattern));
  };

  const sanitize = (str) => {
    if (!str || typeof str !== 'string') return null;
    const trimmed = str.trim();
    if (!trimmed) return null;
    if (isSensitive(trimmed)) {
      return 'An unexpected server error occurred. Please try again.';
    }
    return trimmed;
  };

  if (!errorBody || typeof errorBody !== 'object') {
    return response?.statusText || (response?.status ? `API Error: ${response.status}` : 'An unexpected error occurred.');
  }

  // 1. Handle errorBody.errors (array or ASP.NET dictionary object)
  if (errorBody.errors) {
    if (Array.isArray(errorBody.errors)) {
      const messages = errorBody.errors
        .map((e) => (typeof e === 'string' ? e : e?.message || e?.errorMessage || ''))
        .map(sanitize)
        .filter(Boolean);
      if (messages.length > 0) {
        return messages.join('; ');
      }
    } else if (typeof errorBody.errors === 'object' && errorBody.errors !== null) {
      const messages = Object.values(errorBody.errors)
        .flatMap((val) => (Array.isArray(val) ? val : [val]))
        .map((e) => (typeof e === 'string' ? e : e?.message || e?.errorMessage || ''))
        .map(sanitize)
        .filter(Boolean);
      if (messages.length > 0) {
        return messages.join('; ');
      }
    }
  }

  // 2. Handle specific error string (if not generic placeholder)
  if (typeof errorBody.error === 'string' && errorBody.error.trim()) {
    const sanitized = sanitize(errorBody.error);
    if (sanitized && errorBody.error !== 'An unexpected error occurred.') {
      return sanitized;
    }
  }

  // 3. Handle RFC 7807 ProblemDetails "detail"
  if (typeof errorBody.detail === 'string' && errorBody.detail.trim()) {
    const sanitized = sanitize(errorBody.detail);
    if (sanitized) return sanitized;
  }

  // 4. Handle "message" property
  if (typeof errorBody.message === 'string' && errorBody.message.trim()) {
    const sanitized = sanitize(errorBody.message);
    if (sanitized) return sanitized;
  }

  // 5. Handle RFC 7807 ProblemDetails "title" (if meaningful and not generic validation header)
  if (typeof errorBody.title === 'string' && errorBody.title.trim()) {
    const sanitized = sanitize(errorBody.title);
    if (sanitized && errorBody.title !== 'One or more validation errors occurred.') {
      return sanitized;
    }
  }

  // 6. Generic error fallback
  if (errorBody.error === 'An unexpected error occurred.') {
    return 'An unexpected server error occurred. Please try again.';
  }

  // 7. Status code / statusText fallback
  if (response?.status) {
    return response.statusText ? `${response.statusText} (${response.status})` : `API Error: ${response.status}`;
  }

  return 'An unexpected error occurred. Please try again.';
}

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

  let response;
  try {
    response = await fetch(url, {
      ...options,
      headers,
    });
  } catch (err) {
    const isFetchErr =
      err instanceof TypeError ||
      err?.name === 'TypeError' ||
      err?.message?.includes('fetch') ||
      err?.message?.includes('Network');
    const networkError = new Error(
      isFetchErr
        ? 'Network error: Unable to connect to server. Please check your connection and try again.'
        : (err?.message || 'Network error occurred.')
    );
    networkError.status = 0;
    networkError.isNetworkError = true;
    networkError.originalError = err;
    throw networkError;
  }

  if (!response.ok) {
    let errorBody;
    try {
      errorBody = await response.json();
    } catch {
      errorBody = { error: response.statusText || `HTTP ${response.status}` };
    }
    const detailMessage = extractErrorMessage(errorBody, response);
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
