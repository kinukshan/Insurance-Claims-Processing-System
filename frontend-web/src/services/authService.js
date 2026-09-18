/**
 * Authentication service — shared infrastructure.
 * All requests go to ASP.NET Core — never directly to the database.
 */

import { apiFetch } from './api';

/**
 * Register a new Policyholder account.
 */
export async function register({ firstName, lastName, email, password, confirmPassword }) {
  return apiFetch('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ firstName, lastName, email, password, confirmPassword }),
  });
}

/**
 * Login with email and password.
 */
export async function login({ email, password }) {
  return apiFetch('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
}

/**
 * Get the current authenticated user's profile.
 */
export async function getCurrentUser() {
  return apiFetch('/auth/me');
}

/**
 * Logout — clear token from sessionStorage.
 */
export function logout() {
  sessionStorage.removeItem('auth_token');
  sessionStorage.removeItem('auth_user');
}

/**
 * Save auth data to sessionStorage after login/register.
 */
export function saveAuth(authResponse) {
  sessionStorage.setItem('auth_token', authResponse.token);
  sessionStorage.setItem('auth_user', JSON.stringify({
    userId: authResponse.userId,
    firstName: authResponse.firstName,
    lastName: authResponse.lastName,
    email: authResponse.email,
    role: authResponse.role,
  }));
}

/**
 * Get stored user from sessionStorage.
 */
export function getStoredUser() {
  const raw = sessionStorage.getItem('auth_user');
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

/**
 * Check if a token exists in sessionStorage.
 */
export function isAuthenticated() {
  return !!sessionStorage.getItem('auth_token');
}
