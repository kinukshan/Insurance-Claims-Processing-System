/**
 * Centralized UI mapping for Policy Types to Claim Types.
 * UX helper only — backend rules remain authoritative.
 */

export const CLAIM_TYPES = [
  { value: 0, label: 'Auto' },
  { value: 1, label: 'Home' },
  { value: 2, label: 'Health' },
  { value: 3, label: 'Life' },
  { value: 4, label: 'Travel' },
  { value: 5, label: 'Property' },
  { value: 6, label: 'Liability' },
  { value: 7, label: 'Other' },
  { value: 8, label: 'Motor' },
];

/**
 * Normalizes a policy type name (case-insensitive, trimmed, alias-aware).
 */
export function normalizePolicyTypeName(name) {
  if (!name || typeof name !== 'string') return null;
  const trimmed = name.trim();
  const lower = trimmed.toLowerCase();

  if (lower === 'motor insurance' || lower === 'motor' || lower === 'auto' || lower === 'auto insurance' || lower === 'comprehensive auto') {
    return 'Motor Insurance';
  }
  if (lower === 'health insurance' || lower === 'health') {
    return 'Health Insurance';
  }
  if (
    lower === 'home insurance' ||
    lower === 'home / property insurance' ||
    lower === 'property insurance' ||
    lower === 'home' ||
    lower === 'property'
  ) {
    return 'Home Insurance';
  }
  if (lower === 'life insurance' || lower === 'life') {
    return 'Life Insurance';
  }

  return trimmed;
}

/**
 * Returns the primary modern claim type object ({ value, label }) for a policy type,
 * or null if the policy type is unsupported for claim creation.
 */
export function getCompatibleClaimType(policyTypeName) {
  const normalized = normalizePolicyTypeName(policyTypeName);
  switch (normalized) {
    case 'Motor Insurance':
      return { value: 8, label: 'Motor' };
    case 'Health Insurance':
      return { value: 2, label: 'Health' };
    case 'Home Insurance':
      return { value: 5, label: 'Property' };
    case 'Life Insurance':
      return { value: 3, label: 'Life' };
    default:
      // Fail closed: unsupported policy types must return null, never default to 'Other'
      return null;
  }
}

/**
 * Returns whether a policy type is supported for claim creation.
 */
export function isPolicySupportedForClaims(policyTypeName) {
  return getCompatibleClaimType(policyTypeName) !== null;
}

/**
 * Authoritative fixed deductible amounts by insurance type.
 */
export const FIXED_DEDUCTIBLES = {
  'Motor Insurance': 10000,
  'Health Insurance': 5000,
  'Home Insurance': 15000,
  'Life Insurance': 0,
};

/**
 * Returns the fixed deductible amount for a given policy type name,
 * or null if unrecognized.
 */
export function getFixedDeductible(policyTypeName) {
  const normalized = normalizePolicyTypeName(policyTypeName);
  if (normalized && FIXED_DEDUCTIBLES[normalized] !== undefined) {
    return FIXED_DEDUCTIBLES[normalized];
  }
  return null;
}

/**
 * Formats a deductible amount as a currency string (e.g., "$10,000", "$0").
 */
export function formatDeductible(amount) {
  if (amount == null || isNaN(amount)) return '$0';
  return `$${Number(amount).toLocaleString('en-US')}`;
}
