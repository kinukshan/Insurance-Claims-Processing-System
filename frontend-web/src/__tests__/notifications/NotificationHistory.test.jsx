/**
 * NotificationHistory component tests.
 * Tests rendering, loading state, empty state, and notification display.
 */

import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import NotificationHistory from '../../pages/notifications/NotificationHistory';
import * as notificationService from '../../services/notificationService';

let mockAuth = { role: null, user: null };
vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('../../services/notificationService', async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    getNotifications: vi.fn(),
    getNotificationsByClaimId: vi.fn(),
    getNotificationsByPayoutId: vi.fn(),
  };
});

describe('NotificationHistory Component Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth = { role: null, user: null };
  });

  // Test 1: Exports expected service functions
  it('exports expected notificationService functions', () => {
    expect(typeof notificationService.getNotifications).toBe('function');
    expect(typeof notificationService.getNotificationsByClaimId).toBe('function');
    expect(typeof notificationService.getNotificationsByPayoutId).toBe('function');
  });

  // Test 2: Renders loading state initially
  it('shows loading indicator while fetching', () => {
    mockAuth = { role: 'Admin', user: { id: 'u-1', role: 'Admin' } };
    notificationService.getNotifications.mockReturnValue(new Promise(() => {})); // never resolves
    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );
    expect(screen.getByText('Loading notifications…')).toBeDefined();
  });

  // Test 3: Renders empty state
  it('renders empty state when no notifications exist', async () => {
    mockAuth = { role: 'Admin', user: { id: 'u-1', role: 'Admin' } };
    notificationService.getNotifications.mockResolvedValueOnce([]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('No notifications found.')).toBeDefined();
    });
  });

  // Test 4: Renders notifications for staff
  it('renders system-wide audit log heading for staff', async () => {
    mockAuth = { role: 'ClaimsAdjuster', user: { id: 'u-adj', role: 'ClaimsAdjuster' } };
    notificationService.getNotifications.mockResolvedValueOnce([
      {
        id: 'n-1',
        userId: 'u-1',
        claimId: 'c-1',
        notificationType: 'ClaimSubmitted',
        channel: 'Email',
        recipient: 'user@example.com',
        subject: 'Claim CLM-001 — Submitted Successfully',
        success: true,
        errorMessage: null,
        provider: 'Mock',
        providerMessageId: 'MOCK-EMAIL-abc123',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('System-wide notification audit log')).toBeDefined();
      expect(screen.getByText('Claim Submitted')).toBeDefined();
      expect(screen.getByText('user@example.com')).toBeDefined();
      expect(screen.getByText('Mock')).toBeDefined();
    });
  });

  // Test 5: Renders personal heading for policyholders
  it('renders personal heading for Policyholder', async () => {
    mockAuth = { role: 'Policyholder', user: { id: 'u-ph', role: 'Policyholder' } };
    notificationService.getNotifications.mockResolvedValueOnce([]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Your notification history')).toBeDefined();
    });
  });

  // Test 6: Handles API error gracefully with safe message and Retry button
  it('displays safe error message and does not show "No notifications found" on API failure', async () => {
    mockAuth = { role: 'Policyholder', user: { id: 'u-ph', role: 'Policyholder' } };
    notificationService.getNotifications.mockRejectedValueOnce(
      new Error('42P01: relation "NotificationLogs" does not exist POSITION: 275')
    );

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/Unable to load notifications. Please try again./)).toBeDefined();
    });

    // Must NOT display raw PostgreSQL errors
    expect(screen.queryByText(/42P01/)).toBeNull();
    expect(screen.queryByText(/NotificationLogs/)).toBeNull();

    // Must NOT show "No notifications found." simultaneously when an error occurs
    expect(screen.queryByText('No notifications found.')).toBeNull();

    // Retry button must be available
    expect(screen.getByRole('button', { name: /retry/i })).toBeDefined();
  });

  // Test 6b: Retry button successfully refetches notifications
  it('allows retrying when loading fails', async () => {
    mockAuth = { role: 'Policyholder', user: { id: 'u-ph', role: 'Policyholder' } };
    notificationService.getNotifications
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce([
        {
          id: 'n-retry',
          userId: 'u-ph',
          claimId: 'c-1',
          notificationType: 'ClaimSubmitted',
          channel: 'Email',
          recipient: 'ph-retry@example.com',
          subject: 'Claim Submitted Successfully',
          success: true,
          errorMessage: null,
          provider: 'Mock',
          providerMessageId: 'M-1',
          sentAt: '2026-09-24T12:00:00Z',
          createdAt: '2026-09-24T12:00:00Z',
        },
      ]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/Unable to load notifications. Please try again./)).toBeDefined();
    });

    const retryBtn = screen.getByRole('button', { name: /retry/i });
    fireEvent.click(retryBtn);

    await waitFor(() => {
      expect(screen.getByText('ph-retry@example.com')).toBeDefined();
      expect(screen.getByText('Claim Submitted Successfully')).toBeDefined();
      expect(screen.queryByText(/Unable to load notifications/)).toBeNull();
    });
  });

  // Test 7: Renders success and failure icons
  it('renders success and failure status icons', async () => {
    mockAuth = { role: 'Admin', user: { id: 'u-1', role: 'Admin' } };
    notificationService.getNotifications.mockResolvedValueOnce([
      {
        id: 'n-ok',
        userId: 'u-1',
        claimId: 'c-1',
        notificationType: 'ClaimApproved',
        channel: 'Email',
        recipient: 'ok@example.com',
        subject: 'Approved',
        success: true,
        errorMessage: null,
        provider: 'Mock',
        providerMessageId: 'M-1',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
      {
        id: 'n-fail',
        userId: 'u-2',
        claimId: 'c-2',
        notificationType: 'ClaimRejected',
        channel: 'Email',
        recipient: 'fail@example.com',
        subject: 'Rejected',
        success: false,
        errorMessage: 'Delivery failed',
        provider: 'Mock',
        providerMessageId: 'M-2',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('✅')).toBeDefined();
      expect(screen.getByText('❌')).toBeDefined();
      expect(screen.getByText('Delivery failed')).toBeDefined();
    });
  });

  // Test 8: Renders '📨' icon for Accepted status, distinguishing provider acceptance from confirmed delivery
  it('renders Accepted status with envelope icon (📨) instead of green checkmark (✅)', async () => {
    mockAuth = { role: 'Policyholder', user: { id: 'u-ph', role: 'Policyholder' } };
    notificationService.getNotifications.mockResolvedValueOnce([
      {
        id: 'n-resend',
        userId: 'u-ph',
        claimId: 'c-1',
        notificationType: 'ClaimApproved',
        channel: 'Email',
        recipient: 'policyholder@example.com',
        subject: 'Claim Approved',
        success: true,
        status: 'Accepted',
        errorMessage: null,
        provider: 'Resend',
        providerMessageId: 'resend_email_123',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      // Must show Accepted envelope icon
      expect(screen.getByText('📨')).toBeDefined();
      // Must NOT show confirmed delivery green checkmark
      expect(screen.queryByText('✅')).toBeNull();
      // Provider must display Resend
      expect(screen.getByText('Resend')).toBeDefined();
    });
  });

  // Test 9: Displays both Mock (Sent) and Resend (Accepted) accurately in audit view
  it('distinguishes Mock (Sent ✅) and Resend (Accepted 📨) side-by-side in audit view', async () => {
    mockAuth = { role: 'Admin', user: { id: 'u-admin', role: 'Admin' } };
    notificationService.getNotifications.mockResolvedValueOnce([
      {
        id: 'n-mock',
        userId: 'u-1',
        claimId: 'c-1',
        notificationType: 'ClaimSubmitted',
        channel: 'Email',
        recipient: 'user1@example.com',
        subject: 'Submitted',
        success: true,
        status: 'Sent',
        errorMessage: null,
        provider: 'Mock',
        providerMessageId: 'M-1',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
      {
        id: 'n-resend',
        userId: 'u-2',
        claimId: 'c-2',
        notificationType: 'ClaimApproved',
        channel: 'Email',
        recipient: 'user2@example.com',
        subject: 'Approved',
        success: true,
        status: 'Accepted',
        errorMessage: null,
        provider: 'Resend',
        providerMessageId: 're_456',
        sentAt: '2026-09-24T12:00:00Z',
        createdAt: '2026-09-24T12:00:00Z',
      },
    ]);

    render(
      <MemoryRouter>
        <NotificationHistory />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('✅')).toBeDefined();
      expect(screen.getByText('📨')).toBeDefined();
      expect(screen.getByText('Mock')).toBeDefined();
      expect(screen.getByText('Resend')).toBeDefined();
    });
  });
});
