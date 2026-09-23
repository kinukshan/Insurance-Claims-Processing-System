import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import PayoutCalculation from '../../pages/payout/PayoutCalculation'
import * as payoutService from '../../services/payoutService'
import * as claimService from '../../services/claimService'
import * as policyService from '../../services/policyService'

let mockSearchParams = new URLSearchParams()
const mockNavigate = vi.fn()

vi.mock('react-router-dom', () => ({
  useSearchParams: () => [mockSearchParams, vi.fn()],
  useNavigate: () => mockNavigate,
  Link: ({ children, to, id, ...props }) => <a href={to} id={id} {...props}>{children}</a>,
}))

vi.mock('../../services/payoutService')
vi.mock('../../services/claimService')
vi.mock('../../services/policyService')

describe('PayoutCalculation Page Component', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockSearchParams = new URLSearchParams()
    payoutService.getPayoutByClaim.mockReset()
    const err404 = new Error('Not found')
    err404.status = 404
    payoutService.getPayoutByClaim.mockRejectedValue(err404)
    claimService.getClaim.mockReset()
    claimService.getClaim.mockResolvedValue(null)
    policyService.getPolicyById.mockReset()
    policyService.getPolicyById.mockResolvedValue(null)
  })

  it('renders calculation form with input and buttons', () => {
    render(<PayoutCalculation />)

    expect(screen.getByRole('heading', { name: 'Calculate Payout' })).toBeTruthy()
    expect(screen.getByPlaceholderText('Enter Claim ID (GUID)')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Calculate Payout' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Look Up Existing' })).toBeTruthy()
  })

  it('calculates payout and renders proposal with validation and Gemini explanation', async () => {
    payoutService.calculatePayout.mockResolvedValueOnce({
      id: 'payout-123',
      claimId: 'claim-456',
      claimNumber: 'CLM-20260921-0002',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 5000,
      coverageLimit: 20000,
      deductible: 500,
      proposedPayout: 4500,
      finalPayout: 4500,
      validationResult: {
        valid: true,
        violations: [],
        requiresHumanApproval: true,
        aiUsed: true,
        aiModel: 'gemini-1.5-flash',
        reasoningSummary: 'Claim amount is consistent with reported collision damages.',
        fallbackUsed: false,
      },
    })

    render(<PayoutCalculation />)

    const input = screen.getByPlaceholderText('Enter Claim ID (GUID)')
    fireEvent.change(input, { target: { value: 'claim-456' } })

    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText('Payout proposal created successfully.')).toBeTruthy()
      expect(screen.getByText('Payout Proposal')).toBeTruthy()
      expect(screen.getByText('Claim: CLM-20260921-0002')).toBeTruthy()
      expect(screen.getByText('Validation: ✓ Passed')).toBeTruthy()
      expect(screen.getByText(/Gemini Contextual Explanation/i)).toBeTruthy()
      expect(screen.getByText('Claim amount is consistent with reported collision damages.')).toBeTruthy()
    })
  })

  it('displays fallback notice when fallbackUsed is true', async () => {
    payoutService.calculatePayout.mockResolvedValueOnce({
      id: 'payout-789',
      claimId: 'claim-789',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 3000,
      coverageLimit: 10000,
      deductible: 500,
      proposedPayout: 2500,
      finalPayout: 2500,
      validationResult: {
        valid: true,
        violations: [],
        requiresHumanApproval: true,
        aiUsed: false,
        fallbackUsed: true,
      },
    })

    render(<PayoutCalculation />)

    const input = screen.getByPlaceholderText('Enter Claim ID (GUID)')
    fireEvent.change(input, { target: { value: 'claim-789' } })

    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText(/Rule-based validation used \(AI contextual analysis unavailable\)/)).toBeTruthy()
    })
  })

  it('displays validation failure and violations when valid is false', async () => {
    payoutService.calculatePayout.mockResolvedValueOnce({
      id: 'payout-fail',
      claimId: 'claim-fail',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 30000,
      coverageLimit: 20000,
      deductible: 500,
      proposedPayout: 19500,
      finalPayout: 19500,
      validationResult: {
        valid: false,
        violations: ['Payout exceeds policy coverage limit.'],
        requiresHumanApproval: true,
        aiUsed: false,
        fallbackUsed: true,
      },
    })

    render(<PayoutCalculation />)

    const input = screen.getByPlaceholderText('Enter Claim ID (GUID)')
    fireEvent.change(input, { target: { value: 'claim-fail' } })

    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText('Validation: ✕ Failed')).toBeTruthy()
      expect(screen.getByText('Payout exceeds policy coverage limit.')).toBeTruthy()
    })
  })

  it('handles 401 session expiration error gracefully', async () => {
    const error = new Error('Unauthorized')
    error.status = 401
    payoutService.calculatePayout.mockRejectedValueOnce(error)

    render(<PayoutCalculation />)

    fireEvent.change(screen.getByPlaceholderText('Enter Claim ID (GUID)'), { target: { value: 'claim-123' } })
    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText('Your session has expired. Please log in again.')).toBeTruthy()
    })
  })

  it('handles 403 forbidden error gracefully', async () => {
    const error = new Error('Forbidden')
    error.status = 403
    payoutService.calculatePayout.mockRejectedValueOnce(error)

    render(<PayoutCalculation />)

    fireEvent.change(screen.getByPlaceholderText('Enter Claim ID (GUID)'), { target: { value: 'claim-123' } })
    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText('You do not have permission to calculate payouts.')).toBeTruthy()
    })
  })

  it('looks up existing payout when Look Up Existing is clicked', async () => {
    payoutService.getPayoutByClaim.mockResolvedValueOnce({
      id: 'existing-payout',
      claimId: 'claim-existing',
      claimNumber: 'CLM-EXISTING',
      status: 2,
      statusDisplay: 'Approved',
      approvedClaimAmount: 4000,
      coverageLimit: 15000,
      deductible: 500,
      proposedPayout: 3500,
      finalPayout: 3500,
    })

    render(<PayoutCalculation />)

    fireEvent.change(screen.getByPlaceholderText('Enter Claim ID (GUID)'), { target: { value: 'claim-existing' } })
    fireEvent.click(screen.getByRole('button', { name: 'Look Up Existing' }))

    await waitFor(() => {
      expect(payoutService.getPayoutByClaim).toHaveBeenCalledWith('claim-existing')
      expect(screen.getByText('Payout Proposal')).toBeTruthy()
      expect(screen.getByText('Claim: CLM-EXISTING')).toBeTruthy()
    })
  })

  // ── Integration Tests: Route Preloading & Duplicate Prevention ──

  it('9. preloads claimId from route searchParams', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-preloaded-123')
    render(<PayoutCalculation />)

    const input = screen.getByPlaceholderText('Enter Claim ID (GUID)')
    expect(input.value).toBe('claim-preloaded-123')
  })

  it('10. loads authoritative claim and policy context from backend data', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-context-123')
    claimService.getClaim.mockResolvedValueOnce({
      id: 'claim-context-123',
      claimNumber: 'CLM-20260922-0001',
      claimedAmount: 7500,
      claimType: 'Auto',
      status: 'Submitted',
      policyId: 'policy-guid-456',
    })
    policyService.getPolicyById.mockResolvedValueOnce({
      id: 'policy-guid-456',
      policyNumber: 'POL-AUTO-99',
      policyTypeName: 'Comprehensive Motor',
      coverageLimit: 50000,
      deductible: 500,
    })

    render(<PayoutCalculation />)

    await waitFor(() => {
      expect(screen.getByText('Authoritative Claim & Policy Context')).toBeTruthy()
      expect(screen.getByText('CLM-20260922-0001')).toBeTruthy()
      expect(screen.getByText('$7,500.00')).toBeTruthy()
      expect(screen.getByText('$50,000.00')).toBeTruthy()
      expect(screen.getByText('$500.00')).toBeTruthy()
      expect(screen.getByText('Auto')).toBeTruthy()
      expect(screen.getByText('Comprehensive Motor')).toBeTruthy()
    })
  })

  it('12. detects existing payout proposal, shows banner with Approval Desk link, and prevents duplicate creation', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-duplicate-test')
    payoutService.getPayoutByClaim.mockResolvedValueOnce({
      id: 'payout-existing-99',
      claimId: 'claim-duplicate-test',
      claimNumber: 'CLM-20260921-0002',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 8000,
      coverageLimit: 30000,
      deductible: 500,
      proposedPayout: 7500,
      finalPayout: 7500,
    })

    render(<PayoutCalculation />)

    await waitFor(() => {
      expect(screen.getByText(/Existing payout proposal found for this claim/)).toBeTruthy()
      expect(screen.getByText('Proposal Already Created')).toBeTruthy()
      expect(screen.getByText('Go to Approval Desk →')).toBeTruthy()
    })

    const calcBtn = screen.getByRole('button', { name: /Proposal Already Created/i })
    expect(calcBtn.disabled).toBe(true)
    expect(payoutService.calculatePayout).not.toHaveBeenCalled()
  })

  it('13. calculates and validates payout from preloaded claim', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-calculate-preloaded')
    payoutService.calculatePayout.mockResolvedValueOnce({
      id: 'payout-fresh-1',
      claimId: 'claim-calculate-preloaded',
      claimNumber: 'CLM-20260922-0001',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 10000,
      coverageLimit: 40000,
      deductible: 500,
      proposedPayout: 9500,
      finalPayout: 9500,
      validationResult: {
        valid: true,
        violations: [],
        requiresHumanApproval: true,
        aiUsed: true,
        reasoningSummary: 'Claimed repairs are consistent with accident report.',
        fallbackUsed: false,
      },
    })

    render(<PayoutCalculation />)

    const calcBtn = screen.getByRole('button', { name: 'Calculate Payout' })
    expect(calcBtn.disabled).toBe(false)
    fireEvent.click(calcBtn)

    await waitFor(() => {
      expect(payoutService.calculatePayout).toHaveBeenCalledWith('claim-calculate-preloaded')
      expect(screen.getByText('Payout proposal created successfully.')).toBeTruthy()
      expect(screen.getByText('Validation: ✓ Passed')).toBeTruthy()
      expect(screen.getByText('Claimed repairs are consistent with accident report.')).toBeTruthy()
      expect(screen.getByRole('link', { name: /Go to Approval Desk/i })).toBeTruthy()
    })
  })

  it('preloads claim with 404 from getPayoutByClaim as expected no payout without page error', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-404-test')
    const err404 = new Error('No payout found')
    err404.status = 404
    payoutService.getPayoutByClaim.mockRejectedValueOnce(err404)

    render(<PayoutCalculation />)

    await waitFor(() => {
      expect(screen.getByPlaceholderText('Enter Claim ID (GUID)').value).toBe('claim-404-test')
    })
    expect(screen.queryByText(/failed to check existing payout/i)).toBeNull()
    const calcBtn = screen.getByRole('button', { name: 'Calculate Payout' })
    expect(calcBtn.disabled).toBe(false)
  })

  it('preloads claim with 500 from getPayoutByClaim as real error', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-500-test')
    const err500 = new Error('Database query timeout')
    err500.status = 500
    payoutService.getPayoutByClaim.mockRejectedValueOnce(err500)

    render(<PayoutCalculation />)

    await waitFor(() => {
      expect(screen.getByText('Database query timeout')).toBeTruthy()
    })
  })

  it('displays blocked warning and disables calculation for Withdrawn claim', async () => {
    mockSearchParams = new URLSearchParams('claimId=claim-withdrawn-test')
    claimService.getClaim.mockResolvedValueOnce({
      id: 'claim-withdrawn-test',
      claimNumber: 'CLM-20260921-0001',
      claimedAmount: 5000,
      claimType: 'Auto',
      status: 'Withdrawn',
    })

    render(<PayoutCalculation />)

    await waitFor(() => {
      expect(screen.getByText(/has status "Withdrawn" and is not eligible for payout preparation/i)).toBeTruthy()
    })
    const calcBtn = screen.getByRole('button', { name: 'Calculate Payout' })
    expect(calcBtn.disabled).toBe(true)
  })

  it('handles backend 409 conflict response on calculate and loads existing payout', async () => {
    const err409 = new Error('A payout already exists for this claim.')
    err409.status = 409
    payoutService.calculatePayout.mockRejectedValueOnce(err409)
    payoutService.getPayoutByClaim.mockResolvedValueOnce({
      id: 'payout-409-loaded',
      claimId: 'claim-123',
      claimNumber: 'CLM-409',
      status: 1,
      statusDisplay: 'PendingApproval',
      approvedClaimAmount: 3500,
      coverageLimit: 10000,
      deductible: 500,
      proposedPayout: 3000,
      finalPayout: 3000,
    })

    render(<PayoutCalculation />)

    fireEvent.change(screen.getByPlaceholderText('Enter Claim ID (GUID)'), { target: { value: 'claim-123' } })
    fireEvent.click(screen.getByRole('button', { name: 'Calculate Payout' }))

    await waitFor(() => {
      expect(screen.getByText('A payout already exists for this claim.')).toBeTruthy()
      expect(screen.getByText('Payout Proposal')).toBeTruthy()
      expect(screen.getByText('Claim: CLM-409')).toBeTruthy()
    })
  })
})
