// Create new policy — Component A (Member 1)
// Implements policy creation form with validation, dropdown selection, and auth integration

import React, { useState, useEffect } from 'react'
import { createPolicy, getPolicyTypes } from '../../services/policyService'
import { useAuth } from '../../context/AuthContext'

function PolicyCreate({ onBack, onCreated }) {
  const { user, role } = useAuth()
  const isPolicyholder = role === 'Policyholder'

  const [policyTypes, setPolicyTypes] = useState([])
  const [loadingTypes, setLoadingTypes] = useState(true)

  const [formData, setFormData] = useState({
    policyholderId: isPolicyholder ? (user?.userId || '') : '',
    policyTypeId: '',
    coverageLimit: '',
    deductible: '',
    startDate: '',
    expiryDate: '',
    exclusions: '',
  })
  const [errors, setErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState(null)
  const [success, setSuccess] = useState(false)

  // Fetch policy types on mount
  useEffect(() => {
    let mounted = true
    getPolicyTypes()
      .then((types) => {
        if (mounted) {
          setPolicyTypes(types || [])
          setLoadingTypes(false)
        }
      })
      .catch((err) => {
        if (mounted) {
          console.error('Failed to load policy types:', err)
          setLoadingTypes(false)
        }
      })
    return () => {
      mounted = false
    }
  }, [])

  // Auto-sync policyholderId for policyholder
  useEffect(() => {
    if (isPolicyholder && user?.userId) {
      setFormData((prev) => ({ ...prev, policyholderId: user.userId }))
    }
  }, [isPolicyholder, user?.userId])

  const handleChange = (e) => {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
    // Clear field error on change
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: null }))
    }
  }

  const handlePolicyTypeChange = (e) => {
    const selectedTypeId = e.target.value
    const selectedType = policyTypes.find((pt) => pt.id === selectedTypeId)

    setFormData((prev) => ({
      ...prev,
      policyTypeId: selectedTypeId,
      // Autofill defaults if empty
      coverageLimit: selectedType?.defaultCoverageLimit != null ? String(selectedType.defaultCoverageLimit) : prev.coverageLimit,
      deductible: selectedType?.defaultDeductible != null ? String(selectedType.defaultDeductible) : prev.deductible,
    }))

    if (errors.policyTypeId) {
      setErrors((prev) => ({ ...prev, policyTypeId: null }))
    }
  }

  const validate = () => {
    const newErrors = {}

    const effectivePolicyholderId = isPolicyholder ? (user?.userId || '') : formData.policyholderId.trim()

    if (!effectivePolicyholderId) {
      newErrors.policyholderId = 'Policyholder ID is required.'
    } else {
      const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
      if (!guidRegex.test(effectivePolicyholderId)) {
        newErrors.policyholderId = 'Must be a valid GUID.'
      }
    }

    if (!formData.policyTypeId.trim()) {
      newErrors.policyTypeId = 'Please select a policy type.'
    }

    const coverageLimit = Number(formData.coverageLimit)
    if (!formData.coverageLimit || coverageLimit <= 0) {
      newErrors.coverageLimit = 'Coverage limit must be greater than zero.'
    }

    const deductible = Number(formData.deductible)
    if (formData.deductible !== '' && deductible < 0) {
      newErrors.deductible = 'Deductible cannot be negative.'
    }

    if (!formData.startDate) {
      newErrors.startDate = 'Start date is required.'
    }

    if (!formData.expiryDate) {
      newErrors.expiryDate = 'Expiry date is required.'
    }

    if (formData.startDate && formData.expiryDate && formData.startDate >= formData.expiryDate) {
      newErrors.expiryDate = 'Expiry date must be after start date.'
    }

    if (formData.exclusions && formData.exclusions.length > 2000) {
      newErrors.exclusions = 'Exclusions text cannot exceed 2000 characters.'
    }

    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setSubmitError(null)
    setSuccess(false)

    if (!validate()) return

    setSubmitting(true)
    try {
      const payload = {
        policyholderId: isPolicyholder ? (user?.userId || '') : formData.policyholderId.trim(),
        policyTypeId: formData.policyTypeId.trim(),
        coverageLimit: Number(formData.coverageLimit),
        deductible: Number(formData.deductible) || 0,
        startDate: new Date(formData.startDate).toISOString(),
        expiryDate: new Date(formData.expiryDate).toISOString(),
        exclusions: formData.exclusions || null,
      }
      const created = await createPolicy(payload)
      setSuccess(true)
      if (onCreated) onCreated(created)
    } catch (err) {
      setSubmitError(err.message || 'Failed to create policy.')
    } finally {
      setSubmitting(false)
    }
  }

  const fieldStyle = {
    width: '100%',
    padding: '8px 12px',
    border: '1px solid #d1d5db',
    borderRadius: '6px',
    fontSize: '0.9rem',
    boxSizing: 'border-box',
    backgroundColor: '#fff',
    color: '#111827',
  }

  const errorFieldStyle = { ...fieldStyle, borderColor: '#dc2626' }

  const labelStyle = { display: 'block', fontWeight: 500, marginBottom: '4px', color: '#374151' }

  const fieldErrorStyle = { color: '#dc2626', fontSize: '0.8rem', marginTop: '4px' }

  return (
    <div style={{ maxWidth: '600px', margin: '0 auto', padding: '24px' }}>
      <button
        onClick={onBack}
        style={{ marginBottom: '16px', cursor: 'pointer', background: 'none', border: 'none', color: '#3b82f6', fontSize: '0.95rem' }}
      >
        ← Back to Policies
      </button>
      <h2>Create Policy</h2>

      {success && (
        <div style={{ padding: '12px', backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', color: '#16a34a', marginBottom: '16px' }}>
          Policy created successfully!
        </div>
      )}

      {submitError && (
        <div style={{ padding: '12px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#dc2626', marginBottom: '16px' }}>
          {submitError}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        {/* Policyholder Field */}
        <div style={{ marginBottom: '16px' }}>
          <label style={labelStyle}>Policyholder</label>
          {isPolicyholder ? (
            <div
              style={{
                padding: '10px 14px',
                backgroundColor: '#f3f4f6',
                border: '1px solid #e5e7eb',
                borderRadius: '6px',
                fontSize: '0.9rem',
                color: '#1f2937',
              }}
            >
              <strong>{user?.firstName} {user?.lastName}</strong> ({user?.email})
            </div>
          ) : (
            <>
              <input
                type="text"
                name="policyholderId"
                value={formData.policyholderId}
                onChange={handleChange}
                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                style={errors.policyholderId ? errorFieldStyle : fieldStyle}
              />
              {errors.policyholderId && <div style={fieldErrorStyle}>{errors.policyholderId}</div>}
            </>
          )}
        </div>

        {/* Policy Type Dropdown */}
        <div style={{ marginBottom: '16px' }}>
          <label style={labelStyle}>Policy Type *</label>
          <select
            name="policyTypeId"
            value={formData.policyTypeId}
            onChange={handlePolicyTypeChange}
            disabled={loadingTypes}
            style={errors.policyTypeId ? errorFieldStyle : fieldStyle}
          >
            <option value="">
              {loadingTypes ? 'Loading policy types...' : 'Select a policy type...'}
            </option>
            {policyTypes.map((pt) => (
              <option key={pt.id} value={pt.id}>
                {pt.name} {pt.description ? `— ${pt.description}` : ''}
              </option>
            ))}
          </select>
          {errors.policyTypeId && <div style={fieldErrorStyle}>{errors.policyTypeId}</div>}
        </div>

        <div style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Coverage Limit ($) *</label>
            <input
              type="number"
              name="coverageLimit"
              value={formData.coverageLimit}
              onChange={handleChange}
              placeholder="50000"
              min="0"
              step="0.01"
              style={errors.coverageLimit ? errorFieldStyle : fieldStyle}
            />
            {errors.coverageLimit && <div style={fieldErrorStyle}>{errors.coverageLimit}</div>}
          </div>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Deductible ($)</label>
            <input
              type="number"
              name="deductible"
              value={formData.deductible}
              onChange={handleChange}
              placeholder="1000"
              min="0"
              step="0.01"
              style={errors.deductible ? errorFieldStyle : fieldStyle}
            />
            {errors.deductible && <div style={fieldErrorStyle}>{errors.deductible}</div>}
          </div>
        </div>

        <div style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Start Date *</label>
            <input
              type="date"
              name="startDate"
              value={formData.startDate}
              onChange={handleChange}
              style={errors.startDate ? errorFieldStyle : fieldStyle}
            />
            {errors.startDate && <div style={fieldErrorStyle}>{errors.startDate}</div>}
          </div>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Expiry Date *</label>
            <input
              type="date"
              name="expiryDate"
              value={formData.expiryDate}
              onChange={handleChange}
              style={errors.expiryDate ? errorFieldStyle : fieldStyle}
            />
            {errors.expiryDate && <div style={fieldErrorStyle}>{errors.expiryDate}</div>}
          </div>
        </div>

        <div style={{ marginBottom: '20px' }}>
          <label style={labelStyle}>Exclusions</label>
          <textarea
            name="exclusions"
            value={formData.exclusions}
            onChange={handleChange}
            rows={3}
            placeholder="Any exclusions or limitations..."
            style={errors.exclusions ? errorFieldStyle : fieldStyle}
          />
          {errors.exclusions && <div style={fieldErrorStyle}>{errors.exclusions}</div>}
        </div>

        <button
          type="submit"
          disabled={submitting}
          style={{
            width: '100%',
            padding: '12px',
            backgroundColor: submitting ? '#9ca3af' : '#3b82f6',
            color: '#fff',
            border: 'none',
            borderRadius: '6px',
            fontSize: '1rem',
            fontWeight: 600,
            cursor: submitting ? 'not-allowed' : 'pointer',
          }}
        >
          {submitting ? 'Creating...' : 'Create Policy'}
        </button>
      </form>
    </div>
  )
}

export default PolicyCreate
