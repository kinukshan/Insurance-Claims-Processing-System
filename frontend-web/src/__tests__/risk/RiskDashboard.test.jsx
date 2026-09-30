// Risk component tests — Component C (Member 3)
// Tests for RiskDashboard, FlaggedClaims, and risk components

import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import RiskDashboard from '../../pages/risk/RiskDashboard'
import FlaggedClaims from '../../pages/risk/FlaggedClaims'
import RiskAssessmentDetail from '../../pages/risk/RiskAssessmentDetail'
import RiskScoreBadge from '../../components/risk/RiskScoreBadge'
import FraudFlagList from '../../components/risk/FraudFlagList'

// ── Mock riskService ─────────────────────────────────────────

vi.mock('../../services/riskService', () => ({
  getAllAssessments: vi.fn(),
  getFlaggedClaims: vi.fn(),
  getAssessment: vi.fn(),
  getFlags: vi.fn(),
  assessClaim: vi.fn(),
  escalateClaim: vi.fn(),
  getFraudHistory: vi.fn(),
  updateFraudCase: vi.fn(),
  getPolicyholderStatus: vi.fn(),
}))

import {
  getAllAssessments,
  getFlaggedClaims,
  escalateClaim,
  getAssessment,
  getFlags,
} from '../../services/riskService'

// ── Sample test data ─────────────────────────────────────────

const sampleAssessments = [
  {
    id: 'a1a1a1a1-0000-0000-0000-000000000001',
    claimId: 'c1c1c1c1-0000-0000-0000-000000000001',
    riskScore: 75.5,
    riskLevelDisplay: 'High',
    recommendationDisplay: 'Escalate',
    assessorType: 1,
    assessmentTimestamp: '2026-09-10T10:00:00Z',
    summary: 'Risk Score: 75.5/100 | Level: High',
    fraudFlagCount: 3,
    hasFraudCase: false,
    createdAt: '2026-09-10T10:00:00Z',
    updatedAt: '2026-09-10T10:00:00Z',
  },
  {
    id: 'a2a2a2a2-0000-0000-0000-000000000002',
    claimId: 'c2c2c2c2-0000-0000-0000-000000000002',
    riskScore: 22.0,
    riskLevelDisplay: 'Low',
    recommendationDisplay: 'Proceed',
    assessorType: 0,
    assessmentTimestamp: '2026-09-11T14:30:00Z',
    summary: 'Risk Score: 22.0/100 | Level: Low',
    fraudFlagCount: 0,
    hasFraudCase: false,
    createdAt: '2026-09-11T14:30:00Z',
    updatedAt: '2026-09-11T14:30:00Z',
  },
]

const sampleFlags = [
  {
    id: 'f1',
    flagTypeDisplay: 'HighAmount',
    description: 'Claim amount exceeds threshold',
    severityDisplay: 'High',
    sourceDisplay: 'Rule',
    isResolved: false,
  },
  {
    id: 'f2',
    flagTypeDisplay: 'DuplicateClaim',
    description: 'Potential duplicate found',
    severityDisplay: 'Critical',
    sourceDisplay: 'Rule',
    isResolved: true,
  },
]

// ══════════════════════════════════════════════════════════════
// RiskDashboard Tests
// ══════════════════════════════════════════════════════════════

describe('RiskDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders loading state initially', () => {
    getAllAssessments.mockReturnValue(new Promise(() => {})) // never resolves
    render(<RiskDashboard />)
    expect(screen.getByText('Risk Assessment Dashboard')).toBeInTheDocument()
    expect(document.querySelector('.spinner')).toBeInTheDocument()
  })

  it('renders dashboard with data', async () => {
    getAllAssessments.mockResolvedValue(sampleAssessments)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Risk Assessment Dashboard')).toBeInTheDocument()
    })

    // Summary cards
    expect(screen.getByText('Total Assessments')).toBeInTheDocument()
    expect(screen.getByText('Flagged Claims')).toBeInTheDocument()
    expect(screen.getByText('Open Fraud Cases')).toBeInTheDocument()
    expect(screen.getByText('Avg Risk Score')).toBeInTheDocument()

    // Data values
    expect(screen.getByText('2')).toBeInTheDocument() // total
  })

  it('renders error state with retry button', async () => {
    getAllAssessments.mockRejectedValue(new Error('Network failure'))
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Network failure')).toBeInTheDocument()
    })

    expect(screen.getByText('Retry')).toBeInTheDocument()
  })

  it('renders empty state when no assessments', async () => {
    getAllAssessments.mockResolvedValue([])
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('No risk assessments recorded yet.')).toBeInTheDocument()
    })
  })

  it('search filter works', async () => {
    getAllAssessments.mockResolvedValue(sampleAssessments)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Total Assessments')).toBeInTheDocument()
    })

    const searchInput = screen.getByPlaceholderText('Search by Claim ID...')
    fireEvent.change(searchInput, { target: { value: 'c1c1c1c1' } })

    // Should show only the matching row
    const rows = document.querySelectorAll('.risk-table tbody tr')
    expect(rows.length).toBe(1)
  })

  it('search filter works by claimNumber', async () => {
    const assessmentsWithNumber = [
      { ...sampleAssessments[0], claimNumber: 'CLM-20260921-0001' },
      { ...sampleAssessments[1], claimNumber: 'CLM-20260921-0002' },
    ]
    getAllAssessments.mockResolvedValue(assessmentsWithNumber)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('CLM-20260921-0001')).toBeInTheDocument()
    })

    const searchInput = screen.getByPlaceholderText('Search by Claim ID...')
    fireEvent.change(searchInput, { target: { value: '0002' } })

    const rows = document.querySelectorAll('.risk-table tbody tr')
    expect(rows.length).toBe(1)
    expect(screen.getByText('CLM-20260921-0002')).toBeInTheDocument()
  })

  it('clear button resets search', async () => {
    getAllAssessments.mockResolvedValue(sampleAssessments)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Total Assessments')).toBeInTheDocument()
    })

    const searchInput = screen.getByPlaceholderText('Search by Claim ID...')
    fireEvent.change(searchInput, { target: { value: 'nonexistent' } })

    // Click clear
    fireEvent.click(screen.getByText('Clear'))

    const rows = document.querySelectorAll('.risk-table tbody tr')
    expect(rows.length).toBe(2)
  })

  it('retry button reloads data after error', async () => {
    getAllAssessments.mockRejectedValueOnce(new Error('Server error'))
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Server error')).toBeInTheDocument()
    })

    // Now mock success
    getAllAssessments.mockResolvedValue(sampleAssessments)
    fireEvent.click(screen.getByText('Retry'))

    await waitFor(() => {
      expect(screen.getByText('Total Assessments')).toBeInTheDocument()
    })
  })

  it('loads and displays high-risk Escalate assessment with flags and metrics without crashing', async () => {
    const highRiskEscalate = [
      {
        id: '9b2cbfb7-64fa-4d97-a766-4c4a12e57398',
        claimId: 'ef39e0fd-6213-450e-b8a4-509e4000fd78',
        claimNumber: 'CLM-20260925-0004',
        riskScore: 80.0,
        riskLevelDisplay: 'Critical',
        recommendationDisplay: 'Escalate',
        assessorType: 1,
        assessmentTimestamp: '2026-09-25T11:09:52Z',
        summary: 'Risk Score: 80.0/100 | Level: Critical | Recommendation: Escalate | Flags: 3',
        fraudFlagCount: 3,
        hasFraudCase: false,
        createdAt: '2026-09-25T11:09:52Z',
        updatedAt: '2026-09-25T11:09:52Z',
      },
    ]
    getAllAssessments.mockResolvedValue(highRiskEscalate)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Risk Assessment Dashboard')).toBeInTheDocument()
    })

    expect(screen.getByText('CLM-20260925-0004')).toBeInTheDocument()
    expect(screen.getAllByText('Critical').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Escalate')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('AI')).toBeInTheDocument()
  })

  it('displays safe error and Retry button when API returns validation error dictionary without JS crash', async () => {
    const error = new Error('The Priority field is required.; The request field is required.')
    error.status = 400
    error.body = {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        Priority: ['The Priority field is required.'],
        request: ['The request field is required.'],
      },
    }
    getAllAssessments.mockRejectedValue(error)
    render(<RiskDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/The Priority field is required/)).toBeInTheDocument()
    })

    expect(screen.getByText('Retry')).toBeInTheDocument()
    expect(document.querySelector('.state-message.error')).toBeInTheDocument()
  })
})

// ══════════════════════════════════════════════════════════════
// FlaggedClaims Tests
// ══════════════════════════════════════════════════════════════

describe('FlaggedClaims', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders loading state', () => {
    getFlaggedClaims.mockReturnValue(new Promise(() => {}))
    render(<FlaggedClaims />)
    expect(screen.getByText('Flagged Claims')).toBeInTheDocument()
    expect(document.querySelector('.spinner')).toBeInTheDocument()
  })

  it('renders flagged claims table', async () => {
    getFlaggedClaims.mockResolvedValue(sampleAssessments)
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(screen.getByText('Flagged Claims')).toBeInTheDocument()
    })

    // Table headers
    expect(screen.getByText('Claim ID')).toBeInTheDocument()
    expect(screen.getByText('Risk Score')).toBeInTheDocument()
    expect(screen.getByText('Flags')).toBeInTheDocument()
  })

  it('renders empty state when no flagged claims', async () => {
    getFlaggedClaims.mockResolvedValue([])
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(screen.getByText('No flagged claims. All clear!')).toBeInTheDocument()
    })
  })

  it('severity filter works', async () => {
    getFlaggedClaims.mockResolvedValue(sampleAssessments)
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(document.querySelector('#flagged-claims-table')).toBeInTheDocument()
    })

    const severitySelect = document.querySelector('#filter-severity')
    fireEvent.change(severitySelect, { target: { value: 'Low' } })

    // Only the Low risk assessment should show
    const rows = document.querySelectorAll('#flagged-claims-table tbody tr')
    expect(rows.length).toBe(1)
  })

  it('shows escalate button for non-fraud-case assessments', async () => {
    getFlaggedClaims.mockResolvedValue(sampleAssessments)
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(document.querySelector('#flagged-claims-table')).toBeInTheDocument()
    })

    const escalateButtons = screen.getAllByRole('button', { name: 'Escalate' })
    expect(escalateButtons.length).toBe(2) // both have hasFraudCase: false
  })

  it('renders error state', async () => {
    getFlaggedClaims.mockRejectedValue(new Error('API down'))
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(screen.getByText('API down')).toBeInTheDocument()
    })
  })

  it('renders high-risk Escalate assessment in flagged claims table', async () => {
    const highRiskClaim = [
      {
        id: '9b2cbfb7-64fa-4d97-a766-4c4a12e57398',
        claimId: 'ef39e0fd-6213-450e-b8a4-509e4000fd78',
        riskScore: 80.0,
        riskLevelDisplay: 'Critical',
        recommendationDisplay: 'Escalate',
        fraudFlagCount: 3,
        hasFraudCase: false,
        assessmentTimestamp: '2026-09-25T11:09:52Z',
      },
    ]
    getFlaggedClaims.mockResolvedValue(highRiskClaim)
    render(<FlaggedClaims />)

    await waitFor(() => {
      expect(screen.getAllByText('Critical').length).toBeGreaterThanOrEqual(1)
    })

    expect(screen.getAllByText('Escalate').length).toBe(2)
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Escalate' })).toBeInTheDocument()
  })
})

// ══════════════════════════════════════════════════════════════
// RiskAssessmentDetail Tests
// ══════════════════════════════════════════════════════════════

describe('RiskAssessmentDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders high-risk Escalate assessment with flags, score, and level', async () => {
    const highRiskDetail = {
      id: '9b2cbfb7-64fa-4d97-a766-4c4a12e57398',
      claimId: 'ef39e0fd-6213-450e-b8a4-509e4000fd78',
      riskScore: 80.0,
      riskLevelDisplay: 'Critical',
      recommendationDisplay: 'Escalate',
      assessorType: 1,
      assessmentTimestamp: '2026-09-25T11:09:52Z',
      summary: 'Risk Score: 80.0/100 | Level: Critical | Recommendation: Escalate | Flags: 3',
      hasFraudCase: false,
    }

    getAssessment.mockResolvedValue(highRiskDetail)
    getFlags.mockResolvedValue(sampleFlags)

    render(<RiskAssessmentDetail claimId="ef39e0fd-6213-450e-b8a4-509e4000fd78" />)

    await waitFor(() => {
      expect(screen.getByText('Risk Assessment Detail')).toBeInTheDocument()
    })

    expect(screen.getAllByText('Critical').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Escalate')).toBeInTheDocument()
    expect(screen.getByText('AI Agent')).toBeInTheDocument()
    expect(screen.getByText('Fraud Flags (2)')).toBeInTheDocument()
    expect(screen.getByText('Escalate to Fraud Case')).toBeInTheDocument()
  })

  it('renders safe error state and Retry button on API failure', async () => {
    getAssessment.mockRejectedValue(new Error('Assessment retrieval failed.'))
    getFlags.mockResolvedValue([])

    render(<RiskAssessmentDetail claimId="ef39e0fd-6213-450e-b8a4-509e4000fd78" />)

    await waitFor(() => {
      expect(screen.getByText('Assessment retrieval failed.')).toBeInTheDocument()
    })

    expect(screen.getByText('Retry')).toBeInTheDocument()
  })
})

// ══════════════════════════════════════════════════════════════
// RiskScoreBadge Tests
// ══════════════════════════════════════════════════════════════

describe('RiskScoreBadge', () => {
  it('renders score with correct level class', () => {
    const { container } = render(<RiskScoreBadge score={75.5} level="High" />)
    const badge = container.querySelector('.risk-score-badge')
    expect(badge).toBeInTheDocument()
    expect(badge.classList.contains('high')).toBe(true)
  })

  it('displays score text', () => {
    render(<RiskScoreBadge score={22.0} level="Low" />)
    expect(screen.getByText('22')).toBeInTheDocument()
  })
})

// ══════════════════════════════════════════════════════════════
// FraudFlagList Tests
// ══════════════════════════════════════════════════════════════

describe('FraudFlagList', () => {
  it('renders empty message when no flags', () => {
    render(<FraudFlagList flags={[]} />)
    expect(screen.getByText('No fraud flags recorded.')).toBeInTheDocument()
  })

  it('renders flags with severity indicators', () => {
    render(<FraudFlagList flags={sampleFlags} />)
    expect(screen.getByText('HighAmount')).toBeInTheDocument()
    expect(screen.getByText('DuplicateClaim')).toBeInTheDocument()
    expect(screen.getByText('Claim amount exceeds threshold')).toBeInTheDocument()
  })

  it('shows resolved badge for resolved flags', () => {
    render(<FraudFlagList flags={sampleFlags} />)
    expect(screen.getByText('Resolved')).toBeInTheDocument()
  })

  it('handles null flags gracefully', () => {
    render(<FraudFlagList flags={null} />)
    expect(screen.getByText('No fraud flags recorded.')).toBeInTheDocument()
  })
})
