/**
 * Tests for PayoutApproval page component.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import React from 'react'

// Mock the auth context
const mockAuth = { role: 'Admin', user: { firstName: 'Test', lastName: 'Admin' } }
vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

// Mock the payout service
vi.mock('../../services/payoutService', () => ({
  getPayoutById: vi.fn(),
  getPayoutByClaim: vi.fn(),
  approvePayout: vi.fn(),
  rejectPayout: vi.fn(),
  requestRevision: vi.fn(),
  executePayout: vi.fn(),
}))

import PayoutApproval from '../../pages/payout/PayoutApproval'
import {
  getPayoutById,
  getPayoutByClaim,
  approvePayout,
  rejectPayout,
  requestRevision,
  executePayout,
} from '../../services/payoutService'

const mockPayout = {
  id: 'payout-1',
  claimId: 'claim-1',
  claimNumber: 'CLM-20260921-0002',
  approvedClaimAmount: 15000,
  coverageLimit: 50000,
  deductible: 500,
  proposedPayout: 14500,
  finalPayout: 14500,
  status: 1, // PendingApproval
  statusDisplay: 'PendingApproval',
  approvals: [],
  createdAt: '2026-09-22T00:00:00Z',
  updatedAt: '2026-09-22T00:00:00Z',
}

describe('PayoutApproval', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAuth.role = 'Admin'
  })

  it('renders lookup form', () => {
    render(<PayoutApproval />)
    expect(screen.getByPlaceholderText(/Enter Payout ID/i)).toBeTruthy()
    expect(screen.getByText(/Load Payout/i)).toBeTruthy()
  })

  it('loads payout by ID', async () => {
    getPayoutById.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText('Payout Details')).toBeTruthy()
      expect(screen.getByText('$14,500.00')).toBeTruthy()
    })
  })

  it('falls back to claim ID lookup on 404', async () => {
    const notFoundError = new Error('Not found')
    notFoundError.status = 404
    getPayoutById.mockRejectedValueOnce(notFoundError)
    getPayoutByClaim.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'claim-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(getPayoutByClaim).toHaveBeenCalledWith('claim-1')
      expect(screen.getByText('Payout Details')).toBeTruthy()
    })
  })

  it('shows 401 auth error clearly', async () => {
    const authError = new Error('Unauthorized')
    authError.status = 401
    getPayoutById.mockRejectedValueOnce(authError)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/session has expired/i)).toBeTruthy()
    })
  })

  it('shows 403 forbidden error clearly', async () => {
    const forbiddenError = new Error('Forbidden')
    forbiddenError.status = 403
    getPayoutById.mockRejectedValueOnce(forbiddenError)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/do not have permission/i)).toBeTruthy()
    })
  })

  it('shows approve/reject buttons for Admin on PendingApproval', async () => {
    getPayoutById.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText('✓ Approve')).toBeTruthy()
      expect(screen.getByText('✕ Reject')).toBeTruthy()
      expect(screen.getByText('↻ Request Revision')).toBeTruthy()
    })
  })

  it('shows approve/reject buttons for Underwriter on PendingApproval', async () => {
    mockAuth.role = 'Underwriter'
    getPayoutById.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText('✓ Approve')).toBeTruthy()
      expect(screen.getByText('✕ Reject')).toBeTruthy()
    })
  })

  it('hides approve/reject for ClaimsAdjuster on PendingApproval', async () => {
    mockAuth.role = 'ClaimsAdjuster'
    getPayoutById.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.queryByText('✓ Approve')).toBeNull()
      expect(screen.getByText(/Underwriter or Admin must approve/i)).toBeTruthy()
    })
  })

  it('shows execute button only for Admin on Approved status', async () => {
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Underwriter' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/Execute Payout/i)).toBeTruthy()
    })
  })

  it('hides execute button for Underwriter on Approved status', async () => {
    mockAuth.role = 'Underwriter'
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Admin' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.queryByText(/Execute Payout/i)).toBeNull()
      expect(screen.getByText(/Admin must execute/i)).toBeTruthy()
    })
  })

  it('calls approvePayout when approve is clicked', async () => {
    getPayoutById.mockResolvedValueOnce(mockPayout)
    approvePayout.mockResolvedValueOnce({ ...mockPayout, status: 2, statusDisplay: 'Approved' })

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText('✓ Approve'))
    fireEvent.click(screen.getByText('✓ Approve'))

    await waitFor(() => {
      expect(approvePayout).toHaveBeenCalledWith('payout-1', '')
      expect(screen.getByText(/Action completed/i)).toBeTruthy()
    })
  })
})
