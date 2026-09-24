/**
 * Create Claim — Policyholder claim submission form.
 *
 * Allows policyholders to file a new insurance claim against one of their active policies.
 * The claim is created as a Draft, then the policyholder can submit it to trigger the AI pipeline.
 *
 * Required fields match the backend CreateClaimDto:
 *   PolicyId, ClaimType, IncidentDate, IncidentLocation, Description, ClaimedAmount
 */

import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { createClaim, submitClaim } from '../../services/claimService';
import { getPolicies } from '../../services/policyService';
import { CLAIM_TYPES, getCompatibleClaimType } from '../../utils/policyClaimMapping';

function ClaimCreate({ onBack, onCreated }) {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [policies, setPolicies] = useState([]);
  const [loadingPolicies, setLoadingPolicies] = useState(true);

  const [formData, setFormData] = useState({
    policyId: '',
    claimType: '',
    incidentDate: '',
    incidentLocation: '',
    description: '',
    claimedAmount: '',
  });

  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState(null);
  const [createdClaim, setCreatedClaim] = useState(null);
  const [submittingClaim, setSubmittingClaim] = useState(false);

  // Fetch the user's policies on mount
  useEffect(() => {
    let mounted = true;
    getPolicies()
      .then((data) => {
        if (mounted) {
          // Only show active policies the user can claim against
          const activePolicies = (data || []).filter(
            (p) => p.status === 'Active' || p.status === 'active'
          );
          setPolicies(activePolicies);
          setLoadingPolicies(false);
        }
      })
      .catch((err) => {
        if (mounted) {
          console.error('Failed to load policies:', err);
          setLoadingPolicies(false);
        }
      });
    return () => {
      mounted = false;
    };
  }, []);

  const selectedPolicy = policies.find((p) => p.id === formData.policyId);
  const compatibleClaimType = selectedPolicy ? getCompatibleClaimType(selectedPolicy.policyTypeName) : null;
  const isUnsupportedPolicy = Boolean(selectedPolicy && !compatibleClaimType);

  const handlePolicyChange = (e) => {
    const policyId = e.target.value;
    const policy = policies.find((p) => p.id === policyId);
    const compatible = policy ? getCompatibleClaimType(policy.policyTypeName) : null;

    setFormData((prev) => ({
      ...prev,
      policyId,
      claimType: compatible ? String(compatible.value) : '',
    }));

    if (errors.policyId) {
      setErrors((prev) => ({ ...prev, policyId: null }));
    }
    if (errors.claimType) {
      setErrors((prev) => ({ ...prev, claimType: null }));
    }
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: null }));
    }
  };

  const validate = () => {
    const newErrors = {};

    if (!formData.policyId) {
      newErrors.policyId = 'Please select a policy.';
    } else if (isUnsupportedPolicy) {
      newErrors.policyId = 'This policy type is not currently supported for claim creation.';
    }

    if (formData.claimType === '' && !isUnsupportedPolicy) {
      newErrors.claimType = 'Please select a claim type.';
    }

    if (!formData.incidentDate) {
      newErrors.incidentDate = 'Incident date is required.';
    } else {
      const incidentDate = new Date(formData.incidentDate);
      if (incidentDate > new Date()) {
        newErrors.incidentDate = 'Incident date cannot be in the future.';
      }
    }

    if (!formData.incidentLocation.trim()) {
      newErrors.incidentLocation = 'Incident location is required.';
    }

    if (!formData.description.trim()) {
      newErrors.description = 'Description is required.';
    }

    const amount = Number(formData.claimedAmount);
    if (!formData.claimedAmount || amount <= 0) {
      newErrors.claimedAmount = 'Claimed amount must be greater than zero.';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitError(null);

    if (!validate()) return;

    setSubmitting(true);
    try {
      const payload = {
        policyId: formData.policyId,
        claimType: Number(formData.claimType),
        incidentDate: new Date(formData.incidentDate).toISOString(),
        incidentLocation: formData.incidentLocation.trim(),
        description: formData.description.trim(),
        claimedAmount: Number(formData.claimedAmount),
      };
      const created = await createClaim(payload);
      setCreatedClaim(created);
    } catch (err) {
      setSubmitError(err.message || 'Failed to create claim.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitClaim = async () => {
    if (!createdClaim?.id) return;
    setSubmittingClaim(true);
    setSubmitError(null);
    try {
      await submitClaim(createdClaim.id);
      if (onCreated) onCreated();
      navigate(`/claims/${createdClaim.id}`);
    } catch (err) {
      setSubmitError(err.message || 'Failed to submit claim.');
    } finally {
      setSubmittingClaim(false);
    }
  };

  // Success state — claim created, offer to submit
  if (createdClaim) {
    return (
      <div className="fade-in" style={{ maxWidth: '600px', margin: '0 auto', padding: '24px' }}>
        <div className="card" style={{ padding: '32px', textAlign: 'center' }}>
          <div style={{ fontSize: '3rem', marginBottom: '16px' }}>✅</div>
          <h3 style={{ marginBottom: '8px' }}>Claim Created Successfully</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '8px' }}>
            Claim <strong style={{ color: 'var(--color-primary-light)' }}>{createdClaim.claimNumber}</strong> has been saved as a <strong>Draft</strong>.
          </p>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '24px', fontSize: '0.9rem' }}>
            Submit it now to start the AI-assisted review pipeline (Document Verification → Risk Assessment → Payout Processing).
          </p>

          {submitError && (
            <div style={{ padding: '12px', backgroundColor: 'rgba(220, 38, 38, 0.1)', border: '1px solid rgba(220, 38, 38, 0.3)', borderRadius: '8px', color: '#ef4444', marginBottom: '16px' }}>
              {submitError}
            </div>
          )}

          <div style={{ display: 'flex', gap: '12px', justifyContent: 'center' }}>
            <button
              className="btn btn--primary"
              onClick={handleSubmitClaim}
              disabled={submittingClaim}
            >
              {submittingClaim ? 'Submitting…' : '🚀 Submit Claim'}
            </button>
            <button
              className="btn btn--secondary"
              onClick={() => navigate(`/claims/${createdClaim.id}`)}
            >
              View Draft
            </button>
            <button
              className="btn btn--secondary"
              onClick={onBack}
            >
              Back to Claims
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="fade-in" style={{ maxWidth: '640px', margin: '0 auto', padding: '24px' }}>
      <button
        onClick={onBack}
        style={{ marginBottom: '16px', cursor: 'pointer', background: 'none', border: 'none', color: 'var(--color-primary-light)', fontSize: '0.95rem' }}
      >
        ← Back to Claims
      </button>
      <h2>Submit a Claim</h2>
      <p style={{ color: 'var(--color-text-secondary)', marginBottom: '24px', fontSize: '0.9rem' }}>
        File a new insurance claim against one of your active policies.
      </p>

      {submitError && (
        <div style={{ padding: '12px', backgroundColor: 'rgba(220, 38, 38, 0.1)', border: '1px solid rgba(220, 38, 38, 0.3)', borderRadius: '8px', color: '#ef4444', marginBottom: '16px' }}>
          {submitError}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        {/* Policy Selection */}
        <div className="form-group" style={{ marginBottom: '16px' }}>
          <label className="form-label" htmlFor="policyId">Policy *</label>
          <select
            id="policyId"
            name="policyId"
            value={formData.policyId}
            onChange={handlePolicyChange}
            disabled={loadingPolicies}
            className={`form-select ${errors.policyId ? 'form-input--error' : ''}`}
          >
            <option value="">
              {loadingPolicies ? 'Loading your policies…' : 'Select a policy…'}
            </option>
            {policies.map((p) => (
              <option key={p.id} value={p.id}>
                {p.policyNumber} — {p.policyTypeName || 'Policy'} (Coverage: ${Number(p.coverageLimit).toLocaleString()})
              </option>
            ))}
          </select>
          {isUnsupportedPolicy && (
            <div className="form-error" style={{ color: '#ef4444', fontSize: '0.85rem', marginTop: '6px' }}>
              This policy type is not currently supported for claim creation.
            </div>
          )}
          {errors.policyId && !isUnsupportedPolicy && <div className="form-error">{errors.policyId}</div>}
          {!loadingPolicies && policies.length === 0 && (
            <div style={{ color: 'var(--color-warning)', fontSize: '0.85rem', marginTop: '6px' }}>
              ⚠️ You don't have any active policies. Create a policy first.
            </div>
          )}
        </div>

        {/* Claim Type */}
        <div className="form-group" style={{ marginBottom: '16px' }}>
          <label className="form-label" htmlFor="claimType">Claim Type *</label>
          <select
            id="claimType"
            name="claimType"
            value={formData.claimType}
            onChange={handleChange}
            disabled={!selectedPolicy || isUnsupportedPolicy}
            className={`form-select ${errors.claimType ? 'form-input--error' : ''}`}
          >
            {!selectedPolicy ? (
              <option value="">Select a policy first…</option>
            ) : isUnsupportedPolicy ? (
              <option value="">Unsupported policy</option>
            ) : compatibleClaimType ? (
              <option value={compatibleClaimType.value}>
                {compatibleClaimType.label}
              </option>
            ) : (
              <option value="">Select claim type…</option>
            )}
          </select>
          {errors.claimType && <div className="form-error">{errors.claimType}</div>}
        </div>

        {/* Incident Date & Location */}
        <div style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
          <div style={{ flex: 1 }}>
            <label className="form-label">Incident Date *</label>
            <input
              type="date"
              name="incidentDate"
              value={formData.incidentDate}
              onChange={handleChange}
              max={new Date().toISOString().split('T')[0]}
              className={`form-input ${errors.incidentDate ? 'form-input--error' : ''}`}
            />
            {errors.incidentDate && <div className="form-error">{errors.incidentDate}</div>}
          </div>
          <div style={{ flex: 1 }}>
            <label className="form-label">Incident Location *</label>
            <input
              type="text"
              name="incidentLocation"
              value={formData.incidentLocation}
              onChange={handleChange}
              placeholder="e.g., Colombo, Sri Lanka"
              className={`form-input ${errors.incidentLocation ? 'form-input--error' : ''}`}
            />
            {errors.incidentLocation && <div className="form-error">{errors.incidentLocation}</div>}
          </div>
        </div>

        {/* Claimed Amount */}
        <div className="form-group" style={{ marginBottom: '16px' }}>
          <label className="form-label">Claimed Amount ($) *</label>
          <input
            type="number"
            name="claimedAmount"
            value={formData.claimedAmount}
            onChange={handleChange}
            placeholder="e.g., 15000"
            min="0"
            step="0.01"
            className={`form-input ${errors.claimedAmount ? 'form-input--error' : ''}`}
          />
          {errors.claimedAmount && <div className="form-error">{errors.claimedAmount}</div>}
        </div>

        {/* Description */}
        <div className="form-group" style={{ marginBottom: '24px' }}>
          <label className="form-label">Description *</label>
          <textarea
            name="description"
            value={formData.description}
            onChange={handleChange}
            rows={4}
            placeholder="Describe the incident — what happened, when, where, and the extent of the damage or loss…"
            className={`form-input ${errors.description ? 'form-input--error' : ''}`}
            style={{ resize: 'vertical' }}
          />
          {errors.description && <div className="form-error">{errors.description}</div>}
        </div>

        {/* Submit Button */}
        <button
          type="submit"
          className="btn btn--primary"
          disabled={submitting || isUnsupportedPolicy}
          style={{ width: '100%', padding: '12px', fontSize: '1rem' }}
        >
          {submitting ? 'Creating Claim…' : '📋 Create Claim'}
        </button>
      </form>
    </div>
  );
}

export default ClaimCreate;
