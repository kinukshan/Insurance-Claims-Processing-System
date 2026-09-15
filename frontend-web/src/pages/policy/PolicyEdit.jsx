// Edit policy — Component A (Member 1)
// Pre-populates form with existing policy data, validates, and submits update

import React, { useState, useEffect } from 'react'
import { getPolicyById, updatePolicy } from '../../services/policyService'

const STATUS_OPTIONS = ['Draft', 'Active', 'Cancelled']

function PolicyEdit({ policyId, onBack, onUpdated }) {
  const [formData, setFormData] = useState({
    coverageLimit: '',
    deductible: '',
    expiryDate: '',
    exclusions: '',
    status: '',
  })
  const [originalPolicy, setOriginalPolicy] = useState(null)
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState(null)
  const [success, setSuccess] = useState(false)

  useEffect(() => {
    const fetchPolicy = async () => {
      setLoading(true)
      try {
        const policy = await getPolicyById(policyId)
        setOriginalPolicy(policy)
        setFormData({
          coverageLimit: String(policy.coverageLimit),
          deductible: String(policy.deductible),
          expiryDate: policy.expiryDate ? policy.expiryDate.split('T')[0] : '',
          exclusions: policy.exclusions || '',
          status: policy.status,
        })
      } catch (err) {
        setSubmitError(err.message || 'Failed to load policy.')
      } finally {
        setLoading(false)
      }
    }
    fetchPolicy()
  }, [policyId])

  const handleChange = (e) => {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: null }))
    }
  }

  const validate = () => {
    const newErrors = {}

    const coverageLimit = Number(formData.coverageLimit)
    if (!formData.coverageLimit || coverageLimit <= 0) {
      newErrors.coverageLimit = 'Coverage limit must be greater than zero.'
    }

    const deductible = Number(formData.deductible)
    if (formData.deductible !== '' && deductible < 0) {
      newErrors.deductible = 'Deductible cannot be negative.'
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
        coverageLimit: Number(formData.coverageLimit),
        deductible: Number(formData.deductible) || 0,
        expiryDate: formData.expiryDate ? new Date(formData.expiryDate).toISOString() : undefined,
        exclusions: formData.exclusions || null,
        status: formData.status,
      }
      const updated = await updatePolicy(policyId, payload)
      setSuccess(true)
      if (onUpdated) onUpdated(updated)
    } catch (err) {
      setSubmitError(err.message || 'Failed to update policy.')
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
  }
  const errorFieldStyle = { ...fieldStyle, borderColor: '#dc2626' }
  const labelStyle = { display: 'block', fontWeight: 500, marginBottom: '4px', color: '#374151' }
  const fieldErrorStyle = { color: '#dc2626', fontSize: '0.8rem', marginTop: '4px' }

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '40px', color: '#6b7280' }}>
        <p>Loading policy...</p>
      </div>
    )
  }

  return (
    <div style={{ maxWidth: '600px', margin: '0 auto', padding: '24px' }}>
      <button
        onClick={onBack}
        style={{ marginBottom: '16px', cursor: 'pointer', background: 'none', border: 'none', color: '#3b82f6', fontSize: '0.95rem' }}
      >
        ← Back
      </button>
      <h2>Edit Policy {originalPolicy?.policyNumber}</h2>

      {success && (
        <div style={{ padding: '12px', backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', color: '#16a34a', marginBottom: '16px' }}>
          Policy updated successfully!
        </div>
      )}

      {submitError && (
        <div style={{ padding: '12px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#dc2626', marginBottom: '16px' }}>
          {submitError}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Coverage Limit</label>
            <input
              type="number"
              name="coverageLimit"
              value={formData.coverageLimit}
              onChange={handleChange}
              min="0"
              step="0.01"
              style={errors.coverageLimit ? errorFieldStyle : fieldStyle}
            />
            {errors.coverageLimit && <div style={fieldErrorStyle}>{errors.coverageLimit}</div>}
          </div>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Deductible</label>
            <input
              type="number"
              name="deductible"
              value={formData.deductible}
              onChange={handleChange}
              min="0"
              step="0.01"
              style={errors.deductible ? errorFieldStyle : fieldStyle}
            />
            {errors.deductible && <div style={fieldErrorStyle}>{errors.deductible}</div>}
          </div>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label style={labelStyle}>Expiry Date</label>
          <input
            type="date"
            name="expiryDate"
            value={formData.expiryDate}
            onChange={handleChange}
            style={fieldStyle}
          />
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label style={labelStyle}>Status</label>
          <select
            name="status"
            value={formData.status}
            onChange={handleChange}
            style={fieldStyle}
          >
            {STATUS_OPTIONS.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>

        <div style={{ marginBottom: '20px' }}>
          <label style={labelStyle}>Exclusions</label>
          <textarea
            name="exclusions"
            value={formData.exclusions}
            onChange={handleChange}
            rows={3}
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
            backgroundColor: submitting ? '#9ca3af' : '#f59e0b',
            color: '#fff',
            border: 'none',
            borderRadius: '6px',
            fontSize: '1rem',
            fontWeight: 600,
            cursor: submitting ? 'not-allowed' : 'pointer',
          }}
        >
          {submitting ? 'Updating...' : 'Update Policy'}
        </button>
      </form>
    </div>
  )
}

export default PolicyEdit
