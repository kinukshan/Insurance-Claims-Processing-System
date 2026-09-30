import React from 'react';
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import PayoutBreakdown from '../../components/payout/PayoutBreakdown';
import {
  getDeductiblePercentage,
  formatPolicyDeductible,
  formatCurrency,
  DEDUCTIBLE_PERCENTAGES,
} from '../../utils/policyClaimMapping';

describe('Percentage Deductible Business Rules & Matrix', () => {
  it('correctly maps confirmed percentage deductibles for all insurance products', () => {
    expect(getDeductiblePercentage('Motor Insurance')).toBe(5);
    expect(getDeductiblePercentage('Health Insurance')).toBe(10);
    expect(getDeductiblePercentage('Home Insurance')).toBe(10);
    expect(getDeductiblePercentage('Home / Property Insurance')).toBe(10);
    expect(getDeductiblePercentage('Life Insurance')).toBe(0);
  });

  it('formats policy deductibles correctly for percentage and historical fixed policies', () => {
    expect(formatPolicyDeductible({ deductiblePercentage: 5, deductible: 1000 })).toBe('5% deductible');
    expect(formatPolicyDeductible({ deductiblePercentage: 10, deductible: 2000 })).toBe('10% deductible');
    expect(formatPolicyDeductible({ deductiblePercentage: 0, deductible: 0 })).toBe('0% deductible');
    expect(formatPolicyDeductible({ deductiblePercentage: null, deductible: 10000 })).toBe('LKR 10,000');
  });

  describe('PayoutBreakdown Display Matrix', () => {
    it('displays Motor Insurance 5% deductible breakdown (LKR 20,000 approved -> LKR 1,000 ded -> LKR 19,000 payout)', () => {
      const payout = {
        approvedClaimAmount: 20000,
        coverageLimit: 100000,
        eligibleAmount: 20000,
        deductiblePercentage: 5,
        deductible: 1000,
        finalPayout: 19000,
      };

      render(<PayoutBreakdown payout={payout} />);

      expect(screen.getByText('Approved Claim Amount')).toBeInTheDocument();
      expect(screen.getAllByText('LKR 20,000.00').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('LKR 100,000.00')).toBeInTheDocument();
      expect(screen.getByText('Deductible Percentage')).toBeInTheDocument();
      expect(screen.getByText('5%')).toBeInTheDocument();
      expect(screen.getByText('Calculated Deductible Amount')).toBeInTheDocument();
      expect(screen.getByText('− LKR 1,000.00')).toBeInTheDocument();
      expect(screen.getByText('LKR 19,000.00')).toBeInTheDocument();
    });

    it('displays Health Insurance 10% deductible breakdown (LKR 20,000 approved -> LKR 2,000 ded -> LKR 18,000 payout)', () => {
      const payout = {
        approvedClaimAmount: 20000,
        coverageLimit: 100000,
        eligibleAmount: 20000,
        deductiblePercentage: 10,
        deductible: 2000,
        finalPayout: 18000,
      };

      render(<PayoutBreakdown payout={payout} />);

      expect(screen.getByText('10%')).toBeInTheDocument();
      expect(screen.getByText('− LKR 2,000.00')).toBeInTheDocument();
      expect(screen.getByText('LKR 18,000.00')).toBeInTheDocument();
    });

    it('displays Home Insurance 10% deductible breakdown (LKR 20,000 approved -> LKR 2,000 ded -> LKR 18,000 payout)', () => {
      const payout = {
        approvedClaimAmount: 20000,
        coverageLimit: 100000,
        eligibleAmount: 20000,
        deductiblePercentage: 10,
        deductible: 2000,
        finalPayout: 18000,
      };

      render(<PayoutBreakdown payout={payout} />);

      expect(screen.getByText('10%')).toBeInTheDocument();
      expect(screen.getByText('− LKR 2,000.00')).toBeInTheDocument();
      expect(screen.getByText('LKR 18,000.00')).toBeInTheDocument();
    });

    it('displays Life Insurance 0% deductible breakdown (LKR 20,000 approved -> LKR 0 ded -> LKR 20,000 payout)', () => {
      const payout = {
        approvedClaimAmount: 20000,
        coverageLimit: 100000,
        eligibleAmount: 20000,
        deductiblePercentage: 0,
        deductible: 0,
        finalPayout: 20000,
      };

      render(<PayoutBreakdown payout={payout} />);

      expect(screen.getByText('0%')).toBeInTheDocument();
      expect(screen.getByText('− LKR 0.00')).toBeInTheDocument();
      expect(screen.getAllByText('LKR 20,000.00').length).toBe(3); // approved, eligible, final
    });

    it('displays coverage capping correctly (LKR 80,000 approved with LKR 50,000 limit -> eligible LKR 50,000 -> 5% = LKR 2,500 ded -> LKR 47,500 payout)', () => {
      const payout = {
        approvedClaimAmount: 80000,
        coverageLimit: 50000,
        eligibleAmount: 50000,
        deductiblePercentage: 5,
        deductible: 2500,
        finalPayout: 47500,
      };

      render(<PayoutBreakdown payout={payout} />);

      expect(screen.getByText('LKR 80,000.00')).toBeInTheDocument();
      expect(screen.getAllByText('LKR 50,000.00').length).toBe(2); // limit and eligible
      expect(screen.getByText('5%')).toBeInTheDocument();
      expect(screen.getByText('− LKR 2,500.00')).toBeInTheDocument();
      expect(screen.getByText('LKR 47,500.00')).toBeInTheDocument();
    });

    it('preserves historical fixed deductible presentation when deductiblePercentage is null', () => {
      const payout = {
        approvedClaimAmount: 20000,
        coverageLimit: 100000,
        eligibleAmount: 20000,
        deductiblePercentage: null,
        deductible: 1000,
        finalPayout: 19000,
      };

      const { container } = render(<PayoutBreakdown payout={payout} />);

      expect(screen.queryByText('Deductible Percentage')).not.toBeInTheDocument();
      expect(screen.getByText('Deductible')).toBeInTheDocument();
      expect(screen.getByText('− LKR 1,000.00')).toBeInTheDocument();
      expect(screen.getByText('LKR 19,000.00')).toBeInTheDocument();

      // Ensure no double currency symbols or dollar signs appear
      expect(container.textContent).not.toMatch(/LKR\s*\$/);
      expect(container.textContent).not.toMatch(/\$\d/);
    });
  });

  describe('formatCurrency helper and anti-double-symbol guarantees', () => {
    it('formats raw numeric amounts with two decimal places', () => {
      expect(formatCurrency(275000)).toBe('LKR 275,000.00');
      expect(formatCurrency(100000)).toBe('LKR 100,000.00');
      expect(formatCurrency(20000.5)).toBe('LKR 20,000.50');
      expect(formatCurrency(0)).toBe('LKR 0.00');
    });

    it('safely handles legacy strings containing dollar signs without producing LKR $', () => {
      expect(formatCurrency('$275,000')).toBe('LKR 275,000.00');
      expect(formatCurrency('$100,000.00')).toBe('LKR 100,000.00');
      expect(formatCurrency('LKR $275,000')).toBe('LKR 275,000.00');
      expect(formatCurrency('LKR 275,000.00')).toBe('LKR 275,000.00');
    });

    it('never produces dollar signs or LKR $ in formatted output', () => {
      const formatted = formatCurrency('$275,000');
      expect(formatted).not.toContain('$');
      expect(formatted).not.toMatch(/LKR\s*\$/);
      expect(formatted.startsWith('LKR ')).toBe(true);
    });
  });
});
