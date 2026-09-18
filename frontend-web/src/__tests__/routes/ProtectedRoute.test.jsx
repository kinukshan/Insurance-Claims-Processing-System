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
})
