/**
 * ClaimsList component tests — Component B (Member 2).
 * Tests list rendering, role-based headings, and scoped count display.
 */

import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import ClaimsList from '../../pages/claims/ClaimsList';
import * as claimService from '../../services/claimService';

let mockAuth = { role: null, user: null };
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

describe('ClaimsList Component Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth = { role: null, user: null };
  });

  // Test 1: claimService exports expected functions
  it('exports expected claimService functions', () => {
    expect(typeof claimService.getAllClaims).toBe('function');
    expect(typeof claimService.getClaim).toBe('function');
    expect(typeof claimService.createClaim).toBe('function');
  });

  // Test 2: Renders "My Claims" heading and scoped count for Policyholder
  it('renders My Claims heading and scoped count for Policyholder', async () => {
    mockAuth = { role: 'Policyholder', user: { userId: 'u-1', role: 'Policyholder' } };
    claimService.getAllClaims.mockResolvedValueOnce([
      {
        id: 'claim-1',
        claimNumber: 'CLM-2026-0001',
        claimType: 'Auto',
        claimedAmount: 1500,
        status: 'Submitted',
        incidentDate: '2026-09-01T00:00:00Z',
        submittedAt: '2026-09-02T00:00:00Z',
        createdAt: '2026-09-01T00:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'My Claims' })).toBeDefined();
      expect(screen.getByText('1 claim')).toBeDefined();
      expect(screen.getByText('CLM-2026-0001')).toBeDefined();
    });
  });

  // Test 3: Renders "Claims Management" heading for staff
  it('renders Claims Management heading for staff roles', async () => {
    mockAuth = { role: 'ClaimsAdjuster', user: { userId: 'u-adj', role: 'ClaimsAdjuster' } };
    claimService.getAllClaims.mockResolvedValueOnce([
      {
        id: 'claim-1',
        claimNumber: 'CLM-2026-0001',
        claimType: 'Auto',
        claimedAmount: 1500,
        status: 'Submitted',
        incidentDate: '2026-09-01T00:00:00Z',
        submittedAt: '2026-09-02T00:00:00Z',
        createdAt: '2026-09-01T00:00:00Z',
      },
      {
        id: 'claim-2',
        claimNumber: 'CLM-2026-0002',
        claimType: 'Home',
        claimedAmount: 3200,
        status: 'UnderReview',
        incidentDate: '2026-09-03T00:00:00Z',
        submittedAt: '2026-09-04T00:00:00Z',
        createdAt: '2026-09-03T00:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <ClaimsList />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Claims Management' })).toBeDefined();
      expect(screen.getByText('2 claims')).toBeDefined();
    });
  });
});
