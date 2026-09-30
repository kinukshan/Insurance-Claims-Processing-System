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
 * Authoritative percentage deductibles by insurance type:
 * Motor: 5%
 * Health: 10%
 * Home / Property: 10%
 * Life: 0%
 */
export const DEDUCTIBLE_PERCENTAGES = {
  'Motor Insurance': 5,
  'Health Insurance': 10,
  'Home Insurance': 10,
  'Life Insurance': 0,
};

/**
 * Returns the configured deductible percentage for a given policy type name,
 * or null if unrecognized.
 */
export function getDeductiblePercentage(policyTypeName) {
  const normalized = normalizePolicyTypeName(policyTypeName);
  if (normalized && DEDUCTIBLE_PERCENTAGES[normalized] !== undefined) {
    return DEDUCTIBLE_PERCENTAGES[normalized];
  }
  return null;
}

/**
 * Historical fixed deductible amounts by insurance type.
 * Preserved for legacy policies created under fixed deductible terms.
 */
export const FIXED_DEDUCTIBLES = {
  'Motor Insurance': 10000,
  'Health Insurance': 5000,
  'Home Insurance': 15000,
  'Life Insurance': 0,
};

/**
 * Returns the historical fixed deductible amount for a given policy type name,
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
 * Helper to extract raw numeric amount from number or string.
 * Strips legacy currency symbols ($, USD, LKR), commas, and whitespace
 * to prevent double symbols (e.g. "LKR $").
 */
function toNumericAmount(val) {
  if (val == null) return null;
  if (typeof val === 'number') {
    return isNaN(val) ? null : val;
  }
  if (typeof val === 'string') {
    const cleaned = val.replace(/[$A-Za-z,\s]/g, '').trim();
    if (!cleaned) return null;
    const parsed = Number(cleaned);
    return isNaN(parsed) ? null : parsed;
  }
  return null;
}

/**
 * Formats a deductible amount as a currency string (e.g., "LKR 10,000", "LKR 0").
 */
export function formatDeductible(amount) {
  const num = toNumericAmount(amount);
  if (num == null) return 'LKR 0';
  return `LKR ${num.toLocaleString('en-US')}`;
}

/**
 * Formats a currency amount in LKR with 2 decimal places (e.g. "LKR 20,000.00").
 * Strips any accidental symbols to ensure only a single "LKR " prefix is rendered.
 */
export function formatCurrency(amount) {
  const num = toNumericAmount(amount);
  if (num == null) return 'LKR 0.00';
  return `LKR ${num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

/**
 * Formats a policy's deductible based on its terms:
 * Percentage if percentage-based (e.g. "5% deductible"), or currency for legacy fixed policies.
 */
export function formatPolicyDeductible(policy) {
  if (!policy) return '0%';
  if (policy.deductiblePercentage != null) {
    return `${policy.deductiblePercentage}% deductible`;
  }
  return formatDeductible(policy.deductible);
}
