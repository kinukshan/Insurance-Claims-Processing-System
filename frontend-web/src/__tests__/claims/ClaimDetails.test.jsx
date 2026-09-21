/**
 * ClaimDetails component tests — Component B (Member 2).
 * Tests detail view rendering, role-aware Document Verification Agent panel,
 * Gemini AI reasoning metadata, fallback notices, and service integration.
 */

import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import ClaimDetails from '../../pages/claims/ClaimDetails';
import {
  getClaim,
  uploadDocument,
  validateCoverage,
  startWorkflow,
  deleteClaim,
  withdrawClaim,
  deleteDocument,
} from '../../services/claimService';

// ── Mock react-router-dom ──
const mockNavigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useParams: () => ({ id: '11111111-1111-1111-1111-111111111111' }),
  useNavigate: () => mockNavigate,
}));

// ── Mock AuthContext ──
let mockAuthRole = 'ClaimsAdjuster';
let mockAuthUserId = '33333333-3333-3333-3333-333333333333';
vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    role: mockAuthRole,
    user: { email: 'test@example.com', role: mockAuthRole, userId: mockAuthUserId },
    isAuthenticated: true,
  }),
}));

// ── Mock claimService ──
vi.mock('../../services/claimService', () => ({
  getClaim: vi.fn(),
  uploadDocument: vi.fn(),
  validateCoverage: vi.fn(),
  startWorkflow: vi.fn(),
  deleteClaim: vi.fn(),
  withdrawClaim: vi.fn(),
  deleteDocument: vi.fn(),
}));

const sampleClaim = {
  id: '11111111-1111-1111-1111-111111111111',
  claimNumber: 'CLM-2026-0001',
  claimType: 'Auto',
  status: 'Submitted',
  description: 'Rear-end collision at traffic light',
  claimedAmount: 5000,
  incidentDate: '2026-09-01T10:00:00Z',
  incidentLocation: 'Main St & 5th Ave',
  policyId: '22222222-2222-2222-2222-222222222222',
  policyHolderId: '33333333-3333-3333-3333-333333333333',
  submittedAt: '2026-09-02T12:00:00Z',
  createdAt: '2026-09-01T11:00:00Z',
  updatedAt: '2026-09-02T12:00:00Z',
  documents: [
    {
      id: 'd1',
      fileName: 'police_report.pdf',
      documentType: 'Police Report',
      fileSize: 204800,
      uploadedAt: '2026-09-02T12:00:00Z',
      verificationStatus: 'Verified',
    },
  ],
};

describe('ClaimDetails Component Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthRole = 'ClaimsAdjuster';
    mockAuthUserId = '33333333-3333-3333-3333-333333333333';
    getClaim.mockResolvedValue(sampleClaim);
  });

  // ── Prior Existing Tests Preserved ──
  it('preserves prior basic exports and function checks', () => {
    expect(typeof getClaim).toBe('function');
    expect(typeof uploadDocument).toBe('function');
    expect(typeof validateCoverage).toBe('function');
    expect(typeof startWorkflow).toBe('function');
    expect(typeof deleteClaim).toBe('function');
    expect(typeof withdrawClaim).toBe('function');
    expect(typeof deleteDocument).toBe('function');
  });

  it('validates expected document types list', () => {
    const EXPECTED_DOC_TYPES = [
      'Police Report', 'Photos of Damage', 'Repair Estimate',
      'Medical Report', 'Supporting Document',
    ];
    EXPECTED_DOC_TYPES.forEach((type) => {
      expect(typeof type).toBe('string');
      expect(type.length).toBeGreaterThan(0);
    });
  });

  it('renders claim details successfully', async () => {
    render(<ClaimDetails />);
    await waitFor(() => {
      expect(screen.getByText('CLM-2026-0001')).toBeDefined();
      expect(screen.getByText('Rear-end collision at traffic light')).toBeDefined();
      expect(screen.getByText('police_report.pdf')).toBeDefined();
    });
  });

  // ── Test 1: Complete Verification State (Green) ──
  it('1. renders complete verification result with success state', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/All required documents are present and verified/i)).toBeDefined();
      expect(screen.getByText('Complete')).toBeDefined();
    });
  });

  // ── Test 2: Missing Documents Section ──
  it('2. displays missing documents section when required items are missing', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: ['Repair Estimate', 'Driver License'],
      inconsistencies: [],
      warnings: [],
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Missing Documents \(2\)/i)).toBeDefined();
      const missingSec = container.querySelector('#missing-documents-section');
      expect(missingSec).not.toBeNull();
      expect(within(missingSec).getByText('Repair Estimate')).toBeDefined();
      expect(within(missingSec).getByText('Driver License')).toBeDefined();
    });
  });

  // ── Test 3: Inconsistencies Section ──
  it('3. displays inconsistencies with field, description, and severity badges', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: [],
      inconsistencies: [
        { field: 'incident_date', description: 'Date mismatch between report and claim', severity: 'error' },
        { field: 'claimed_amount', description: 'Amount slightly exceeds initial repair estimate', severity: 'warning' },
      ],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Inconsistencies \(2\)/i)).toBeDefined();
      expect(screen.getByText('incident_date')).toBeDefined();
      expect(screen.getByText('Date mismatch between report and claim')).toBeDefined();
      expect(screen.getByText('error')).toBeDefined();
      expect(screen.getByText('warning')).toBeDefined();
    });
  });

  // ── Test 4: Warnings Section ──
  it('4. displays warnings section when warnings are returned', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: ['Scan resolution is low for page 2', 'Document uploaded after deadline'],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Warnings \(2\)/i)).toBeDefined();
      expect(screen.getByText('Scan resolution is low for page 2')).toBeDefined();
    });
  });

  // ── Test 5: Gemini AI Reasoning Section (Staff View) ──
  it('5. displays Gemini AI analysis and reasoning summary for staff', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      aiProvider: 'google',
      aiModel: 'gemini-2.5-flash',
      reasoningSummary: 'Police report and repair invoice cross-referenced successfully.',
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Gemini AI Analysis \(Staff Only\)/i)).toBeDefined();
      expect(screen.getByText(/Provider: google/i)).toBeDefined();
      expect(screen.getByText(/Model: gemini-2.5-flash/i)).toBeDefined();
      expect(screen.getByText('Police report and repair invoice cross-referenced successfully.')).toBeDefined();
    });
  });

  // ── Test 6: Fallback Behavior Notice ──
  it('6. displays fallback notice when fallbackUsed is true', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: ['Repair Estimate'],
      inconsistencies: [],
      warnings: ['AI service timed out'],
      fallbackUsed: true,
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Fallback Rule-Based Verification was used/i)).toBeDefined();
    });
  });

  // ── Test 7: Empty Arrays Hidden ──
  it('7. hides missing documents, inconsistencies, and warnings sections when arrays are empty', async () => {
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(container.querySelector('#missing-documents-section')).toBeNull();
      expect(container.querySelector('#inconsistencies-section')).toBeNull();
      expect(container.querySelector('#verification-warnings-section')).toBeNull();
    });
  });

  // ── Test 8: Policyholder View ──
  it('8. renders actionable guidance for Policyholder and hides staff Gemini analysis', async () => {
    mockAuthRole = 'Policyholder';
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: ['Repair Estimate'],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      aiProvider: 'google',
      aiModel: 'gemini-2.5-flash',
      reasoningSummary: 'Internal AI confidence 0.88',
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      // Policyholder guidance should be displayed
      expect(screen.getByText(/Action Required on Your Claim/i)).toBeDefined();
      expect(screen.getByText(/Please upload the missing documents/i)).toBeDefined();
      // Staff-only analysis should NOT be rendered for policyholder
      expect(container.querySelector('#staff-gemini-analysis')).toBeNull();
      expect(screen.queryByText(/Gemini AI Analysis/i)).toBeNull();
    });
  });

  // ── Test 9: Staff View ──
  it('9. renders staff-only Gemini analysis for ClaimsAdjuster / Admin', async () => {
    mockAuthRole = 'Admin';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      aiProvider: 'google',
      aiModel: 'gemini-2.5-flash',
      reasoningSummary: 'Comprehensive multi-page doc reasoning verified.',
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(container.querySelector('#staff-gemini-analysis')).not.toBeNull();
      expect(screen.getByText(/Comprehensive multi-page doc reasoning verified/i)).toBeDefined();
    });
  });

  // ── Test 10: snake_case Compatibility ──
  it('10. handles snake_case properties from AI/backend seamlessly', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missing_items: ['Hospital Bills'],
      inconsistencies: [
        { field: 'bill_amount', description: 'Mismatch in hospital totals', severity: 'error' },
      ],
      warnings: ['Legacy document format detected'],
      ai_used: true,
      ai_provider: 'google',
      ai_model: 'gemini-2.5-flash',
      reasoning_summary: 'Processed with snake_case schema from backend agent.',
      fallback_used: false,
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByText(/Verify Documents/i));

    await waitFor(() => {
      expect(screen.getByText(/Missing Documents \(1\)/i)).toBeDefined();
      const missingSec = container.querySelector('#missing-documents-section');
      expect(missingSec).not.toBeNull();
      expect(within(missingSec).getByText('Hospital Bills')).toBeDefined();
      expect(screen.getByText('Mismatch in hospital totals')).toBeDefined();
      expect(screen.getByText('Legacy document format detected')).toBeDefined();
      expect(screen.getByText('Processed with snake_case schema from backend agent.')).toBeDefined();
      expect(screen.getByText(/Provider: google/i)).toBeDefined();
    });
  });

  // ── Safe Delete & Withdraw Tests ──

  it('11. shows Delete Claim button for Draft claim when user is owner policyholder and triggers delete on confirmation', async () => {
    mockAuthRole = 'Policyholder';
    mockAuthUserId = '33333333-3333-3333-3333-333333333333';
    getClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Draft',
    });
    deleteClaim.mockResolvedValueOnce({});
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const deleteBtn = screen.getByRole('button', { name: /delete claim/i });
    expect(deleteBtn).toBeDefined();
    expect(screen.queryByRole('button', { name: /withdraw claim/i })).toBeNull();

    fireEvent.click(deleteBtn);
    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Delete Claim?'));
    expect(deleteClaim).toHaveBeenCalledWith(sampleClaim.id);
    confirmSpy.mockRestore();
  });

  it('12. shows Withdraw Claim button for Submitted claim when user is owner policyholder and triggers withdraw on confirmation', async () => {
    mockAuthRole = 'Policyholder';
    mockAuthUserId = '33333333-3333-3333-3333-333333333333';
    getClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Submitted',
    });
    withdrawClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Withdrawn',
    });
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const withdrawBtn = screen.getByRole('button', { name: /withdraw claim/i });
    expect(withdrawBtn).toBeDefined();
    expect(screen.queryByRole('button', { name: /delete claim/i })).toBeNull();

    fireEvent.click(withdrawBtn);
    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Withdraw Claim?'));
    expect(withdrawClaim).toHaveBeenCalledWith(sampleClaim.id);
    confirmSpy.mockRestore();
  });

  it('13. hides destructive action buttons for finalized claims', async () => {
    mockAuthRole = 'Policyholder';
    mockAuthUserId = '33333333-3333-3333-3333-333333333333';
    getClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Approved',
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(screen.queryByRole('button', { name: /delete claim/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /withdraw claim/i })).toBeNull();
  });

  it('14. hides delete and withdraw buttons for staff roles', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Draft',
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(screen.queryByRole('button', { name: /delete claim/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /withdraw claim/i })).toBeNull();
  });

  it('15. allows document deletion with confirmation for eligible user', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getClaim.mockResolvedValueOnce(sampleClaim);
    deleteDocument.mockResolvedValueOnce({});
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('police_report.pdf')).toBeDefined());

    const deleteDocBtn = screen.getByRole('button', { name: /^delete$/i });
    expect(deleteDocBtn).toBeDefined();

    fireEvent.click(deleteDocBtn);
    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Delete Document?'));
    expect(deleteDocument).toHaveBeenCalledWith(sampleClaim.id, 'd1');
    confirmSpy.mockRestore();
  });

  it('16. displays friendly error message when 403 or 409 is returned', async () => {
    mockAuthRole = 'Policyholder';
    mockAuthUserId = '33333333-3333-3333-3333-333333333333';
    getClaim.mockResolvedValueOnce({ ...sampleClaim, status: 'Draft' });
    deleteClaim.mockRejectedValueOnce({ status: 403, message: 'Forbidden' });
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const deleteBtn = screen.getByRole('button', { name: /delete claim/i });
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(screen.getByText(/You do not have permission to delete this item/i)).toBeDefined();
    });
    confirmSpy.mockRestore();
  });
});
