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

// ── Mock riskService ──
vi.mock('../../services/riskService', () => ({
  assessClaim: vi.fn(),
  getAssessment: vi.fn(),
  getAllAssessments: vi.fn(),
}));

// ── Mock payoutService ──
vi.mock('../../services/payoutService', () => ({
  getPayoutByClaim: vi.fn(),
  calculatePayout: vi.fn(),
}));

import { assessClaim, getAssessment } from '../../services/riskService';
import { getPayoutByClaim } from '../../services/payoutService';

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
    assessClaim.mockReset();
    getAssessment.mockReset();
    getAssessment.mockResolvedValue(null);
    getPayoutByClaim.mockReset();
    getPayoutByClaim.mockResolvedValue(null);
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

  // ── Document Verification Invalidation & Re-verification Tests ──

  it('17. successful document deletion invalidates previous COMPLETE verification and hides Gemini reasoning', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      aiProvider: 'google',
      aiModel: 'gemini-2.5-flash',
      reasoningSummary: 'All required documents verified successfully.',
    });
    deleteDocument.mockResolvedValueOnce({});
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // 1. Run initial verification -> COMPLETE
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => {
      expect(screen.getByText(/All required documents are present and verified/i)).toBeDefined();
      expect(container.querySelector('#staff-gemini-analysis')).not.toBeNull();
      expect(screen.getByText('All required documents verified successfully.')).toBeDefined();
    });

    // 2. Delete document
    const deleteBtn = screen.getByRole('button', { name: /^delete$/i });
    fireEvent.click(deleteBtn);

    // 3. Old verification and Gemini reasoning disappear; re-verification notice appears
    await waitFor(() => {
      expect(container.querySelector('#verification-result-panel')).toBeNull();
      expect(container.querySelector('#staff-gemini-analysis')).toBeNull();
      expect(screen.queryByText(/All required documents are present and verified/i)).toBeNull();
      expect(screen.queryByText('All required documents verified successfully.')).toBeNull();
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
      expect(screen.getByText(/Documents Changed/i)).toBeDefined();
      expect(screen.getByText(/Needs Re-Verification/i)).toBeDefined();
      expect(screen.getByText(/The claim documents have changed since the last verification/i)).toBeDefined();
    });

    // 4. Verify startWorkflow was NOT automatically called on document deletion
    expect(startWorkflow).toHaveBeenCalledTimes(1);

    confirmSpy.mockRestore();
  });

  it('18. re-verifying documents after document deletion displays fresh verification result and clears re-verification notice', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
    });
    deleteDocument.mockResolvedValueOnce({});
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // Run initial verification
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Delete document -> invalidation
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));
    await waitFor(() => expect(container.querySelector('#reverification-notice')).not.toBeNull());

    // Fresh verification returns Action Required (missing Photos of Damage)
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: ['Photos of Damage'],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      reasoningSummary: 'Photos of damage are missing.',
    });

    // Adjuster clicks Verify Documents again
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));

    await waitFor(() => {
      // Re-verification notice is gone
      expect(container.querySelector('#reverification-notice')).toBeNull();
      expect(screen.queryByText(/Needs Re-Verification/i)).toBeNull();
      // Fresh verification result is displayed
      const verifPanel = container.querySelector('#verification-result-panel');
      expect(verifPanel).not.toBeNull();
      expect(screen.getByText('Action Required')).toBeDefined();
      const missingSec = container.querySelector('#missing-documents-section');
      expect(missingSec).not.toBeNull();
      expect(within(missingSec).getByText('Photos of Damage')).toBeDefined();
    });

    expect(startWorkflow).toHaveBeenCalledTimes(2);
    confirmSpy.mockRestore();
  });

  it('19. successful document upload after verification invalidates result and does not auto-call verifyDocuments', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      reasoningSummary: 'Initial docs complete.',
    });
    uploadDocument.mockResolvedValueOnce({ id: 'd2', fileName: 'new_doc.pdf' });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // Run verification
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Upload a new document
    const file = new File(['dummy content'], 'new_doc.pdf', { type: 'application/pdf' });
    const fileInput = container.querySelector('#doc-file');
    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadBtn = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(uploadBtn);

    await waitFor(() => {
      expect(uploadDocument).toHaveBeenCalledWith(sampleClaim.id, file, 'Supporting Document');
      // Old verification result is cleared
      expect(container.querySelector('#verification-result-panel')).toBeNull();
      expect(screen.queryByText('Complete')).toBeNull();
      expect(screen.queryByText('Initial docs complete.')).toBeNull();
      // Re-verification notice is displayed
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
      expect(screen.getByText(/Needs Re-Verification/i)).toBeDefined();
    });

    // Verify startWorkflow was NOT automatically called on document upload
    expect(startWorkflow).toHaveBeenCalledTimes(1);
  });

  it('20. failed document deletion does not invalidate still-current verification result', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
    });
    deleteDocument.mockRejectedValueOnce(new Error('Network error deleting document'));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // Run verification
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Attempt delete that fails
    const deleteBtn = screen.getByRole('button', { name: /^delete$/i });
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(screen.getByText(/Network error deleting document/i)).toBeDefined();
      // Current verification result remains intact
      expect(container.querySelector('#verification-result-panel')).not.toBeNull();
      expect(screen.getByText('Complete')).toBeDefined();
      // Re-verification notice is NOT displayed
      expect(container.querySelector('#reverification-notice')).toBeNull();
    });

    confirmSpy.mockRestore();
  });

  it('21. cancelled document deletion prompt does not invalidate still-current verification result', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
    });
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Click delete but cancel confirm prompt
    const deleteBtn = screen.getByRole('button', { name: /^delete$/i });
    fireEvent.click(deleteBtn);

    expect(deleteDocument).not.toHaveBeenCalled();
    expect(container.querySelector('#verification-result-panel')).not.toBeNull();
    expect(screen.getByText('Complete')).toBeDefined();
    expect(container.querySelector('#reverification-notice')).toBeNull();

    confirmSpy.mockRestore();
  });

  it('22. failed document upload does not invalidate still-current verification result', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
    });
    uploadDocument.mockRejectedValueOnce(new Error('Storage upload failed'));

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // Run verification
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Attempt upload that fails
    const file = new File(['content'], 'fail.pdf', { type: 'application/pdf' });
    const fileInput = container.querySelector('#doc-file');
    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadBtn = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(uploadBtn);

    await waitFor(() => {
      expect(screen.getByText(/Storage upload failed/i)).toBeDefined();
      // Current verification result remains intact
      expect(container.querySelector('#verification-result-panel')).not.toBeNull();
      expect(screen.getByText('Complete')).toBeDefined();
      expect(container.querySelector('#reverification-notice')).toBeNull();
    });
  });

  it('23. documents changed -> Verify Documents request fails -> Needs Re-Verification notice remains visible and no stale verification result reappears', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      reasoningSummary: 'Old reasoning summary',
    });
    deleteDocument.mockResolvedValueOnce({});
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // 1. Initial verification COMPLETE
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // 2. Document deleted -> State C (Needs Re-Verification)
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));
    await waitFor(() => {
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
      expect(container.querySelector('#verification-result-panel')).toBeNull();
    });

    // 3. Re-verification request fails (e.g. backend / network 500 error)
    startWorkflow.mockRejectedValueOnce(new Error('AI service connection timeout'));

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));

    await waitFor(() => {
      // Error message is displayed
      expect(screen.getByText(/AI service connection timeout/i)).toBeDefined();
      // Needs Re-Verification notice REMAINS visible
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
      expect(screen.getByText(/Needs Re-Verification/i)).toBeDefined();
      // No stale verification panel reappears
      expect(container.querySelector('#verification-result-panel')).toBeNull();
      expect(screen.queryByText('Complete')).toBeNull();
      expect(screen.queryByText('Old reasoning summary')).toBeNull();
    });

    confirmSpy.mockRestore();
  });

  it('24. document mutation before any verification was run does not show reverification notice (State A)', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    uploadDocument.mockResolvedValueOnce({ id: 'd2', fileName: 'first_upload.pdf' });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    // No verification run yet
    expect(container.querySelector('#verification-result-panel')).toBeNull();
    expect(container.querySelector('#reverification-notice')).toBeNull();

    // Upload a document in State A
    const file = new File(['content'], 'first_upload.pdf', { type: 'application/pdf' });
    const fileInput = container.querySelector('#doc-file');
    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadBtn = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(uploadBtn);

    await waitFor(() => {
      expect(uploadDocument).toHaveBeenCalled();
      // Still in State A: neither panel nor reverification notice is shown
      expect(container.querySelector('#verification-result-panel')).toBeNull();
      expect(container.querySelector('#reverification-notice')).toBeNull();
    });
  });

  // ── Tests 25-35: Run Risk Assessment Workflow & Authorization ──

  it('25. run risk assessment button is not rendered for policyholder', async () => {
    mockAuthRole = 'Policyholder';
    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(screen.queryByRole('button', { name: /run risk assessment/i })).toBeNull();
    expect(document.querySelector('#run-risk-assessment-btn')).toBeNull();
  });

  it('26. run risk assessment button is rendered for staff (ClaimsAdjuster, Underwriter, Admin)', async () => {
    for (const staffRole of ['ClaimsAdjuster', 'Underwriter', 'Admin']) {
      mockAuthRole = staffRole;
      const { unmount } = render(<ClaimDetails />);
      await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

      expect(document.querySelector('#run-risk-assessment-btn')).not.toBeNull();
      unmount();
    }
  });

  it('27. run risk assessment button is initially disabled before document verification completes', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const riskBtn = document.querySelector('#run-risk-assessment-btn');
    expect(riskBtn).not.toBeNull();
    expect(riskBtn.disabled).toBe(true);
    expect(riskBtn.title).toContain('Document verification must be completed first');
  });

  it('28. run risk assessment button enables after document verification completes successfully', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const riskBtn = document.querySelector('#run-risk-assessment-btn');
    expect(riskBtn.disabled).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));

    await waitFor(() => {
      expect(screen.getByText('Complete')).toBeDefined();
      expect(riskBtn.disabled).toBe(false);
    });
  });

  it('29. run risk assessment button is disabled when verification result has missing items (incomplete)', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({
      complete: false,
      missingItems: ['Repair Estimate'],
      inconsistencies: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));

    await waitFor(() => {
      expect(screen.getByText('Action Required')).toBeDefined();
    });

    const riskBtn = document.querySelector('#run-risk-assessment-btn');
    expect(riskBtn.disabled).toBe(true);
  });

  it('30. run risk assessment button is re-disabled when documents are mutated after verification', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({ complete: true });
    deleteDocument.mockResolvedValueOnce({ success: true });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const riskBtn = container.querySelector('#run-risk-assessment-btn');
    expect(riskBtn.disabled).toBe(false);

    // Delete a document -> Needs Re-Verification
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));
    await waitFor(() => {
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
    });

    // Risk assessment button should now be disabled again
    expect(riskBtn.disabled).toBe(true);

    confirmSpy.mockRestore();
  });

  it('31. run risk assessment triggers assessClaim and renders assessment result panel with Gemini explanation', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({ complete: true });
    assessClaim.mockResolvedValueOnce({
      id: 'r1',
      claimId: sampleClaim.id,
      claimNumber: sampleClaim.claimNumber,
      riskScore: 25.0,
      riskLevelDisplay: 'Low',
      recommendationDisplay: 'Proceed',
      assessorType: 1,
      assessmentTimestamp: '2026-09-21T18:00:00Z',
      summary: 'Risk Score: 25.0/100 | Level: Low | Recommendation: Proceed | Flags: 0',
      fraudFlagCount: 0,
      flags: [],
      aiUsed: true,
      aiProvider: 'gemini',
      aiModel: 'gemini-2.0-flash',
      reasoningSummary: 'Deterministic rules verified standard claim amount and no duplicate records found.',
      fallbackUsed: false,
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const riskBtn = container.querySelector('#run-risk-assessment-btn');
    fireEvent.click(riskBtn);

    await waitFor(() => {
      expect(assessClaim).toHaveBeenCalledWith(sampleClaim.id, { includeAiAnalysis: true });
      expect(container.querySelector('#risk-assessment-result-panel')).not.toBeNull();
      expect(screen.getByText('25.0/100')).toBeDefined();
      expect(screen.getAllByText(/Proceed/i).length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText(/Deterministic rules verified standard claim amount/i)).toBeDefined();
      expect(screen.getByText(/Gemini AI Contextual Explanation/i)).toBeDefined();
      expect(screen.getByText(/gemini-2\.0-flash/)).toBeDefined();
    });
  });

  it('32. run risk assessment renders deterministic result and fallback notice when AI fallback occurs', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({ complete: true });
    assessClaim.mockResolvedValueOnce({
      id: 'r2',
      claimId: sampleClaim.id,
      claimNumber: sampleClaim.claimNumber,
      riskScore: 35.0,
      riskLevelDisplay: 'Medium',
      recommendationDisplay: 'Review',
      assessorType: 0,
      assessmentTimestamp: '2026-09-21T18:00:00Z',
      summary: 'Risk Score: 35.0/100 | Level: Medium | Recommendation: Review | Flags: 1',
      fraudFlagCount: 1,
      flags: [{ id: 'f1', flagTypeDisplay: 'HighAmount', description: 'Amount is high', severityDisplay: 'Medium' }],
      aiUsed: false,
      fallbackUsed: true,
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const riskBtn = container.querySelector('#run-risk-assessment-btn');
    fireEvent.click(riskBtn);

    await waitFor(() => {
      expect(container.querySelector('#risk-assessment-result-panel')).not.toBeNull();
      expect(container.querySelector('#risk-fallback-notice')).not.toBeNull();
      expect(screen.getByText(/Fallback Rule-Based Assessment was used/i)).toBeDefined();
      expect(screen.getByText('35.0/100')).toBeDefined();
      expect(screen.getAllByText(/Medium/i).length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText(/HighAmount/)).toBeDefined();
    });
  });

  it('33. displays error message if risk assessment fails', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    startWorkflow.mockResolvedValueOnce({ complete: true });
    assessClaim.mockRejectedValueOnce(new Error('Risk assessment calculation timed out.'));

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const riskBtn = container.querySelector('#run-risk-assessment-btn');
    fireEvent.click(riskBtn);

    await waitFor(() => {
      expect(screen.getByText(/Risk assessment calculation timed out/i)).toBeDefined();
      expect(container.querySelector('#risk-assessment-result-panel')).toBeNull();
    });
  });

  it('34. loads and renders existing persisted risk assessment on mount for staff', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getAssessment.mockResolvedValueOnce({
      id: 'r-persisted',
      claimId: sampleClaim.id,
      claimNumber: sampleClaim.claimNumber,
      riskScore: 15.0,
      riskLevelDisplay: 'Low',
      recommendationDisplay: 'Proceed',
      assessorType: 0,
      assessmentTimestamp: '2026-09-20T10:00:00Z',
      summary: 'Risk Score: 15.0/100 | Level: Low | Recommendation: Proceed | Flags: 0 | AI Context: Claim matches standard policy criteria.',
      fraudFlagCount: 0,
      flags: [],
      hasFraudCase: false,
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => {
      expect(container.querySelector('#risk-assessment-result-panel')).not.toBeNull();
      expect(screen.getByText('15.0/100')).toBeDefined();
      expect(screen.getByText(/Claim matches standard policy criteria/i)).toBeDefined();
    });
  });

  it('35. policyholder does not see internal risk assessment panel even if existing assessment exists', async () => {
    mockAuthRole = 'Policyholder';
    getAssessment.mockResolvedValueOnce({
      id: 'r-persisted',
      claimId: sampleClaim.id,
      claimNumber: sampleClaim.claimNumber,
      riskScore: 90.0,
      riskLevelDisplay: 'Critical',
      recommendationDisplay: 'Escalate',
      assessorType: 1,
      fraudFlagCount: 3,
    });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(container.querySelector('#risk-assessment-result-panel')).toBeNull();
    expect(screen.queryByText('Critical')).toBeNull();
  });

  // ── Payout Workflow Integration Tests (Kinukshan) ──

  it('36. Policyholder does not see Prepare Payout or View Payout buttons', async () => {
    mockAuthRole = 'Policyholder';
    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(screen.queryByRole('button', { name: /prepare payout/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /view payout/i })).toBeNull();
    expect(document.querySelector('#prepare-payout-btn')).toBeNull();
    expect(document.querySelector('#view-payout-btn')).toBeNull();
  });

  it('37. Staff sees Prepare Payout button when viewing claim', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn).toBeDefined();
    expect(btn.id).toBe('prepare-payout-btn');
  });

  it('38. Prepare Payout button is disabled before Document Verification is complete', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    // Persisted risk assessment exists, but document verification has not completed
    getAssessment.mockResolvedValueOnce({ id: 'r-1', riskScore: 25.0 });
    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(true);
    expect(btn.getAttribute('title')).toBe('Complete Document Verification before preparing payout.');
  });

  it('39. Prepare Payout button is disabled before Risk Assessment exists', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getAssessment.mockResolvedValue(null);
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(true);
    expect(btn.getAttribute('title')).toBe('Complete Risk Assessment before preparing payout.');
  });

  it('40. Prepare Payout button is enabled after fresh Document Verification + Risk Assessment exists', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getAssessment.mockResolvedValue({
      id: 'r-1',
      riskScore: 25.0,
      riskLevelDisplay: 'Low',
      recommendationDisplay: 'Proceed',
    });
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(false);
    expect(btn.getAttribute('title')).toBe('Prepare payout proposal');
  });

  it('41. Withdrawn claim cannot prepare payout and button is blocked', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getClaim.mockResolvedValueOnce({
      ...sampleClaim,
      status: 'Withdrawn',
    });
    getAssessment.mockResolvedValue({ id: 'r-1', riskScore: 25.0 });
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(true);
    expect(btn.getAttribute('title')).toContain('Cannot prepare payout for a withdrawn claim');

    fireEvent.click(btn);
    expect(mockNavigate).not.toHaveBeenCalledWith(expect.stringContaining('/payouts/calculate'));
  });

  it('42. Clicking enabled Prepare Payout button navigates to /payouts/calculate with claim GUID', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getAssessment.mockResolvedValue({ id: 'r-1', riskScore: 25.0 });
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(false);

    fireEvent.click(btn);
    expect(mockNavigate).toHaveBeenCalledWith(`/payouts/calculate?claimId=${sampleClaim.id}`);
  });

  it('43. Existing payout replaces action with View Payout button', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    getPayoutByClaim.mockResolvedValueOnce({
      id: 'payout-guid-999',
      claimId: sampleClaim.id,
      claimNumber: sampleClaim.claimNumber,
      status: 1,
      statusDisplay: 'PendingApproval',
      finalPayout: 4500,
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    expect(screen.queryByRole('button', { name: /prepare payout/i })).toBeNull();
    const viewBtn = screen.getByRole('button', { name: /view payout/i });
    expect(viewBtn).toBeDefined();
    expect(viewBtn.id).toBe('view-payout-btn');

    fireEvent.click(viewBtn);
    expect(mockNavigate).toHaveBeenCalledWith(`/payouts/calculate?claimId=${sampleClaim.id}`);
  });

  it('44. getPayoutByClaim returning 404 treats as expected no payout and allows Prepare Payout without error banner', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    const notFoundError = new Error('No payout found');
    notFoundError.status = 404;
    getPayoutByClaim.mockRejectedValueOnce(notFoundError);

    getAssessment.mockResolvedValue({ id: 'r-1', riskScore: 25.0 });
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
    });

    render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-0001')).toBeDefined());

    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Expect no error message displayed for 404
    expect(screen.queryByText(/failed to check existing payout/i)).toBeNull();
    const btn = screen.getByRole('button', { name: /prepare payout/i });
    expect(btn.disabled).toBe(false);
  });

  it('45. getPayoutByClaim 500/network error sets page-level error and is not interpreted as no payout', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    const serverError = new Error('Database server failure');
    serverError.status = 500;
    getPayoutByClaim.mockRejectedValueOnce(serverError);

    render(<ClaimDetails />);
    await waitFor(() => {
      expect(screen.getByText(/Database server failure/i)).toBeDefined();
    });
  });
});
