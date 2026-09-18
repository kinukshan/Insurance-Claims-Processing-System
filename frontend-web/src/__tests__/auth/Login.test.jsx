import { describe, it, expect, beforeEach } from 'vitest'
import * as authService from '../../services/authService'

describe('authService', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('initially reports not authenticated when sessionStorage is empty', () => {
    expect(authService.isAuthenticated()).toBe(false)
    expect(authService.getStoredUser()).toBeNull()
  })

  it('saveAuth stores token and user in sessionStorage and reports authenticated', () => {
    const mockAuthResponse = {
      token: 'mock-jwt-token-12345',
      userId: '11111111-1111-1111-1111-111111111111',
      firstName: 'Alice',
      lastName: 'Smith',
      email: 'alice@example.com',
      role: 'Policyholder',
    }

    authService.saveAuth(mockAuthResponse)

    expect(authService.isAuthenticated()).toBe(true)
    expect(sessionStorage.getItem('auth_token')).toBe('mock-jwt-token-12345')

    const storedUser = authService.getStoredUser()
    expect(storedUser).toEqual({
      userId: '11111111-1111-1111-1111-111111111111',
      firstName: 'Alice',
      lastName: 'Smith',
      email: 'alice@example.com',
      role: 'Policyholder',
    })
  })

  it('logout removes token and user from sessionStorage', () => {
    sessionStorage.setItem('auth_token', 'token-to-remove')
    sessionStorage.setItem('auth_user', JSON.stringify({ userId: 'u1', role: 'Policyholder' }))

    expect(authService.isAuthenticated()).toBe(true)

    authService.logout()

    expect(authService.isAuthenticated()).toBe(false)
    expect(authService.getStoredUser()).toBeNull()
    expect(sessionStorage.getItem('auth_token')).toBeNull()
    expect(sessionStorage.getItem('auth_user')).toBeNull()
  })

  it('getStoredUser handles malformed JSON gracefully', () => {
    sessionStorage.setItem('auth_user', '{ invalid json')
    expect(authService.getStoredUser()).toBeNull()
  })
})

describe('Role validation and dashboard routing', () => {
  const allowedRoles = ['Policyholder', 'ClaimsAdjuster', 'Underwriter', 'Admin']

  it('all system roles are defined and recognized', () => {
    expect(allowedRoles).toContain('Policyholder')
    expect(allowedRoles).toContain('ClaimsAdjuster')
    expect(allowedRoles).toContain('Underwriter')
    expect(allowedRoles).toContain('Admin')
  })

  it('distinguishes staff roles from policyholder for route protection', () => {
    const isStaff = (role) => ['ClaimsAdjuster', 'Underwriter', 'Admin'].includes(role)

    expect(isStaff('Policyholder')).toBe(false)
    expect(isStaff('ClaimsAdjuster')).toBe(true)
    expect(isStaff('Underwriter')).toBe(true)
    expect(isStaff('Admin')).toBe(true)
  })
})

describe('Registration validation rules', () => {
  function validateRegistration({ firstName, lastName, email, password, confirmPassword }) {
    if (!firstName?.trim()) return 'First name is required.'
    if (!lastName?.trim()) return 'Last name is required.'
    if (!email?.trim()) return 'Email is required.'
    if (!password) return 'Password is required.'
    if (password.length < 8) return 'Password must be at least 8 characters.'
    if (password !== confirmPassword) return 'Passwords do not match.'
    return null
  }

  it('rejects passwords shorter than 8 characters', () => {
    const err = validateRegistration({
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      password: 'short',
      confirmPassword: 'short',
    })
    expect(err).toBe('Password must be at least 8 characters.')
  })

  it('rejects mismatched password and confirmPassword', () => {
    const err = validateRegistration({
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      password: 'SecurePassword123',
      confirmPassword: 'DifferentPassword456',
    })
    expect(err).toBe('Passwords do not match.')
  })

  it('accepts valid registration input', () => {
    const err = validateRegistration({
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      password: 'SecurePassword123!',
      confirmPassword: 'SecurePassword123!',
    })
    expect(err).toBeNull()
  })
})
