import React, { createContext, useContext, useState, useEffect, useCallback } from 'react'
import * as authService from '../services/authService'

const AuthContext = createContext(null)

/**
 * AuthProvider — wraps the app with authentication state.
 * Token stored in sessionStorage (clears on tab close).
 */
export function AuthProvider({ children }) {
  const [user, setUser] = useState(authService.getStoredUser())
  const [loading, setLoading] = useState(true)

  // On mount: check sessionStorage and hydrate user from /api/auth/me
  useEffect(() => {
    const hydrate = async () => {
      if (!authService.isAuthenticated()) {
        setUser(null)
        setLoading(false)
        return
      }

      try {
        const profile = await authService.getCurrentUser()
        const userData = {
          userId: profile.userId,
          firstName: profile.firstName,
          lastName: profile.lastName,
          email: profile.email,
          role: profile.role,
        }
        setUser(userData)
        sessionStorage.setItem('auth_user', JSON.stringify(userData))
      } catch {
        // Token expired or invalid — clear auth state
        authService.logout()
        setUser(null)
      } finally {
        setLoading(false)
      }
    }

    hydrate()
  }, [])

  const login = useCallback(async ({ email, password }) => {
    const response = await authService.login({ email, password })
    authService.saveAuth(response)
    const userData = {
      userId: response.userId,
      firstName: response.firstName,
      lastName: response.lastName,
      email: response.email,
      role: response.role,
    }
    setUser(userData)
    return userData
  }, [])

  const register = useCallback(async ({ firstName, lastName, email, password, confirmPassword }) => {
    const response = await authService.register({ firstName, lastName, email, password, confirmPassword })
    authService.saveAuth(response)
    const userData = {
      userId: response.userId,
      firstName: response.firstName,
      lastName: response.lastName,
      email: response.email,
      role: response.role,
    }
    setUser(userData)
    return userData
  }, [])

  const logout = useCallback(() => {
    authService.logout()
    setUser(null)
  }, [])

  const value = {
    user,
    loading,
    isAuthenticated: !!user,
    role: user?.role || null,
    login,
    register,
    logout,
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}

/**
 * Hook to access auth context.
 */
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

export default AuthContext
