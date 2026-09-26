import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import {
  getCompatibleClaimType,
  isPolicySupportedForClaims,
  normalizePolicyTypeName,
  CLAIM_TYPES,
} from '../../utils/policyClaimMapping'
import PolicyCreate from '../../pages/policy/PolicyCreate'
import ClaimCreate from '../../pages/claims/ClaimCreate'
import * as policyService from '../../services/policyService'
import * as claimService from '../../services/claimService'

// Mock react-router-dom
vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
}))

// Mock AuthContext
let mockAuth = {
  role: 'Policyholder',
  user: { userId: '11111111-1111-1111-1111-111111111111', firstName: 'John', lastName: 'Doe', email: 'john@example.com' },
}

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

vi.mock('../../services/policyService', () => ({
  getPolicyTypes: vi.fn(),
  getPolicies: vi.fn(),
  createPolicy: vi.fn(),
}))

vi.mock('../../services/claimService', () => ({
  createClaim: vi.fn(),
  submitClaim: vi.fn(),
}))

describe('PolicyClaimMapping Utility Tests', () => {
  it('correctly maps supported policy types to primary modern claim types', () => {
    expect(getCompatibleClaimType('Motor Insurance')).toEqual({ value: 8, label: 'Motor' })
    expect(getCompatibleClaimType('Health Insurance')).toEqual({ value: 2, label: 'Health' })
    expect(getCompatibleClaimType('Home Insurance')).toEqual({ value: 5, label: 'Property' })
    expect(getCompatibleClaimType('Home / Property Insurance')).toEqual({ value: 5, label: 'Property' })
    expect(getCompatibleClaimType('Life Insurance')).toEqual({ value: 3, label: 'Life' })
  })

  it('fails closed and returns null for unsupported or future products (never defaults to Other)', () => {
    expect(getCompatibleClaimType('Pension')).toBeNull()
    expect(getCompatibleClaimType('Annuity')).toBeNull()
    expect(getCompatibleClaimType('Investment-Linked')).toBeNull()
    expect(getCompatibleClaimType('Marine')).toBeNull()
    expect(getCompatibleClaimType('Travel Insurance')).toBeNull()
    expect(getCompatibleClaimType('Unknown Insurance')).toBeNull()
    expect(getCompatibleClaimType('')).toBeNull()
    expect(getCompatibleClaimType(null)).toBeNull()
  })

  it('correctly checks isPolicySupportedForClaims', () => {
    expect(isPolicySupportedForClaims('Motor Insurance')).toBe(true)
    expect(isPolicySupportedForClaims('Life Insurance')).toBe(true)
    expect(isPolicySupportedForClaims('Pension')).toBe(false)
  })

  it('includes Motor (8) and preserves historical Auto (0) and Home (1) in CLAIM_TYPES', () => {
    const motor = CLAIM_TYPES.find((c) => c.value === 8)
    const auto = CLAIM_TYPES.find((c) => c.value === 0)
    const home = CLAIM_TYPES.find((c) => c.value === 1)
    const prop = CLAIM_TYPES.find((c) => c.value === 5)
    const life = CLAIM_TYPES.find((c) => c.value === 3)

    expect(motor).toEqual({ value: 8, label: 'Motor' })
    expect(auto).toEqual({ value: 0, label: 'Auto' })
    expect(home).toEqual({ value: 1, label: 'Home' })
    expect(prop).toEqual({ value: 5, label: 'Property' })
    expect(life).toEqual({ value: 3, label: 'Life' })
  })
})

describe('PolicyCreate Insurance Class Grouping & Fixed Deductible Rule', () => {
  const mockPolicyTypes = [
    {
      id: '22222222-2222-4222-8222-222222222221',
      name: 'Motor Insurance',
      description: 'Motor vehicle cover',
      insuranceClass: 0,
      insuranceClassCode: 'General',
      insuranceClassName: 'General Insurance',
      defaultCoverageLimit: 500000,
      defaultDeductible: 10000,
    },
    {
      id: '22222222-2222-4222-8222-222222222222',
      name: 'Health Insurance',
      description: 'Medical expenses cover',
      insuranceClass: 0,
      insuranceClassCode: 'General',
      insuranceClassName: 'General Insurance',
      defaultCoverageLimit: 1000000,
      defaultDeductible: 5000,
    },
    {
      id: '22222222-2222-4222-8222-222222222223',
      name: 'Home Insurance',
      description: 'Property protection',
      insuranceClass: 0,
      insuranceClassCode: 'General',
      insuranceClassName: 'General Insurance',
      defaultCoverageLimit: 750000,
      defaultDeductible: 15000,
    },
    {
      id: '22222222-2222-4222-8222-222222222224',
      name: 'Life Insurance',
      description: 'Term life coverage',
      insuranceClass: 1,
      insuranceClassCode: 'LongTerm',
      insuranceClassName: 'Long-Term Insurance',
      defaultCoverageLimit: 2000000,
      defaultDeductible: 0,
    },
  ]

  beforeEach(() => {
    vi.clearAllMocks()
    policyService.getPolicyTypes.mockResolvedValue(mockPolicyTypes)
  })

  it('renders policy types grouped by General and Long-Term optgroups using insuranceClassCode', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const generalOptGroup = screen.getByRole('group', { name: 'General Insurance' })
    const longTermOptGroup = screen.getByRole('group', { name: 'Long-Term Insurance' })

    expect(generalOptGroup).toBeInTheDocument()
    expect(longTermOptGroup).toBeInTheDocument()

    // Home Insurance displays alias Home / Property Insurance
    expect(screen.getByText(/Home \/ Property Insurance/)).toBeInTheDocument()
    expect(screen.getByText(/Life Insurance/)).toBeInTheDocument()
  })

  it('enforces Life deductible = $0 and shows project rule note when Life Insurance selected', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const select = screen.getByRole('combobox')
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222224' } })

    // Life Insurance project rule message displayed
    expect(screen.getByText('Project rule: Life Insurance deductible is $0.')).toBeInTheDocument()
    expect(screen.getByText(/The deductible is fixed according to your selected insurance type/)).toBeInTheDocument()

    // Deductible input is disabled, read-only, and displays $0
    const deductibleInput = screen.getByPlaceholderText('1000')
    expect(deductibleInput).toBeDisabled()
    expect(deductibleInput).toHaveAttribute('readonly')
    expect(deductibleInput.value).toBe('$0')
  })

  it('automatically displays each fixed deductible ($10,000 for Motor, $5,000 for Health, $15,000 for Home) in read-only field', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const select = screen.getByRole('combobox')
    const deductibleInput = screen.getByPlaceholderText('1000')

    // Select Motor Insurance
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222221' } })
    expect(deductibleInput.value).toBe('$10,000')
    expect(deductibleInput).toHaveAttribute('readonly')
    expect(deductibleInput).toBeDisabled()

    // Select Health Insurance
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222222' } })
    expect(deductibleInput.value).toBe('$5,000')
    expect(deductibleInput).toHaveAttribute('readonly')
    expect(deductibleInput).toBeDisabled()

    // Select Home Insurance
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222223' } })
    expect(deductibleInput.value).toBe('$15,000')
    expect(deductibleInput).toHaveAttribute('readonly')
    expect(deductibleInput).toBeDisabled()

    // Select Life Insurance
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222224' } })
    expect(deductibleInput.value).toBe('$0')
    expect(deductibleInput).toHaveAttribute('readonly')
    expect(deductibleInput).toBeDisabled()
  })

  it('displays backend-confirmed deductible upon successful creation', async () => {
    policyService.createPolicy.mockResolvedValueOnce({
      id: 'pol-123',
      policyNumber: 'POL-MTR-999',
      deductible: 10000,
      coverageLimit: 500000,
    })

    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const select = screen.getByRole('combobox')
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222221' } })

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)
    fireEvent.change(startDateInput, { target: { value: '2026-10-01' } })
    fireEvent.change(expiryDateInput, { target: { value: '2027-10-01' } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByText(/Confirmed Deductible: \$10,000/i)).toBeInTheDocument()
    })
  })
})

describe('ClaimCreate Compatibility & Safety Tests', () => {
  const activePolicies = [
    {
      id: 'pol-motor-1',
      policyNumber: 'POL-MTR-001',
      policyTypeName: 'Motor Insurance',
      coverageLimit: 500000,
      status: 'Active',
    },
    {
      id: 'pol-life-1',
      policyNumber: 'POL-LIF-001',
      policyTypeName: 'Life Insurance',
      coverageLimit: 2000000,
      status: 'Active',
    },
    {
      id: 'pol-unsupported-1',
      policyNumber: 'POL-PNS-001',
      policyTypeName: 'Pension',
      coverageLimit: 1000000,
      status: 'Active',
    },
  ]

  beforeEach(() => {
    vi.clearAllMocks()
    policyService.getPolicies.mockResolvedValue(activePolicies)
  })

  it('auto-selects Motor (8) when Motor Insurance policy is selected', async () => {
    render(<ClaimCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText(/POL-MTR-001/)).toBeInTheDocument()
    })

    const policySelect = screen.getByLabelText(/Policy \*/)
    fireEvent.change(policySelect, { target: { value: 'pol-motor-1' } })

    const claimTypeSelect = screen.getByLabelText(/Claim Type \*/)
    expect(claimTypeSelect.value).toBe('8')
    expect(screen.getByRole('option', { name: 'Motor' })).toBeInTheDocument()
  })

  it('auto-selects Life (3) when Life Insurance policy is selected', async () => {
    render(<ClaimCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText(/POL-LIF-001/)).toBeInTheDocument()
    })

    const policySelect = screen.getByLabelText(/Policy \*/)
    fireEvent.change(policySelect, { target: { value: 'pol-life-1' } })

    const claimTypeSelect = screen.getByLabelText(/Claim Type \*/)
    expect(claimTypeSelect.value).toBe('3')
    expect(screen.getByRole('option', { name: 'Life' })).toBeInTheDocument()
  })

  it('blocks claim submission and shows error message when unsupported policy is selected', async () => {
    render(<ClaimCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText(/POL-PNS-001/)).toBeInTheDocument()
    })

    const policySelect = screen.getByLabelText(/Policy \*/)
    fireEvent.change(policySelect, { target: { value: 'pol-unsupported-1' } })

    expect(
      screen.getByText('This policy type is not currently supported for claim creation.')
    ).toBeInTheDocument()

    const submitBtn = screen.getByRole('button', { name: /Create Claim/ })
    expect(submitBtn).toBeDisabled()
  })
})
