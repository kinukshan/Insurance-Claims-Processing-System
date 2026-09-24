/**
 * Tests for payout service — verifies authenticated apiFetch usage.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// Mock the api module before importing payoutService
vi.mock('../../services/api', () => {
  const apiFetch = vi.fn()
  return { apiFetch, API_BASE_URL: 'http://localhost:5000/api' }
})

import { apiFetch } from '../../services/api'
import {
  calculatePayout,
  getPayoutById,
  getPayoutByClaim,
  getPayoutHistory,
  getMyPayouts,
  approvePayout,
  rejectPayout,
  requestRevision,
  executePayout,
  updatePayout,
  deletePayout,
  getPaymentTransactions,
  syncPaymentStatus,
  getPaymentProviderInfo,
} from '../../services/payoutService'

describe('payoutService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiFetch.mockResolvedValue({ id: 'test-payout-id' })
  })

  // Test 1: All functions use the authenticated apiFetch helper
  it('uses apiFetch for calculatePayout', async () => {
    await calculatePayout('claim-123')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/calculate/claim-123', { method: 'POST' })
  })

  it('uses apiFetch for getPayoutById', async () => {
    await getPayoutById('payout-456')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-456')
  })

  it('uses apiFetch for getPayoutByClaim', async () => {
    await getPayoutByClaim('claim-789')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/claim/claim-789')
  })

  // Test 2: Correct endpoint/method/payload
  it('sends correct endpoint and method for getPayoutHistory', async () => {
    await getPayoutHistory({ page: 2, pageSize: 10, status: '1', sortBy: 'FinalPayout', sortDescending: true })
    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [endpoint] = apiFetch.mock.calls[0]
    expect(endpoint).toContain('/payouts/history')
    expect(endpoint).toContain('page=2')
    expect(endpoint).toContain('pageSize=10')
    expect(endpoint).toContain('status=1')
    expect(endpoint).toContain('sortBy=FinalPayout')
    expect(endpoint).toContain('sortDescending=true')
  })

  it('sends correct endpoint and method for getMyPayouts', async () => {
    await getMyPayouts({ page: 2, pageSize: 10, status: '1', sortBy: 'FinalPayout', sortDescending: true })
    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [endpoint] = apiFetch.mock.calls[0]
    expect(endpoint).toContain('/payouts/my')
    expect(endpoint).toContain('page=2')
    expect(endpoint).toContain('pageSize=10')
    expect(endpoint).toContain('status=1')
    expect(endpoint).toContain('sortBy=FinalPayout')
    expect(endpoint).toContain('sortDescending=true')
  })

  it('sends correct method and body for approvePayout', async () => {
    await approvePayout('payout-1', 'Approved after review')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-1/approve', {
      method: 'POST',
      body: JSON.stringify({ comments: 'Approved after review' }),
    })
  })

  it('sends correct method and body for rejectPayout', async () => {
    await rejectPayout('payout-2', 'Insufficient documentation')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-2/reject', {
      method: 'POST',
      body: JSON.stringify({ comments: 'Insufficient documentation' }),
    })
  })

  it('sends correct method for requestRevision', async () => {
    await requestRevision('payout-3', 'Need more details')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-3/request-revision', {
      method: 'POST',
      body: JSON.stringify({ comments: 'Need more details' }),
    })
  })

  it('sends correct method for executePayout', async () => {
    await executePayout('payout-4')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-4/execute', { method: 'POST' })
  })

  it('sends correct method for updatePayout', async () => {
    await updatePayout('payout-5')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-5', { method: 'PUT' })
  })

  it('sends correct method for deletePayout', async () => {
    await deletePayout('payout-6')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-6', { method: 'DELETE' })
  })

  it('sends correct method for getPaymentTransactions', async () => {
    await getPaymentTransactions('payout-7')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-7/payments')
  })

  it('sends correct method for syncPaymentStatus', async () => {
    await syncPaymentStatus('payout-8')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-8/payments/sync', { method: 'POST' })
  })

  it('sends correct method for getPaymentProviderInfo', async () => {
    await getPaymentProviderInfo()
    expect(apiFetch).toHaveBeenCalledWith('/payouts/provider-info')
  })

  // Test 3: Error handling — errors propagate from apiFetch
  it('propagates 401 error from apiFetch', async () => {
    const error = new Error('Unauthorized')
    error.status = 401
    apiFetch.mockRejectedValue(error)

    await expect(calculatePayout('claim-x')).rejects.toThrow('Unauthorized')
    await expect(calculatePayout('claim-x')).rejects.toMatchObject({ status: 401 })
  })

  it('propagates 403 error from apiFetch', async () => {
    const error = new Error('Forbidden')
    error.status = 403
    apiFetch.mockRejectedValueOnce(error)

    await expect(getPayoutHistory()).rejects.toThrow('Forbidden')
  })

  it('propagates backend error messages', async () => {
    const error = new Error('A payout already exists for this claim')
    error.status = 400
    apiFetch.mockRejectedValueOnce(error)

    await expect(calculatePayout('claim-dup')).rejects.toThrow('A payout already exists')
  })

  // Test: Default params for getPayoutHistory
  it('sends default params for getPayoutHistory', async () => {
    await getPayoutHistory()
    const [endpoint] = apiFetch.mock.calls[0]
    expect(endpoint).toContain('page=1')
    expect(endpoint).toContain('pageSize=20')
  })

  // Test: Default params for getMyPayouts
  it('sends default params for getMyPayouts', async () => {
    await getMyPayouts()
    const [endpoint] = apiFetch.mock.calls[0]
    expect(endpoint).toContain('/payouts/my')
    expect(endpoint).toContain('page=1')
    expect(endpoint).toContain('pageSize=20')
  })

  // Test: Empty comments default
  it('sends empty comments by default for approvePayout', async () => {
    await approvePayout('payout-x')
    expect(apiFetch).toHaveBeenCalledWith('/payouts/payout-x/approve', {
      method: 'POST',
      body: JSON.stringify({ comments: '' }),
    })
  })

  // Test: No raw fetch usage — payoutService should NOT import fetch directly
  it('does not use raw fetch (all calls go through apiFetch)', async () => {
    const globalFetch = vi.spyOn(globalThis, 'fetch')
    await calculatePayout('claim-test')
    expect(globalFetch).not.toHaveBeenCalled()
    globalFetch.mockRestore()
  })
})
