/**
 * Error State Tests
 *
 * Tests error state handling across the frontend:
 *
 * 1. extractErrorMessage (api.js) — normalises diverse error response shapes
 *    into user-facing strings, sanitising sensitive data.
 *
 * 2. ClaimsList component — renders loading → error state with the correct
 *    message and a Retry button that re-fetches.
 *
 * 3. Login component — displays error messages from failed login attempts
 *    and clears them on re-submit.
 *
 * 4. Register component — displays error messages for client-side validation
 *    failures and API errors.
 */

import React from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { extractErrorMessage } from '../../services/api';
import ClaimsList from '../../pages/claims/ClaimsList';
import Login from '../../pages/auth/Login';
import Register from '../../pages/auth/Register';
import * as claimService from '../../services/claimService';

// ── Mocks ──────────────────────────────────────────────────────────────────

let mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('../../services/claimService', async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    getAllClaims: vi.fn(),
  };
});

// ────────────────────────────────────────────────────────────────────────────
// 1. extractErrorMessage — error normalisation & sanitisation
// ────────────────────────────────────────────────────────────────────────────

describe('extractErrorMessage — error state normalisation', () => {
  it('returns statusText for null/undefined errorBody', () => {
    const response = { status: 500, statusText: 'Internal Server Error' };
    expect(extractErrorMessage(null, response)).toBe('Internal Server Error');
    expect(extractErrorMessage(undefined, response)).toBe('Internal Server Error');
  });

  it('returns generic fallback when errorBody is null and no response', () => {
    expect(extractErrorMessage(null, null)).toBe('An unexpected error occurred.');
  });

  it('returns API Error with status code when statusText is missing', () => {
    const response = { status: 503 };
    expect(extractErrorMessage(null, response)).toBe('API Error: 503');
  });

  it('extracts message from errors array', () => {
    const errorBody = { errors: ['Field A is required', 'Field B is invalid'] };
    const response = { status: 400, statusText: 'Bad Request' };
    expect(extractErrorMessage(errorBody, response)).toBe(
      'Field A is required; Field B is invalid',
    );
  });

  it('extracts messages from ASP.NET validation dictionary', () => {
    const errorBody = {
      title: 'One or more validation errors occurred.',
      errors: {
        Name: ['Name is required.'],
        Amount: ['Amount must be positive.'],
      },
    };
    const response = { status: 400, statusText: 'Bad Request' };
    expect(extractErrorMessage(errorBody, response)).toBe(
      'Name is required.; Amount must be positive.',
    );
  });

  it('extracts error from a single string error property', () => {
    const errorBody = { error: 'Resource not found.' };
    const response = { status: 404, statusText: 'Not Found' };
    expect(extractErrorMessage(errorBody, response)).toBe('Resource not found.');
  });

  it('extracts detail from RFC 7807 ProblemDetails', () => {
    const errorBody = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
      title: 'Not Found',
      detail: 'Claim with ID abc was not found.',
      status: 404,
    };
    const response = { status: 404, statusText: 'Not Found' };
    expect(extractErrorMessage(errorBody, response)).toBe('Claim with ID abc was not found.');
  });

  it('extracts message property when error/detail are absent', () => {
    const errorBody = { message: 'Operation failed.' };
    const response = { status: 422, statusText: 'Unprocessable Entity' };
    expect(extractErrorMessage(errorBody, response)).toBe('Operation failed.');
  });

  it('falls back to title when only title is meaningful', () => {
    const errorBody = { title: 'Forbidden', status: 403 };
    const response = { status: 403, statusText: 'Forbidden' };
    expect(extractErrorMessage(errorBody, response)).toBe('Forbidden');
  });

  it('does NOT use generic validation title as message', () => {
    const errorBody = { title: 'One or more validation errors occurred.', status: 400 };
    const response = { status: 400, statusText: 'Bad Request' };
    // Should fall through to status text since title is the generic one
    expect(extractErrorMessage(errorBody, response)).toBe('Bad Request (400)');
  });

  it('falls back to statusText (status) when errorBody has no useful fields', () => {
    const errorBody = { randomProp: 42 };
    const response = { status: 500, statusText: 'Internal Server Error' };
    expect(extractErrorMessage(errorBody, response)).toBe('Internal Server Error (500)');
  });

  it('handles the generic "An unexpected error occurred." error string by upgrading it', () => {
    const errorBody = { error: 'An unexpected error occurred.' };
    const response = { status: 500, statusText: 'Internal Server Error' };
    expect(extractErrorMessage(errorBody, response)).toBe(
      'An unexpected server error occurred. Please try again.',
    );
  });

  it('sanitises sensitive SQL / database errors', () => {
    const bodies = [
      { message: 'Npgsql.PostgresException: relation "Claims" does not exist' },
      { detail: 'at Microsoft.EntityFrameworkCore.DbContext.SaveChanges()' },
      { error: 'syntax error at or near SELECT * FROM passwords' },
    ];
    const response = { status: 500, statusText: 'Internal Server Error' };

    for (const body of bodies) {
      const result = extractErrorMessage(body, response);
      expect(result).toBe('An unexpected server error occurred. Please try again.');
    }
  });

  it('sanitises connection string / secret references', () => {
    const errorBody = { message: 'ConnectionString for PostgreSQL is invalid' };
    const response = { status: 500, statusText: 'Internal Server Error' };
    expect(extractErrorMessage(errorBody, response)).toBe(
      'An unexpected server error occurred. Please try again.',
    );
  });

  it('returns generic fallback when errorBody is empty and no response', () => {
    expect(extractErrorMessage({}, null)).toBe('An unexpected error occurred. Please try again.');
  });
});

// ────────────────────────────────────────────────────────────────────────────
// 2. ClaimsList — loading and error state rendering
// ────────────────────────────────────────────────────────────────────────────

describe('ClaimsList error state', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };
  });

  it('shows loading state initially before claims load', () => {
    // Never-resolving promise to keep the loading state
    claimService.getAllClaims.mockReturnValue(new Promise(() => {}));

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    expect(screen.getByText('Loading claims…')).toBeDefined();
  });

  it('renders error message when claim fetch fails', async () => {
    claimService.getAllClaims.mockRejectedValueOnce(new Error('Network error: Unable to connect'));

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Network error: Unable to connect/)).toBeDefined();
    });
  });

  it('renders a Retry button in the error state', async () => {
    claimService.getAllClaims.mockRejectedValueOnce(new Error('Server error'));

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Retry' })).toBeDefined();
    });
  });

  it('retries fetching claims when Retry button is clicked', async () => {
    claimService.getAllClaims
      .mockRejectedValueOnce(new Error('Failed to load claims'))
      .mockResolvedValueOnce([]);

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    // Wait for error state
    await waitFor(() => {
      expect(screen.getByText(/Failed to load claims/)).toBeDefined();
    });

    // Click Retry
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    // Should call getAllClaims again
    await waitFor(() => {
      expect(claimService.getAllClaims).toHaveBeenCalledTimes(2);
    });
  });

  it('transitions from error to data display after successful retry', async () => {
    claimService.getAllClaims
      .mockRejectedValueOnce(new Error('Temporary failure'))
      .mockResolvedValueOnce([
        {
          id: 'c-1',
          claimNumber: 'CLM-2026-0099',
          claimType: 'Motor',
          claimedAmount: 25000,
          status: 'Submitted',
          incidentDate: '2026-08-01T00:00:00Z',
          submittedAt: '2026-08-02T00:00:00Z',
          createdAt: '2026-08-01T00:00:00Z',
        },
      ]);

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Temporary failure/)).toBeDefined();
    });

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => {
      expect(screen.getByText('CLM-2026-0099')).toBeDefined();
    });

    // Error message should no longer be present
    expect(screen.queryByText(/Temporary failure/)).toBeNull();
  });

  it('uses fallback error message when error.message is empty', async () => {
    claimService.getAllClaims.mockRejectedValueOnce(new Error());

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Failed to load claims/)).toBeDefined();
    });
  });

  it('renders the error state with the correct page heading', async () => {
    mockAuth = { role: 'Policyholder', user: { userId: 'u-1', role: 'Policyholder' }, login: vi.fn(), register: vi.fn(), loading: false };
    claimService.getAllClaims.mockRejectedValueOnce(new Error('Service unavailable'));

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'My Claims' })).toBeDefined();
      expect(screen.getByText(/Service unavailable/)).toBeDefined();
    });
  });
});

// ────────────────────────────────────────────────────────────────────────────
// 3. Login component — error state rendering
// ────────────────────────────────────────────────────────────────────────────

describe('Login error state', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not display an error message initially', () => {
    mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>,
    );

    // auth-error div should not be present
    expect(screen.queryByText(/Login failed/)).toBeNull();
  });

  it('displays error when login fails', async () => {
    const loginFn = vi.fn().mockRejectedValue(new Error('Invalid credentials.'));
    mockAuth = { role: null, user: null, login: loginFn, register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>,
    );

    const emailInput = screen.getByLabelText('Email');
    const passwordInput = screen.getByLabelText('Password');
    const submitButton = screen.getByRole('button', { name: 'Sign In' });

    await userEvent.type(emailInput, 'test@example.com');
    await userEvent.type(passwordInput, 'wrongpass');
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Invalid credentials.')).toBeDefined();
    });
  });

  it('displays fallback error message when error.message is empty', async () => {
    const loginFn = vi.fn().mockRejectedValue(new Error());
    mockAuth = { role: null, user: null, login: loginFn, register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'somepass');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));

    await waitFor(() => {
      expect(screen.getByText('Login failed. Please try again.')).toBeDefined();
    });
  });

  it('clears previous error when user re-submits the form', async () => {
    let callCount = 0;
    const loginFn = vi.fn().mockImplementation(() => {
      callCount++;
      if (callCount === 1) {
        return Promise.reject(new Error('Bad credentials'));
      }
      // Second call — never resolves (keeps loading)
      return new Promise(() => {});
    });
    mockAuth = { role: null, user: null, login: loginFn, register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'wrong');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));

    await waitFor(() => {
      expect(screen.getByText('Bad credentials')).toBeDefined();
    });

    // Re-submit — the error should be cleared immediately
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));

    await waitFor(() => {
      expect(screen.queryByText('Bad credentials')).toBeNull();
    });
  });

  it('shows loading state on submit button while login is pending', async () => {
    const loginFn = vi.fn().mockReturnValue(new Promise(() => {}));
    mockAuth = { role: null, user: null, login: loginFn, register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDefined();
    });
  });
});

// ────────────────────────────────────────────────────────────────────────────
// 4. Register component — error state rendering
// ────────────────────────────────────────────────────────────────────────────

describe('Register error state', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not display an error message initially', () => {
    mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>,
    );

    expect(screen.queryByText(/Registration failed/)).toBeNull();
    expect(screen.queryByText(/Passwords do not match/)).toBeNull();
  });

  it('displays error when passwords do not match', async () => {
    mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('First Name'), 'John');
    await userEvent.type(screen.getByLabelText('Last Name'), 'Doe');
    await userEvent.type(screen.getByLabelText('Email'), 'john@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'SecurePass1');
    await userEvent.type(screen.getByLabelText('Confirm Password'), 'DifferentPass');
    await userEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    await waitFor(() => {
      expect(screen.getByText('Passwords do not match.')).toBeDefined();
    });

    // register API should NOT have been called
    expect(mockAuth.register).not.toHaveBeenCalled();
  });

  it('displays error when password is too short', async () => {
    mockAuth = { role: null, user: null, login: vi.fn(), register: vi.fn(), loading: false };

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('First Name'), 'Jane');
    await userEvent.type(screen.getByLabelText('Last Name'), 'Doe');
    await userEvent.type(screen.getByLabelText('Email'), 'jane@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'short');
    await userEvent.type(screen.getByLabelText('Confirm Password'), 'short');
    await userEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    await waitFor(() => {
      expect(screen.getByText('Password must be at least 8 characters.')).toBeDefined();
    });

    expect(mockAuth.register).not.toHaveBeenCalled();
  });

  it('displays API error when registration call fails', async () => {
    const registerFn = vi.fn().mockRejectedValue(new Error('Email already registered.'));
    mockAuth = { role: null, user: null, login: vi.fn(), register: registerFn, loading: false };

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('First Name'), 'Alice');
    await userEvent.type(screen.getByLabelText('Last Name'), 'Smith');
    await userEvent.type(screen.getByLabelText('Email'), 'alice@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'ValidPass123');
    await userEvent.type(screen.getByLabelText('Confirm Password'), 'ValidPass123');
    await userEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    await waitFor(() => {
      expect(screen.getByText('Email already registered.')).toBeDefined();
    });
  });

  it('displays fallback error when API error has no message', async () => {
    const registerFn = vi.fn().mockRejectedValue(new Error());
    mockAuth = { role: null, user: null, login: vi.fn(), register: registerFn, loading: false };

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('First Name'), 'Bob');
    await userEvent.type(screen.getByLabelText('Last Name'), 'Jones');
    await userEvent.type(screen.getByLabelText('Email'), 'bob@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'Password99');
    await userEvent.type(screen.getByLabelText('Confirm Password'), 'Password99');
    await userEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    await waitFor(() => {
      expect(screen.getByText('Registration failed. Please try again.')).toBeDefined();
    });
  });
});
