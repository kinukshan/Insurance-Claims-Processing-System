import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import PayoutHistory from '../../pages/payout/PayoutHistory'
import * as payoutService from '../../services/payoutService'

vi.mock('../../services/payoutService')

describe('PayoutHistory Page Component', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders history table with records from service', async () => {
    payoutService.getPayoutHistory.mockResolvedValueOnce({
      items: [
        {
          id: 'payout-uuid-1',
          claimId: 'claim-uuid-1',
          claimNumber: 'CLM-20260921-0002',
          finalPayout: 4500,
          status: 1,
          statusDisplay: 'PendingApproval',
          createdAt: '2026-09-22T10:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasNextPage: false,
    })

    render(<PayoutHistory />)

    expect(screen.getByText('Loading...')).toBeTruthy()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Payout History' })).toBeTruthy()
      expect(screen.getByText('CLM-20260921-0002')).toBeTruthy()
      expect(screen.getByText('$4,500.00')).toBeTruthy()
      expect(screen.getAllByText('Pending Approval').length).toBeGreaterThan(0)
    })
  })

  it('renders empty state when no payouts exist', async () => {
    payoutService.getPayoutHistory.mockResolvedValueOnce({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
      hasNextPage: false,
    })

    render(<PayoutHistory />)

    await waitFor(() => {
      expect(screen.getByText('No payouts found.')).toBeTruthy()
    })
  })

  it('displays session expired message on 401 error', async () => {
    const error = new Error('Unauthorized')
    error.status = 401
    payoutService.getPayoutHistory.mockRejectedValueOnce(error)

    render(<PayoutHistory />)

    await waitFor(() => {
      expect(screen.getByText('Your session has expired. Please log in again.')).toBeTruthy()
      expect(screen.queryByText('No payouts found.')).toBeNull()
    })
  })

  it('displays forbidden message on 403 error', async () => {
    const error = new Error('Forbidden')
    error.status = 403
    payoutService.getPayoutHistory.mockRejectedValueOnce(error)

    render(<PayoutHistory />)

    await waitFor(() => {
      expect(screen.getByText('You do not have permission to view payout history.')).toBeTruthy()
      expect(screen.queryByText('No payouts found.')).toBeNull()
    })
  })

  it('triggers filter reload when status filter is changed', async () => {
    payoutService.getPayoutHistory.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    })

    render(<PayoutHistory />)

    await waitFor(() => {
      expect(payoutService.getPayoutHistory).toHaveBeenCalledWith(
        expect.objectContaining({ page: 1, status: undefined })
      )
    })

    fireEvent.change(document.getElementById('payout-status-filter'), { target: { value: '1' } })

    await waitFor(() => {
      expect(payoutService.getPayoutHistory).toHaveBeenCalledWith(
        expect.objectContaining({ page: 1, status: '1' })
      )
    })
  })
})
