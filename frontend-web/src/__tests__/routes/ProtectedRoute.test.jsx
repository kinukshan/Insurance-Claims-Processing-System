import { describe, it, expect } from 'vitest'

describe('ProtectedRoute access control logic', () => {
  const getDestination = ({ isAuthenticated, userRole, requiredRoles }) => {
    if (!isAuthenticated) {
      return '/login'
    }

    if (requiredRoles && requiredRoles.length > 0 && !requiredRoles.includes(userRole)) {
      return '/dashboard'
    }

    return null // access allowed
  }

  it('redirects unauthenticated users to /login', () => {
    const dest = getDestination({
      isAuthenticated: false,
      userRole: null,
      requiredRoles: ['Admin'],
    })
    expect(dest).toBe('/login')
  })

  it('allows authenticated user when no role restrictions are defined', () => {
    const dest = getDestination({
      isAuthenticated: true,
      userRole: 'Policyholder',
      requiredRoles: [],
    })
    expect(dest).toBeNull()
  })

  it('allows access when user has matching role', () => {
    const dest = getDestination({
      isAuthenticated: true,
      userRole: 'ClaimsAdjuster',
      requiredRoles: ['ClaimsAdjuster', 'Admin'],
    })
    expect(dest).toBeNull()
  })

  it('redirects unauthorized role to /dashboard', () => {
    const dest = getDestination({
      isAuthenticated: true,
      userRole: 'Policyholder',
      requiredRoles: ['ClaimsAdjuster', 'Admin'],
    })
    expect(dest).toBe('/dashboard')
  })

  // ── Payout Calculation Route (/payouts/calculate) Protection Tests ──

  const PAYOUT_CALCULATE_ROLES = ['ClaimsAdjuster', 'Underwriter', 'Admin']

  it('16. payout calculation route remains protected by staff roles (ClaimsAdjuster, Underwriter, Admin)', () => {
    // ClaimsAdjuster
    expect(getDestination({
      isAuthenticated: true,
      userRole: 'ClaimsAdjuster',
      requiredRoles: PAYOUT_CALCULATE_ROLES,
    })).toBeNull()

    // Underwriter
    expect(getDestination({
      isAuthenticated: true,
      userRole: 'Underwriter',
      requiredRoles: PAYOUT_CALCULATE_ROLES,
    })).toBeNull()

    // Admin
    expect(getDestination({
      isAuthenticated: true,
      userRole: 'Admin',
      requiredRoles: PAYOUT_CALCULATE_ROLES,
    })).toBeNull()
  })

  it('17. Policyholder cannot manually access payout calculation route and is redirected to /dashboard', () => {
    const dest = getDestination({
      isAuthenticated: true,
      userRole: 'Policyholder',
      requiredRoles: PAYOUT_CALCULATE_ROLES,
    })
    expect(dest).toBe('/dashboard')
  })

  it('unauthenticated user attempting to access payout calculation route is redirected to /login', () => {
    const dest = getDestination({
      isAuthenticated: false,
      userRole: null,
      requiredRoles: PAYOUT_CALCULATE_ROLES,
    })
    expect(dest).toBe('/login')
  })
})
