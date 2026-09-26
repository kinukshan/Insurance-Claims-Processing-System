import React, { useState, useEffect, useCallback } from 'react'
import PolicyStatusBadge from '../../components/policy/PolicyStatusBadge'
import CoverageTable from '../../components/policy/CoverageTable'
import { getPolicyById, calculatePremium, renewPolicy, getCoverage } from '../../services/policyService'
import { useAuth } from '../../context/AuthContext'

function useOptionalAuth() {
  try {
    const auth = useAuth()
    return {
      role: auth.role || auth.user?.role || null,
      user: auth.user || null,
    }
  } catch {
    return { role: null, user: null }
  }
}

function PolicyDetails({ policyId, onBack, onEdit }) {
  const { role, user } = useOptionalAuth()
  const currentRole = role || user?.role
  const canEdit = currentRole === 'Underwriter' || currentRole === 'Admin'

  const [policy, setPolicy] = useState(null)
  const [coverages, setCoverages] = useState([])
  const [premiumResult, setPremiumResult] = useState(null)
  const [renewalResult, setRenewalResult] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [successMessage, setSuccessMessage] = useState(null)

  const fetchPolicy = useCallback(async () => {
    if (!policyId) return
    setLoading(true)
    setError(null)
    try {
      const [policyData, coverageData] = await Promise.all([
        getPolicyById(policyId),
        getCoverage(policyId),
      ])
      setPolicy(policyData)
      setCoverages(coverageData)
    } catch (err) {
      setError(err.message || 'Failed to load policy details.')
    } finally {
      setLoading(false)
    }
  }, [policyId])

  useEffect(() => {
    fetchPolicy()
  }, [fetchPolicy])

  const handleCalculatePremium = async () => {
    setActionLoading(true)
    setPremiumResult(null)
    try {
      const result = await calculatePremium(policyId)
      setPremiumResult(result)
    } catch (err) {
      setError(err.message)
    } finally {
      setActionLoading(false)
    }
  }

  const handleRenew = async () => {
    setActionLoading(true)
    setRenewalResult(null)
    try {
      const result = await renewPolicy(policyId)
      setRenewalResult(result)
      if (result.success) {
        setSuccessMessage('Policy renewed successfully!')
        fetchPolicy()
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setActionLoading(false)
    }
  }

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '40px', color: '#6b7280' }}>
        <p>Loading policy details...</p>
      </div>
    )
  }

  if (error && !policy) {
    return (
      <div style={{ maxWidth: '800px', margin: '0 auto', padding: '24px' }}>
        <button onClick={onBack} style={{ marginBottom: '16px', cursor: 'pointer' }}>
          ← Back to Policies
        </button>
        <div
          style={{
            padding: '16px',
            backgroundColor: '#fef2f2',
            border: '1px solid #fecaca',
            borderRadius: '8px',
            color: '#dc2626',
          }}
        >
          <strong>Error:</strong> {error}
        </div>
      </div>
    )
  }

  if (!policy) return null

  const infoRow = (label, value) => (
    <div style={{ display: 'flex', padding: '8px 0', borderBottom: '1px solid #f3f4f6' }}>
      <span style={{ width: '180px', fontWeight: 500, color: '#374151' }}>{label}</span>
      <span style={{ color: '#6b7280' }}>{value}</span>
    </div>
  )

  return (
    <div style={{ maxWidth: '800px', margin: '0 auto', padding: '24px' }}>
      <button
        onClick={onBack}
        style={{
          marginBottom: '16px',
          cursor: 'pointer',
          background: 'none',
          border: 'none',
          color: '#3b82f6',
          fontSize: '0.95rem',
        }}
      >
        ← Back to Policies
      </button>

      {/* Success Message */}
      {successMessage && (
        <div
          style={{
            padding: '12px 16px',
            backgroundColor: '#f0fdf4',
            border: '1px solid #bbf7d0',
            borderRadius: '8px',
            color: '#16a34a',
            marginBottom: '16px',
          }}
        >
          {successMessage}
        </div>
      )}

      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <div>
          <h2 style={{ margin: '0 0 4px 0' }}>Policy {policy.policyNumber}</h2>
          <PolicyStatusBadge status={policy.status} />
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          {canEdit && onEdit && (
            <button
              onClick={() => onEdit(policy.id)}
              style={{
                padding: '8px 16px',
                backgroundColor: '#f59e0b',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                cursor: 'pointer',
                fontWeight: 600,
              }}
            >
              Edit
            </button>
          )}
        </div>
      </div>

      {/* Policy Info */}
      <div style={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', padding: '16px', marginBottom: '20px' }}>
        <h3 style={{ marginTop: 0 }}>Policy Information</h3>
        {infoRow('Policy Type', policy.policyTypeName || 'N/A')}
        {infoRow('Policyholder ID', policy.policyholderId)}
        {infoRow('Coverage Limit', `$${Number(policy.coverageLimit).toLocaleString()}`)}
        {infoRow('Premium', `$${Number(policy.premium).toLocaleString()}`)}
        {infoRow('Deductible', `$${Number(policy.deductible).toLocaleString()}`)}
        {infoRow('Start Date', new Date(policy.startDate).toLocaleDateString())}
        {infoRow('Expiry Date', new Date(policy.expiryDate).toLocaleDateString())}
        {infoRow('Renewal Status', policy.renewalStatus)}
        {infoRow('Expired', policy.isExpired ? 'Yes' : 'No')}
        {policy.exclusions && infoRow('Exclusions', policy.exclusions)}
      </div>

      {/* Coverage */}
      <div style={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', padding: '16px', marginBottom: '20px' }}>
        <h3 style={{ marginTop: 0 }}>Coverage Details</h3>
        <CoverageTable coverages={coverages} />
      </div>

      {/* Actions */}
      <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', marginBottom: '20px' }}>
        <button
          onClick={handleCalculatePremium}
          disabled={actionLoading}
          style={{
            padding: '10px 20px',
            backgroundColor: '#8b5cf6',
            color: '#fff',
            border: 'none',
            borderRadius: '6px',
            cursor: actionLoading ? 'not-allowed' : 'pointer',
            fontWeight: 600,
          }}
        >
          {actionLoading ? 'Calculating...' : 'Calculate Premium'}
        </button>
        {policy.canRenew && (
          <button
            onClick={handleRenew}
            disabled={actionLoading}
            style={{
              padding: '10px 20px',
              backgroundColor: '#10b981',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              cursor: actionLoading ? 'not-allowed' : 'pointer',
              fontWeight: 600,
            }}
          >
            {actionLoading ? 'Renewing...' : 'Renew Policy'}
          </button>
        )}
      </div>

      {/* Premium Calculation Result */}
      {premiumResult && (
        <div style={{ backgroundColor: '#f5f3ff', border: '1px solid #c4b5fd', borderRadius: '8px', padding: '16px', marginBottom: '16px' }}>
          <h4 style={{ marginTop: 0 }}>Premium Calculation</h4>
          <p><strong>Calculated Premium:</strong> ${Number(premiumResult.calculatedPremium).toLocaleString()}</p>
          <p style={{ fontSize: '0.85rem', color: '#6b7280' }}>{premiumResult.breakdown}</p>
        </div>
      )}

      {/* Renewal Result */}
      {renewalResult && !renewalResult.success && (
        <div style={{ backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', padding: '16px' }}>
          <p style={{ color: '#dc2626' }}>{renewalResult.message}</p>
        </div>
      )}

      {/* Error */}
      {error && (
        <div style={{ padding: '12px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#dc2626', marginTop: '12px' }}>
          {error}
        </div>
      )}
    </div>
  )
}

export default PolicyDetails
