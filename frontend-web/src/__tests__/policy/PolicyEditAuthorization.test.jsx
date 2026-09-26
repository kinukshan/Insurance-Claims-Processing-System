import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import PolicyDetails from '../../pages/policy/PolicyDetails'
import PolicyEdit from '../../pages/policy/PolicyEdit'
import PolicyCreate from '../../pages/policy/PolicyCreate'
import * as policyService from '../../services/policyService'

let mockAuth = { role: null, user: null }

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

vi.mock('../../services/policyService', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    getPolicyById: vi.fn(),
    getCoverage: vi.fn(),
    updatePolicy: vi.fn(),
    getPolicyTypes: vi.fn(),
    createPolicy: vi.fn(),
  }
})

describe('Role-Based Policy Editing Authorization', () => {
  const mockPolicy = {
    id: 'pol-123',
    policyNumber: 'POL-TEST-001',
    policyholderId: 'usr-holder-1',
    policyTypeId: 'type-1',
    policyTypeName: 'Comprehensive Auto',
    coverageLimit: 50000,
    premium: 600,
    deductible: 500,
    startDate: '2026-01-01T00:00:00Z',
    expiryDate: '2027-01-01T00:00:00Z',
    status: 'Active',
    renewalStatus: 'NotDue',
    exclusions: 'No track racing',
    isExpired: false,
    canRenew: true,
  }

  beforeEach(() => {
    vi.clearAllMocks()
    mockAuth = { role: null, user: null }
    policyService.getPolicyById.mockResolvedValue({ ...mockPolicy })
    policyService.getCoverage.mockResolvedValue([])
    policyService.getPolicyTypes.mockResolvedValue([
      { id: 'type-1', name: 'Comprehensive Auto', defaultCoverageLimit: 50000, defaultDeductible: 10000 }
    ])
  })

  // 1. Policyholder & ClaimsAdjuster cannot see Edit button in PolicyDetails
  describe('PolicyDetails Edit button visibility', () => {
    it('does not display Edit button for Policyholder', async () => {
      mockAuth = { role: 'Policyholder', user: { userId: 'usr-holder-1', role: 'Policyholder' } }
      const onEdit = vi.fn()

      render(<PolicyDetails policyId="pol-123" onBack={vi.fn()} onEdit={onEdit} />)

      await waitFor(() => {
        expect(screen.getByText('Policy POL-TEST-001')).toBeDefined()
      })

      expect(screen.queryByRole('button', { name: /^edit$/i })).toBeNull()
    })

    it('does not display Edit button for ClaimsAdjuster', async () => {
      mockAuth = { role: 'ClaimsAdjuster', user: { userId: 'usr-adj-1', role: 'ClaimsAdjuster' } }
      const onEdit = vi.fn()

      render(<PolicyDetails policyId="pol-123" onBack={vi.fn()} onEdit={onEdit} />)

      await waitFor(() => {
        expect(screen.getByText('Policy POL-TEST-001')).toBeDefined()
      })

      expect(screen.queryByRole('button', { name: /^edit$/i })).toBeNull()
    })

    it('displays Edit button for Underwriter', async () => {
      mockAuth = { role: 'Underwriter', user: { userId: 'usr-uw-1', role: 'Underwriter' } }
      const onEdit = vi.fn()

      render(<PolicyDetails policyId="pol-123" onBack={vi.fn()} onEdit={onEdit} />)

      await waitFor(() => {
        expect(screen.getByText('Policy POL-TEST-001')).toBeDefined()
      })

      expect(screen.getByRole('button', { name: /^edit$/i })).toBeDefined()
    })

    it('displays Edit button for Admin', async () => {
      mockAuth = { role: 'Admin', user: { userId: 'usr-adm-1', role: 'Admin' } }
      const onEdit = vi.fn()

      render(<PolicyDetails policyId="pol-123" onBack={vi.fn()} onEdit={onEdit} />)

      await waitFor(() => {
        expect(screen.getByText('Policy POL-TEST-001')).toBeDefined()
      })

      expect(screen.getByRole('button', { name: /^edit$/i })).toBeDefined()
    })
  })

  // 2. Access Denied on PolicyEdit for Policyholder and ClaimsAdjuster
  describe('PolicyEdit page access control', () => {
    it('shows Access Denied and no Update Policy button for Policyholder', () => {
      mockAuth = { role: 'Policyholder', user: { userId: 'usr-holder-1', role: 'Policyholder' } }

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      expect(screen.getByRole('heading', { name: 'Access Denied' })).toBeDefined()
      expect(screen.getByText(/you do not have permission to edit policies/i)).toBeDefined()
      expect(screen.queryByRole('button', { name: /update policy/i })).toBeNull()
    })

    it('shows Access Denied and no Update Policy button for ClaimsAdjuster', () => {
      mockAuth = { role: 'ClaimsAdjuster', user: { userId: 'usr-adj-1', role: 'ClaimsAdjuster' } }

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      expect(screen.getByRole('heading', { name: 'Access Denied' })).toBeDefined()
      expect(screen.getByText(/you do not have permission to edit policies/i)).toBeDefined()
      expect(screen.queryByRole('button', { name: /update policy/i })).toBeNull()
    })
  })

  // 3. Underwriter: permitted editable fields, read-only status and deductible
  describe('Underwriter policy editing capabilities', () => {
    it('allows Underwriter to edit coverage limit, expiry date, and exclusions, but status and deductible are read-only', async () => {
      mockAuth = { role: 'Underwriter', user: { userId: 'usr-uw-1', role: 'Underwriter' } }

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      await waitFor(() => {
        expect(screen.getByText('Edit Policy POL-TEST-001')).toBeDefined()
      })

      // Coverage Limit is editable
      const coverageInput = screen.getByLabelText('Coverage Limit')
      expect(coverageInput.disabled).toBe(false)
      expect(coverageInput.readOnly).toBe(false)

      // Expiry Date is editable
      const expiryInput = screen.getByLabelText('Expiry Date')
      expect(expiryInput.disabled).toBe(false)

      // Exclusions is editable
      const exclusionsInput = screen.getByLabelText('Exclusions')
      expect(exclusionsInput.disabled).toBe(false)

      // Deductible is read-only and disabled
      const deductibleInput = screen.getByLabelText('Deductible')
      expect(deductibleInput.readOnly).toBe(true)
      expect(deductibleInput.disabled).toBe(true)

      // Status is read-only and disabled (not a select control)
      const statusInput = screen.getByLabelText('Status')
      expect(statusInput.tagName.toLowerCase()).toBe('input')
      expect(statusInput.readOnly).toBe(true)
      expect(statusInput.disabled).toBe(true)
      expect(screen.queryByRole('combobox')).toBeNull()

      // Update Policy button is displayed
      expect(screen.getByRole('button', { name: /update policy/i })).toBeDefined()
    })

    it('submitting as Underwriter sends only permitted fields without status or deductible', async () => {
      mockAuth = { role: 'Underwriter', user: { userId: 'usr-uw-1', role: 'Underwriter' } }
      policyService.updatePolicy.mockResolvedValueOnce({ ...mockPolicy, coverageLimit: 60000 })

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      await waitFor(() => {
        expect(screen.getByText('Edit Policy POL-TEST-001')).toBeDefined()
      })

      fireEvent.change(screen.getByLabelText('Coverage Limit'), { target: { value: '60000' } })
      fireEvent.click(screen.getByRole('button', { name: /update policy/i }))

      await waitFor(() => {
        expect(policyService.updatePolicy).toHaveBeenCalledTimes(1)
      })

      const [calledId, calledPayload] = policyService.updatePolicy.mock.calls[0]
      expect(calledId).toBe('pol-123')
      expect(calledPayload.coverageLimit).toBe(60000)
      expect(calledPayload.status).toBeUndefined()
      expect(calledPayload.deductible).toBeUndefined()
    })
  })

  // 4. Admin: permitted editable fields, status select control, confirmation on status change
  describe('Admin policy editing capabilities', () => {
    it('displays status select control for Admin, while deductible remains read-only', async () => {
      mockAuth = { role: 'Admin', user: { userId: 'usr-adm-1', role: 'Admin' } }

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      await waitFor(() => {
        expect(screen.getByText('Edit Policy POL-TEST-001')).toBeDefined()
      })

      // Status is a select control
      const statusSelect = screen.getByLabelText('Status')
      expect(statusSelect.tagName.toLowerCase()).toBe('select')

      // Deductible remains read-only
      const deductibleInput = screen.getByLabelText('Deductible')
      expect(deductibleInput.readOnly).toBe(true)
      expect(deductibleInput.disabled).toBe(true)
    })

    it('requires confirmation when Admin changes policy status and submits status in payload', async () => {
      mockAuth = { role: 'Admin', user: { userId: 'usr-adm-1', role: 'Admin' } }
      policyService.updatePolicy.mockResolvedValueOnce({ ...mockPolicy, status: 'Cancelled' })
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true)

      render(<PolicyEdit policyId="pol-123" onBack={vi.fn()} onUpdated={vi.fn()} />)

      await waitFor(() => {
        expect(screen.getByText('Edit Policy POL-TEST-001')).toBeDefined()
      })

      // Change status to Cancelled
      fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Cancelled' } })
      fireEvent.click(screen.getByRole('button', { name: /update policy/i }))

      expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Confirm Status Change'))

      await waitFor(() => {
        expect(policyService.updatePolicy).toHaveBeenCalledTimes(1)
      })

      const [calledId, calledPayload] = policyService.updatePolicy.mock.calls[0]
      expect(calledId).toBe('pol-123')
      expect(calledPayload.status).toBe('Cancelled')
      expect(calledPayload.deductible).toBeUndefined()

      confirmSpy.mockRestore()
    })
  })

  // 5. Existing Create Policy functionality continues to work
  describe('PolicyCreate workflow preservation', () => {
    it('allows Policyholder to access and view Create Policy form', async () => {
      mockAuth = { role: 'Policyholder', user: { userId: 'usr-holder-1', role: 'Policyholder' } }

      render(<PolicyCreate onBack={vi.fn()} onCreated={vi.fn()} />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Create Policy' })).toBeDefined()
      })

      expect(screen.getByRole('button', { name: /create policy/i })).toBeDefined()
    })
  })
})
