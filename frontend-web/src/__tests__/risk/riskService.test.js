import { describe, it, expect, vi, beforeEach } from 'vitest'
import {
  assessClaim,
  getAssessment,
  getFlaggedClaims,
  getFraudHistory,
  escalateClaim,
  getFlags,
  updateFraudCase,
  getPolicyholderStatus,
} from '../../services/riskService'
import { apiFetch } from '../../services/api'

vi.mock('../../services/api', () => ({
  apiFetch: vi.fn(),
  API_BASE_URL: 'http://localhost:5000/api',
  DEV_USER_ID: '00000000-0000-0000-0000-000000000001',
}))

describe('riskService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('getFlaggedClaims delegates to apiFetch with /riskassessments/flagged', async () => {
    const mockData = [{ id: 'flag-1' }]
    apiFetch.mockResolvedValue(mockData)

    const result = await getFlaggedClaims()

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/flagged')
    expect(result).toBe(mockData)
  })

  it('getAssessment delegates to apiFetch with /riskassessments/:id', async () => {
    const mockData = { id: 'assess-1', riskScore: 42 }
    apiFetch.mockResolvedValue(mockData)

    const result = await getAssessment('claim-123')

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/claim-123')
    expect(result).toBe(mockData)
  })

  it('getAssessment returns null when apiFetch throws 404', async () => {
    const notFoundError = new Error('Not found')
    notFoundError.status = 404
    apiFetch.mockRejectedValue(notFoundError)

    const result = await getAssessment('claim-missing')

    expect(result).toBeNull()
  })

  it('getAssessment rethrows non-404 errors', async () => {
    const serverError = new Error('Internal error')
    serverError.status = 500
    apiFetch.mockRejectedValue(serverError)

    await expect(getAssessment('claim-500')).rejects.toThrow('Internal error')
  })

  it('assessClaim delegates to apiFetch with POST and JSON options', async () => {
    const mockData = { id: 'assess-new', riskScore: 88 }
    apiFetch.mockResolvedValue(mockData)

    const options = { includeAiAnalysis: true, notes: 'urgent' }
    const result = await assessClaim('claim-123', options)

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/claim-123/assess', {
      method: 'POST',
      body: JSON.stringify(options),
    })
    expect(result).toBe(mockData)
  })

  it('getFraudHistory delegates to apiFetch with /riskassessments/history/:policyholderId', async () => {
    const mockData = [{ id: 'case-1' }]
    apiFetch.mockResolvedValue(mockData)

    const result = await getFraudHistory('ph-123')

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/history/ph-123')
    expect(result).toBe(mockData)
  })

  it('escalateClaim delegates to apiFetch with POST and data', async () => {
    const mockData = { id: 'case-new' }
    apiFetch.mockResolvedValue(mockData)

    const escalateData = { reason: 'suspicious', priority: 'High' }
    const result = await escalateClaim('assess-1', escalateData)

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/assess-1/escalate', {
      method: 'POST',
      body: JSON.stringify(escalateData),
    })
    expect(result).toBe(mockData)
  })

  it('getFlags delegates to apiFetch with /riskassessments/:id/flags', async () => {
    const mockData = [{ id: 'flag-1' }]
    apiFetch.mockResolvedValue(mockData)

    const result = await getFlags('claim-123')

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/claim-123/flags')
    expect(result).toBe(mockData)
  })

  it('updateFraudCase delegates to apiFetch with PUT and data', async () => {
    const mockData = { id: 'case-1', status: 'Closed' }
    apiFetch.mockResolvedValue(mockData)

    const updateData = { status: 'Closed', resolution: 'Confirmed fraud' }
    const result = await updateFraudCase('case-1', updateData)

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/fraud-cases/case-1', {
      method: 'PUT',
      body: JSON.stringify(updateData),
    })
    expect(result).toBe(mockData)
  })

  it('getPolicyholderStatus delegates to apiFetch with /riskassessments/:id/status', async () => {
    const mockData = { status: 'Under Review' }
    apiFetch.mockResolvedValue(mockData)

    const result = await getPolicyholderStatus('claim-123')

    expect(apiFetch).toHaveBeenCalledWith('/riskassessments/claim-123/status')
    expect(result).toBe(mockData)
  })
})
