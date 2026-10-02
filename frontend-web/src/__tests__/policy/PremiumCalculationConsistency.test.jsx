import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import PolicyDetails from '../../pages/policy/PolicyDetails'
import * as policyService from '../../services/policyService'

// Mock AuthContext
vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ role: 'Admin', user: { role: 'Admin' } }),
}))

vi.mock('../../services/policyService', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    getPolicyById: vi.fn(),
    getCoverage: vi.fn(),
    calculatePremium: vi.fn(),
    renewPolicy: vi.fn(),
  }
})

/**
 * Premium Calculation Consistency Tests
 *
 * Verifies that PolicyDetails renders premium and deductible data
 * from the backend consistently. No client-side premium formula is used.
 */
describe('PolicyDetails Premium Rendering', () => {
  const basePolicyData = {
    id: 'test-policy-id',
    policyNumber: 'POL-MOTOR001',
    policyholderId: 'holder-1',
    policyTypeId: 'type-1',
    policyTypeName: 'Motor Insurance',
    coverageLimit: 100000,
    premium: 150000,
    deductible: 10000,
    deductiblePercentage: 5,
    startDate: '2026-01-01T00:00:00Z',
    expiryDate: '2027-01-01T00:00:00Z',
    status: 'Active',
    renewalStatus: 'NotDue',
    isExpired: false,
    canRenew: true,
  }

  beforeEach(() => {
    vi.clearAllMocks()
    policyService.getPolicyById.mockResolvedValue(basePolicyData)
    policyService.getCoverage.mockResolvedValue([])
  })

  it('renders backend calculatedPremium from premium calculation result', async () => {
    policyService.calculatePremium.mockResolvedValue({
      policyId: 'test-policy-id',
      policyNumber: 'POL-MOTOR001',
      basePremiumRate: 1500,
      coverageLimit: 100000,
      riskMultiplier: 1.0,
      deductibleDiscount: 0,
      calculatedPremium: 150000,
      breakdown: '(1500 × 100000 × 1.0000 / 1000) - 0 deductible discount = 150000.00',
    })

    render(
      <PolicyDetails
        policyId="test-policy-id"
        onBack={() => {}}
      />
    )

    await waitFor(() => {
      expect(screen.getByText(/POL-MOTOR001/)).toBeTruthy()
    })

    // Click "Calculate Premium" button
    const calcButton = screen.getByText('Calculate Premium')
    fireEvent.click(calcButton)

    await waitFor(() => {
      // Must show the backend's calculatedPremium
      expect(screen.getByText(/150,000/)).toBeTruthy()
    })

    // Must show the backend's breakdown string
    await waitFor(() => {
      expect(screen.getByText(/0 deductible discount/)).toBeTruthy()
    })
  })

  it('renders backend breakdown consistently with calculatedPremium', async () => {
    policyService.calculatePremium.mockResolvedValue({
      policyId: 'test-policy-id',
      policyNumber: 'POL-MOTOR001',
      basePremiumRate: 1500,
      coverageLimit: 100000,
      riskMultiplier: 1.0,
      deductibleDiscount: 0,
      calculatedPremium: 150000,
      breakdown: '(1500 × 100000 × 1.0000 / 1000) - 0 deductible discount = 150000.00',
    })

    render(
      <PolicyDetails
        policyId="test-policy-id"
        onBack={() => {}}
      />
    )

    await waitFor(() => {
      expect(screen.getByText(/POL-MOTOR001/)).toBeTruthy()
    })

    fireEvent.click(screen.getByText('Calculate Premium'))

    await waitFor(() => {
      // Verify breakdown does NOT show 500 deductible discount
      const breakdownEl = screen.getByText(/deductible discount/)
      expect(breakdownEl.textContent).toContain('0 deductible discount')
      expect(breakdownEl.textContent).not.toContain('500 deductible discount')
    })
  })

  it('displays percentage deductible as percentage, not as LKR premium discount', async () => {
    render(
      <PolicyDetails
        policyId="test-policy-id"
        onBack={() => {}}
      />
    )

    await waitFor(() => {
      expect(screen.getByText(/POL-MOTOR001/)).toBeTruthy()
    })

    // The deductible row should display "5%" (percentage), not "LKR 10,000"
    await waitFor(() => {
      expect(screen.getByText('5%')).toBeTruthy()
    })
  })

  it('displays fixed deductible as LKR for legacy policies without percentage', async () => {
    const legacyPolicy = {
      ...basePolicyData,
      policyTypeName: 'Motor Insurance (Legacy)',
      deductible: 10000,
      deductiblePercentage: null,
    }
    policyService.getPolicyById.mockResolvedValue(legacyPolicy)

    render(
      <PolicyDetails
        policyId="test-policy-id"
        onBack={() => {}}
      />
    )

    await waitFor(() => {
      // For legacy policies, deductible should show as LKR
      expect(screen.getByText(/LKR 10,000/)).toBeTruthy()
    })
  })
})
