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
  syncPaymentStatus: vi.fn(),
  getPaymentProviderInfo: vi.fn(),
}))

import PayoutApproval from '../../pages/payout/PayoutApproval'
import {
  getPayoutById,
  getPayoutByClaim,
  approvePayout,
  rejectPayout,
  requestRevision,
  executePayout,
  syncPaymentStatus,
  getPaymentProviderInfo,
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
    getPaymentProviderInfo.mockResolvedValue({ provider: 'Mock', isMock: true })
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

  it('calls executePayout when Admin clicks Execute Payout', async () => {
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Underwriter' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)
    executePayout.mockResolvedValueOnce({
      payoutId: 'payout-1',
      transactionId: 'tx-1',
      providerTransactionId: 'MOCK-TX-123',
      status: 'Succeeded',
      message: 'Payment executed successfully.',
    })
    getPayoutById.mockResolvedValueOnce({
      ...approvedPayout,
      status: 6,
      statusDisplay: 'Paid',
      paymentReference: 'MOCK-TX-123',
    })

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    fireEvent.click(screen.getByText(/Execute Payout/i))

    await waitFor(() => {
      expect(executePayout).toHaveBeenCalledWith('payout-1')
      expect(screen.getByText(/Payment executed successfully/i)).toBeTruthy()
    })
  })

  it('displays executing state and disables button during execution', async () => {
    let resolveExecute
    const executePromise = new Promise((resolve) => {
      resolveExecute = resolve
    })
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Underwriter' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)
    executePayout.mockReturnValueOnce(executePromise)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    const btn = screen.getByText(/Execute Payout/i)
    fireEvent.click(btn)

    // Button should now show "Executing..." and be disabled
    expect(screen.getByText('Executing...')).toBeTruthy()
    expect(screen.getByText('Executing...').closest('button').disabled).toBe(true)

    // Resolve execution
    resolveExecute({
      payoutId: 'payout-1',
      transactionId: 'tx-1',
      providerTransactionId: 'MOCK-TX-123',
      status: 'Succeeded',
      message: 'Done',
    })
    getPayoutById.mockResolvedValueOnce({ ...approvedPayout, status: 6, statusDisplay: 'Paid' })

    await waitFor(() => {
      expect(screen.queryByText('Executing...')).toBeNull()
    })
  })

  it('displays error safely when executePayout fails', async () => {
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Underwriter' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)
    const error = new Error('Payment gateway encountered an error.')
    error.status = 502
    executePayout.mockRejectedValueOnce(error)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    fireEvent.click(screen.getByText(/Execute Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/Payment gateway encountered an error/i)).toBeTruthy()
    })
  })

  it('hides execute button for ClaimsAdjuster on Approved status', async () => {
    mockAuth.role = 'ClaimsAdjuster'
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

  it('renders Mock Payment Gateway label and does NOT render PayPal Sandbox by default', async () => {
    getPayoutById.mockResolvedValueOnce(mockPayout)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/Mock Payment Gateway/i)).toBeTruthy()
      expect(screen.queryByText(/PayPal Sandbox/i)).toBeNull()
      expect(screen.queryByText(/PayPal/i)).toBeNull()
    })
  })

  it('displays "Processing Payment..." during execution when provider is Mock', async () => {
    let resolveExecute
    const executePromise = new Promise((resolve) => {
      resolveExecute = resolve
    })
    const approvedPayout = { ...mockPayout, status: 2, statusDisplay: 'Approved', approvedBy: 'Test Underwriter' }
    getPayoutById.mockResolvedValueOnce(approvedPayout)
    executePayout.mockReturnValueOnce(executePromise)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    fireEvent.click(screen.getByText(/Execute Payout/i))

    // Should display neutral "Processing Payment..." and NOT "Sending to PayPal Sandbox..."
    expect(screen.getByText('Processing Payment...')).toBeTruthy()
    expect(screen.queryByText('Sending to PayPal Sandbox...')).toBeNull()

    resolveExecute({
      payoutId: 'payout-1',
      transactionId: 'tx-1',
      provider: 'Mock',
      providerTransactionId: 'MOCK-PAY-12345',
      status: 'Succeeded',
      message: 'MOCK: Payment completed successfully.'
    })
  })

  it('renders PayPal Sandbox label and "Sending to PayPal Sandbox..." when provider is PayPalSandbox', async () => {
    getPaymentProviderInfo.mockResolvedValueOnce({ provider: 'PayPalSandbox', isMock: false })
    const payPalPayout = {
      ...mockPayout,
      status: 2,
      statusDisplay: 'Approved',
      approvedBy: 'Test Underwriter',
      paymentProvider: 'PayPalSandbox'
    }
    getPayoutById.mockResolvedValueOnce(payPalPayout)

    let resolveExecute
    const executePromise = new Promise((resolve) => {
      resolveExecute = resolve
    })
    executePayout.mockReturnValueOnce(executePromise)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => {
      expect(screen.getByText(/PayPal Sandbox/i)).toBeTruthy()
    })

    fireEvent.click(screen.getByText(/Execute Payout/i))

    expect(screen.getByText('Sending to PayPal Sandbox...')).toBeTruthy()
    expect(screen.queryByText('Processing Payment...')).toBeNull()

    resolveExecute({
      payoutId: 'payout-1',
      transactionId: 'tx-paypal-1',
      provider: 'PayPalSandbox',
      status: 'Processing',
      message: 'PayPal Sandbox payout created.'
    })
  })

  it('displays safe error message when execution fails', async () => {
    const approvedPayout = {
      ...mockPayout,
      status: 2,
      statusDisplay: 'Approved',
      approvedBy: 'Test Underwriter'
    }
    getPayoutById.mockResolvedValueOnce(approvedPayout)

    const execError = new Error('Payment gateway encountered an error. The payout was not completed.')
    execError.status = 502
    executePayout.mockRejectedValueOnce(execError)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    fireEvent.click(screen.getByText(/Execute Payout/i))

    await waitFor(() => {
      expect(screen.getByText('Payment gateway encountered an error. The payout was not completed.')).toBeTruthy()
    })
  })

  it('sanitizes internal SQL or exception errors from being exposed to the user', async () => {
    const approvedPayout = {
      ...mockPayout,
      status: 2,
      statusDisplay: 'Approved',
      approvedBy: 'Test Underwriter'
    }
    getPayoutById.mockResolvedValueOnce(approvedPayout)

    const leakError = new Error('Npgsql.PostgresException: 42P01: relation "PaymentTransactions" does not exist at Host=ep-test.aws.neon.tech')
    leakError.status = 500
    executePayout.mockRejectedValueOnce(leakError)

    render(<PayoutApproval />)
    const input = screen.getByPlaceholderText(/Enter Payout ID/i)
    fireEvent.change(input, { target: { value: 'payout-1' } })
    fireEvent.click(screen.getByText(/Load Payout/i))

    await waitFor(() => screen.getByText(/Execute Payout/i))
    fireEvent.click(screen.getByText(/Execute Payout/i))

    await waitFor(() => {
      // Must NOT contain sensitive SQL or connection details
      expect(screen.queryByText(/relation "PaymentTransactions" does not exist/i)).toBeNull()
      expect(screen.queryByText(/neon\.tech/i)).toBeNull()
      expect(screen.getByText('Payment execution failed due to an internal system error. Please contact an administrator.')).toBeTruthy()
    })
  })
})
