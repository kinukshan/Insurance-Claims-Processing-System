/**
 * Base API client configuration.
 * All requests go to ASP.NET Core — never directly to the AI service.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

// TODO: Implement axios/fetch wrapper with JWT interceptor
export { API_BASE_URL };
