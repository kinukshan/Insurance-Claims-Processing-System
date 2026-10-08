/**
 * Form Validation Tests
 *
 * Tests the validation rules used in ClaimCreate (claim form validation)
 * and Register (registration form validation).
 *
 * The validate() logic in ClaimCreate checks:
 *   - policyId is required (and not an unsupported policy type)
 *   - claimType is required (when policy is supported)
 *   - incidentDate is required and must not be in the future
 *   - incidentLocation is required (non-whitespace)
 *   - description is required (non-whitespace)
 *   - claimedAmount must be greater than zero
 *
 * The Register component checks:
 *   - password must be at least 8 characters
 *   - password must match confirmPassword
 */

import { describe, it, expect } from 'vitest';
import {
  getCompatibleClaimType,
  isPolicySupportedForClaims,
  normalizePolicyTypeName,
} from '../../utils/policyClaimMapping';

// ────────────────────────────────────────────────────────────────────────────
// Replicate the exact validate() logic from ClaimCreate.jsx so we can test
// it in isolation without rendering the component.
// ────────────────────────────────────────────────────────────────────────────

/**
 * Pure-function mirror of ClaimCreate's validate().
 * @param {object} formData - { policyId, claimType, incidentDate, incidentLocation, description, claimedAmount }
 * @param {boolean} isUnsupportedPolicy - whether the selected policy has no compatible claim type
 * @returns {object} errors keyed by field name
 */
function validateClaimForm(formData, isUnsupportedPolicy = false) {
  const errors = {};

  if (!formData.policyId) {
    errors.policyId = 'Please select a policy.';
  } else if (isUnsupportedPolicy) {
    errors.policyId = 'This policy type is not currently supported for claim creation.';
  }

  if (formData.claimType === '' && !isUnsupportedPolicy) {
    errors.claimType = 'Please select a claim type.';
  }

  if (!formData.incidentDate) {
    errors.incidentDate = 'Incident date is required.';
  } else {
    const incidentDate = new Date(formData.incidentDate);
    if (incidentDate > new Date()) {
      errors.incidentDate = 'Incident date cannot be in the future.';
    }
  }

  if (!formData.incidentLocation.trim()) {
    errors.incidentLocation = 'Incident location is required.';
  }

  if (!formData.description.trim()) {
    errors.description = 'Description is required.';
  }

  const amount = Number(formData.claimedAmount);
  if (!formData.claimedAmount || amount <= 0) {
    errors.claimedAmount = 'Claimed amount must be greater than zero.';
  }

  return errors;
}

/**
 * Mirrors the Register component's client-side validation.
 */
function validateRegistration({ password, confirmPassword }) {
  if (password !== confirmPassword) {
    return 'Passwords do not match.';
  }
  if (password.length < 8) {
    return 'Password must be at least 8 characters.';
  }
  return null;
}

// ────────────────────────────────────────────────────────────────────────────
// Claim Form Validation Tests
// ────────────────────────────────────────────────────────────────────────────

describe('ClaimCreate form validation', () => {
  const validForm = {
    policyId: 'policy-1',
    claimType: '8',
    incidentDate: '2025-06-15',
    incidentLocation: 'Colombo, Sri Lanka',
    description: 'Vehicle was damaged in an accident.',
    claimedAmount: '15000',
  };

  // ── Valid input ──────────────────────────────────────────────────────────

  it('returns no errors for completely valid input', () => {
    const errors = validateClaimForm(validForm);
    expect(Object.keys(errors)).toHaveLength(0);
  });

  // ── Empty required fields ────────────────────────────────────────────────

  it('returns errors for all empty required fields', () => {
    const errors = validateClaimForm({
      policyId: '',
      claimType: '',
      incidentDate: '',
      incidentLocation: '',
      description: '',
      claimedAmount: '',
    });

    expect(errors.policyId).toBe('Please select a policy.');
    expect(errors.claimType).toBe('Please select a claim type.');
    expect(errors.incidentDate).toBe('Incident date is required.');
    expect(errors.incidentLocation).toBe('Incident location is required.');
    expect(errors.description).toBe('Description is required.');
    expect(errors.claimedAmount).toBe('Claimed amount must be greater than zero.');
  });

  // ── policyId validation ──────────────────────────────────────────────────

  it('requires policyId to be selected', () => {
    const errors = validateClaimForm({ ...validForm, policyId: '' });
    expect(errors.policyId).toBe('Please select a policy.');
  });

  it('errors when selected policy type is unsupported', () => {
    const errors = validateClaimForm(
      { ...validForm, policyId: 'policy-unsupported' },
      true, // isUnsupportedPolicy
    );
    expect(errors.policyId).toBe('This policy type is not currently supported for claim creation.');
  });

  // ── claimType validation ─────────────────────────────────────────────────

  it('requires claimType when policy is supported', () => {
    const errors = validateClaimForm({ ...validForm, claimType: '' });
    expect(errors.claimType).toBe('Please select a claim type.');
  });

  it('does not require claimType when policy is unsupported', () => {
    const errors = validateClaimForm(
      { ...validForm, policyId: 'policy-x', claimType: '' },
      true,
    );
    // claimType error should NOT appear when isUnsupportedPolicy is true
    expect(errors.claimType).toBeUndefined();
    // policyId error SHOULD appear instead
    expect(errors.policyId).toBe('This policy type is not currently supported for claim creation.');
  });

  // ── incidentDate validation ──────────────────────────────────────────────

  it('requires incidentDate', () => {
    const errors = validateClaimForm({ ...validForm, incidentDate: '' });
    expect(errors.incidentDate).toBe('Incident date is required.');
  });

  it('rejects a future incident date', () => {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const futureDate = tomorrow.toISOString().split('T')[0];

    const errors = validateClaimForm({ ...validForm, incidentDate: futureDate });
    expect(errors.incidentDate).toBe('Incident date cannot be in the future.');
  });

  it('accepts a past incident date', () => {
    const errors = validateClaimForm({ ...validForm, incidentDate: '2024-01-15' });
    expect(errors.incidentDate).toBeUndefined();
  });

  it('accepts today as a valid incident date', () => {
    const today = new Date().toISOString().split('T')[0];
    const errors = validateClaimForm({ ...validForm, incidentDate: today });
    expect(errors.incidentDate).toBeUndefined();
  });

  // ── incidentLocation validation ──────────────────────────────────────────

  it('requires incidentLocation', () => {
    const errors = validateClaimForm({ ...validForm, incidentLocation: '' });
    expect(errors.incidentLocation).toBe('Incident location is required.');
  });

  it('rejects whitespace-only incidentLocation', () => {
    const errors = validateClaimForm({ ...validForm, incidentLocation: '   ' });
    expect(errors.incidentLocation).toBe('Incident location is required.');
  });

  // ── description validation ───────────────────────────────────────────────

  it('requires description', () => {
    const errors = validateClaimForm({ ...validForm, description: '' });
    expect(errors.description).toBe('Description is required.');
  });

  it('rejects whitespace-only description', () => {
    const errors = validateClaimForm({ ...validForm, description: '   \t\n  ' });
    expect(errors.description).toBe('Description is required.');
  });

  // ── claimedAmount validation ─────────────────────────────────────────────

  it('requires claimedAmount', () => {
    const errors = validateClaimForm({ ...validForm, claimedAmount: '' });
    expect(errors.claimedAmount).toBe('Claimed amount must be greater than zero.');
  });

  it('rejects zero claimedAmount', () => {
    const errors = validateClaimForm({ ...validForm, claimedAmount: '0' });
    expect(errors.claimedAmount).toBe('Claimed amount must be greater than zero.');
  });

  it('rejects negative claimedAmount', () => {
    const errors = validateClaimForm({ ...validForm, claimedAmount: '-500' });
    expect(errors.claimedAmount).toBe('Claimed amount must be greater than zero.');
  });

  it('accepts a positive claimedAmount', () => {
    const errors = validateClaimForm({ ...validForm, claimedAmount: '1' });
    expect(errors.claimedAmount).toBeUndefined();
  });

  it('accepts a large claimedAmount', () => {
    const errors = validateClaimForm({ ...validForm, claimedAmount: '1000000' });
    expect(errors.claimedAmount).toBeUndefined();
  });
});

// ────────────────────────────────────────────────────────────────────────────
// Policy → Claim Type Mapping Validation
// ────────────────────────────────────────────────────────────────────────────

describe('Policy-to-ClaimType mapping validation', () => {
  it('maps Motor Insurance to Motor claim type', () => {
    const result = getCompatibleClaimType('Motor Insurance');
    expect(result).toEqual({ value: 8, label: 'Motor' });
  });

  it('maps Health Insurance to Health claim type', () => {
    const result = getCompatibleClaimType('Health Insurance');
    expect(result).toEqual({ value: 2, label: 'Health' });
  });

  it('maps Home Insurance to Property claim type', () => {
    const result = getCompatibleClaimType('Home Insurance');
    expect(result).toEqual({ value: 5, label: 'Property' });
  });

  it('maps Life Insurance to Life claim type', () => {
    const result = getCompatibleClaimType('Life Insurance');
    expect(result).toEqual({ value: 3, label: 'Life' });
  });

  it('returns null for unsupported policy types', () => {
    expect(getCompatibleClaimType('Pet Insurance')).toBeNull();
    expect(getCompatibleClaimType('Travel Insurance')).toBeNull();
    expect(getCompatibleClaimType('Unknown Type')).toBeNull();
  });

  it('handles case-insensitive aliases via normalizePolicyTypeName', () => {
    expect(normalizePolicyTypeName('motor')).toBe('Motor Insurance');
    expect(normalizePolicyTypeName('auto')).toBe('Motor Insurance');
    expect(normalizePolicyTypeName('Comprehensive Auto')).toBe('Motor Insurance');
    expect(normalizePolicyTypeName('health')).toBe('Health Insurance');
    expect(normalizePolicyTypeName('home')).toBe('Home Insurance');
    expect(normalizePolicyTypeName('property')).toBe('Home Insurance');
    expect(normalizePolicyTypeName('Home / Property Insurance')).toBe('Home Insurance');
    expect(normalizePolicyTypeName('life')).toBe('Life Insurance');
  });

  it('returns null for null/undefined/empty input', () => {
    expect(normalizePolicyTypeName(null)).toBeNull();
    expect(normalizePolicyTypeName(undefined)).toBeNull();
    expect(normalizePolicyTypeName('')).toBeNull();
  });

  it('isPolicySupportedForClaims returns true for supported types', () => {
    expect(isPolicySupportedForClaims('Motor Insurance')).toBe(true);
    expect(isPolicySupportedForClaims('Health Insurance')).toBe(true);
    expect(isPolicySupportedForClaims('Home Insurance')).toBe(true);
    expect(isPolicySupportedForClaims('Life Insurance')).toBe(true);
  });

  it('isPolicySupportedForClaims returns false for unsupported types', () => {
    expect(isPolicySupportedForClaims('Pet Insurance')).toBe(false);
    expect(isPolicySupportedForClaims('Travel Insurance')).toBe(false);
  });
});

// ────────────────────────────────────────────────────────────────────────────
// Registration Form Validation Tests
// ────────────────────────────────────────────────────────────────────────────

describe('Registration form validation', () => {
  it('accepts valid registration data', () => {
    const result = validateRegistration({
      password: 'SecurePass123!',
      confirmPassword: 'SecurePass123!',
    });
    expect(result).toBeNull();
  });

  it('rejects password shorter than 8 characters', () => {
    const result = validateRegistration({
      password: 'short',
      confirmPassword: 'short',
    });
    expect(result).toBe('Password must be at least 8 characters.');
  });

  it('rejects mismatched passwords', () => {
    const result = validateRegistration({
      password: 'SecurePassword1',
      confirmPassword: 'DifferentPassword2',
    });
    expect(result).toBe('Passwords do not match.');
  });

  it('checks password match before length when both fail', () => {
    // In Register.jsx, the mismatch check comes before the length check
    const result = validateRegistration({
      password: 'abc',
      confirmPassword: 'xyz',
    });
    expect(result).toBe('Passwords do not match.');
  });

  it('accepts exactly 8-character password (boundary)', () => {
    const result = validateRegistration({
      password: '12345678',
      confirmPassword: '12345678',
    });
    expect(result).toBeNull();
  });

  it('rejects 7-character password (boundary)', () => {
    const result = validateRegistration({
      password: '1234567',
      confirmPassword: '1234567',
    });
    expect(result).toBe('Password must be at least 8 characters.');
  });
});
