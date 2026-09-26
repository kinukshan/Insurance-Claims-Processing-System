import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { apiFetch, extractErrorMessage } from '../../services/api'

describe('API Client Error Normalization & Resilience', () => {
  const originalFetch = global.fetch

  beforeEach(() => {
    sessionStorage.clear()
  })

  afterEach(() => {
    global.fetch = originalFetch
    vi.restoreAllMocks()
  })

  describe('extractErrorMessage', () => {
    it('handles ASP.NET validation error dictionary without calling .join on an object', () => {
      const errorBody = {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          ClaimId: ['The Claim ID is invalid.'],
          Priority: ['The Priority field is required.', 'Priority must be a valid value.'],
        },
      }
      const response = { status: 400, statusText: 'Bad Request' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('The Claim ID is invalid.; The Priority field is required.; Priority must be a valid value.')
    })

    it('handles errors as an array of strings', () => {
      const errorBody = {
        errors: ['Claim amount exceeds threshold', 'Duplicate claim detected'],
      }
      const response = { status: 400, statusText: 'Bad Request' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('Claim amount exceeds threshold; Duplicate claim detected')
    })

    it('handles error as a single string', () => {
      const errorBody = {
        error: 'Claim 123 not found.',
      }
      const response = { status: 404, statusText: 'Not Found' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('Claim 123 not found.')
    })

    it('handles message property when error is absent', () => {
      const errorBody = {
        message: 'Invalid operation requested.',
      }
      const response = { status: 409, statusText: 'Conflict' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('Invalid operation requested.')
    })

    it('handles RFC 7807 ProblemDetails detail and title properties', () => {
      const errorBody = {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
        title: 'Resource Not Found',
        status: 404,
        detail: 'Risk assessment with ID ef39e0fd was not found.',
      }
      const response = { status: 404, statusText: 'Not Found' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('Risk assessment with ID ef39e0fd was not found.')
    })

    it('falls back to title when detail and errors are absent', () => {
      const errorBody = {
        title: 'Unauthorized Access',
        status: 401,
      }
      const response = { status: 401, statusText: 'Unauthorized' }
      const message = extractErrorMessage(errorBody, response)

      expect(message).toBe('Unauthorized Access')
    })

    it('handles empty or unexpected error response objects gracefully', () => {
      const response = { status: 500, statusText: 'Internal Server Error' }
      expect(extractErrorMessage({}, response)).toBe('Internal Server Error (500)')
      expect(extractErrorMessage(null, response)).toBe('Internal Server Error')
      expect(extractErrorMessage({ unexpected: 42 }, response)).toBe('Internal Server Error (500)')
    })

    it('sanitizes sensitive SQL errors, stack traces, and database exceptions', () => {
      const sensitiveBodies = [
        {
          error: 'An unexpected error occurred.',
          message: 'Npgsql.PostgresException (0x80004005): 42P01: relation "NotificationLogs" does not exist POSITION: 275',
        },
        {
          detail: 'System.Data.SqlClient.SqlException: syntax error at or near "SELECT * FROM Users"',
        },
        {
          errors: {
            db: ['at Microsoft.EntityFrameworkCore.DbContext.SaveChanges() at InsuranceClaims.Api.Controllers...'],
          },
        },
      ]

      for (const body of sensitiveBodies) {
        const response = { status: 500, statusText: 'Internal Server Error' }
        const result = extractErrorMessage(body, response)
        expect(result).toBe('An unexpected server error occurred. Please try again.')
        expect(result).not.toContain('Npgsql')
        expect(result).not.toContain('PostgresException')
        expect(result).not.toContain('SqlException')
        expect(result).not.toContain('relation')
        expect(result).not.toContain('Microsoft.EntityFrameworkCore')
      }
    })
  })

  describe('apiFetch integration with error normalization', () => {
    it('handles ASP.NET validation error dictionary without throwing TypeError', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => ({
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            request: ['The request field is required.'],
            '$.priority': ['The JSON value could not be converted to FraudCasePriority.'],
          },
        }),
      })

      let thrownError = null
      try {
        await apiFetch('/riskassessments/a1/escalate', {
          method: 'POST',
          body: JSON.stringify({ reason: 'test', priority: 'High' }),
        })
      } catch (err) {
        thrownError = err
      }

      expect(thrownError).toBeInstanceOf(Error)
      expect(thrownError.message).not.toContain('is not a function')
      expect(thrownError.message).toContain('The request field is required.')
      expect(thrownError.message).toContain('The JSON value could not be converted')
      expect(thrownError.status).toBe(400)
      expect(thrownError.body).toBeDefined()
    })

    it('handles array errors safely', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => ({
          errors: ['First validation error', 'Second validation error'],
        }),
      })

      await expect(apiFetch('/test')).rejects.toThrow('First validation error; Second validation error')
    })

    it('handles string errors safely', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        json: async () => ({
          error: 'Claim not found',
        }),
      })

      await expect(apiFetch('/test')).rejects.toThrow('Claim not found')
    })

    it('handles network failure when fetch throws TypeError', async () => {
      global.fetch = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))

      let caught = null
      try {
        await apiFetch('/test')
      } catch (err) {
        caught = err
      }

      expect(caught).toBeInstanceOf(Error)
      expect(caught.isNetworkError).toBe(true)
      expect(caught.status).toBe(0)
      expect(caught.message).toContain('Network error')
    })

    it('returns null on 204 No Content', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      })

      const result = await apiFetch('/test')
      expect(result).toBeNull()
    })

    it('returns parsed json on 200 OK', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: '123', status: 'Success' }),
      })

      const result = await apiFetch('/test')
      expect(result).toEqual({ id: '123', status: 'Success' })
    })
  })
})
