import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import PolicyCreate from '../../pages/policy/PolicyCreate'
import * as policyService from '../../services/policyService'
import { formatLocalDate, addDaysToLocalDate, addYearsToLocalDate } from '../../utils/dateUtils'

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
  createPolicy: vi.fn(),
}))

const samplePolicyTypes = [
  {
    id: '22222222-2222-4222-8222-222222222221',
    name: 'Motor Insurance',
    insuranceClassCode: 'General',
    defaultCoverageLimit: 500000,
    defaultDeductible: 10000,
    defaultDeductiblePercentage: 5,
  },
]

describe('PolicyCreate Date Validation & Default Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    policyService.getPolicyTypes.mockResolvedValue(samplePolicyTypes)
  })

  // 1. Create Policy defaults Start Date to today's local calendar date.
  it('1. defaults Start Date to today local calendar date and Expiry Date to +1 year', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    const todayStr = formatLocalDate(new Date())
    const expectedExpiryStr = addYearsToLocalDate(todayStr, 1)

    expect(startDateInput.value).toBe(todayStr)
    expect(startDateInput.getAttribute('min')).toBe(todayStr)
    expect(expiryDateInput.value).toBe(expectedExpiryStr)
    expect(expiryDateInput.getAttribute('min')).toBe(addDaysToLocalDate(todayStr, 1))
  })

  // 2. Start Date cannot be earlier than today.
  it('2. rejects Start Date earlier than today with clear error message', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const pastDate = '2020-01-01'
    fireEvent.change(startDateInput, { target: { value: pastDate } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByText('Start date cannot be before today.')).toBeInTheDocument()
    })
    expect(policyService.createPolicy).not.toHaveBeenCalled()
  })

  // 3. Expiry Date cannot be before Start Date.
  it('3. rejects Expiry Date before Start Date with clear error message', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const todayStr = formatLocalDate(new Date())
    const futureStart = addDaysToLocalDate(todayStr, 10)
    const earlierExpiry = addDaysToLocalDate(todayStr, 5)

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    fireEvent.change(startDateInput, { target: { value: futureStart } })
    fireEvent.change(expiryDateInput, { target: { value: earlierExpiry } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByText('Expiry date must be later than the start date.')).toBeInTheDocument()
    })
    expect(policyService.createPolicy).not.toHaveBeenCalled()
  })

  // 4. Expiry Date cannot equal Start Date.
  it('4. rejects Expiry Date when equal to Start Date', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const todayStr = formatLocalDate(new Date())
    const futureDate = addDaysToLocalDate(todayStr, 15)

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    fireEvent.change(startDateInput, { target: { value: futureDate } })
    fireEvent.change(expiryDateInput, { target: { value: futureDate } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByText('Expiry date must be later than the start date.')).toBeInTheDocument()
    })
    expect(policyService.createPolicy).not.toHaveBeenCalled()
  })

  // 5. Expiry Date after Start Date is accepted.
  it('5. accepts valid Start Date and future Expiry Date upon submission', async () => {
    policyService.createPolicy.mockResolvedValueOnce({
      id: 'pol-created-1',
      policyNumber: 'POL-TEST-001',
      deductiblePercentage: 5,
      deductible: 10000,
    })

    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    // Select policy type
    const select = screen.getByRole('combobox')
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222221' } })

    const todayStr = formatLocalDate(new Date())
    const futureStart = addDaysToLocalDate(todayStr, 2)
    const futureExpiry = addYearsToLocalDate(futureStart, 1)

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    fireEvent.change(startDateInput, { target: { value: futureStart } })
    fireEvent.change(expiryDateInput, { target: { value: futureExpiry } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(policyService.createPolicy).toHaveBeenCalledWith(
        expect.objectContaining({
          startDate: futureStart,
          expiryDate: futureExpiry,
        })
      )
    })
  })

  // 6. Changing Start Date revalidates Expiry Date.
  it('6. automatically advances Expiry Date when Start Date changes to or past existing Expiry Date', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    const todayStr = formatLocalDate(new Date())
    const futureStart = addYearsToLocalDate(todayStr, 2)
    const expectedExpiry = addYearsToLocalDate(futureStart, 1)

    // Advance startDate beyond the default expiry date (which was today + 1 year)
    fireEvent.change(startDateInput, { target: { value: futureStart } })

    // ExpiryDate should automatically advance to futureStart + 1 year
    expect(expiryDateInput.value).toBe(expectedExpiry)
    expect(expiryDateInput.getAttribute('min')).toBe(addDaysToLocalDate(futureStart, 1))
  })

  // 7. A crafted invalid submission is not sent from normal UI.
  it('7. crafted invalid submission (past startDate) is blocked before network dispatch', async () => {
    render(<PolicyCreate onBack={() => {}} onCreated={() => {}} />)

    await waitFor(() => {
      expect(screen.getByText('Select a policy type...')).toBeInTheDocument()
    })

    // Fill valid type
    const select = screen.getByRole('combobox')
    fireEvent.change(select, { target: { value: '22222222-2222-4222-8222-222222222221' } })

    const startDateInput = screen.getByLabelText(/Start Date/i)
    const expiryDateInput = screen.getByLabelText(/Expiry Date/i)

    // Crafted past date
    fireEvent.change(startDateInput, { target: { value: '2026-01-01' } })
    fireEvent.change(expiryDateInput, { target: { value: '2026-06-01' } })

    const submitBtn = screen.getByRole('button', { name: /Create Policy/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByText('Start date cannot be before today.')).toBeInTheDocument()
    })
    expect(policyService.createPolicy).not.toHaveBeenCalled()
  })
})
