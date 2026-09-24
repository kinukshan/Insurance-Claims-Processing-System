/**
 * Claim Document Requirements UX tests
 * Tests deterministic document requirements checklist, progress badges,
 * alias normalization, upload dropdown grouping, and quick-upload selection.
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
  getDocumentRequirements,
} from '../../services/claimService';
import { assessClaim, getAssessment } from '../../services/riskService';
import { getPayoutByClaim } from '../../services/payoutService';

// ── Mock react-router-dom ──
const mockNavigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useParams: () => ({ id: 'test-claim-uuid-1111' }),
  useNavigate: () => mockNavigate,
}));

// ── Mock AuthContext ──
let mockAuthRole = 'Policyholder';
let mockAuthUserId = 'user-uuid-1111';
vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    role: mockAuthRole,
    user: { email: 'policyholder@example.com', role: mockAuthRole, userId: mockAuthUserId },
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
  getDocumentRequirements: vi.fn(),
}));

// ── Mock riskService & payoutService ──
vi.mock('../../services/riskService', () => ({
  assessClaim: vi.fn(),
  getAssessment: vi.fn(),
  getAllAssessments: vi.fn(),
}));

vi.mock('../../services/payoutService', () => ({
  getPayoutByClaim: vi.fn(),
  calculatePayout: vi.fn(),
}));

describe('Claim Document Requirements Component Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthRole = 'Policyholder';
    mockAuthUserId = 'user-uuid-1111';
    getAssessment.mockResolvedValue(null);
    getPayoutByClaim.mockResolvedValue(null);
  });

  const createBaseClaim = (claimType = 'Property', documents = []) => ({
    id: 'test-claim-uuid-1111',
    claimNumber: 'CLM-2026-TEST',
    claimType,
    status: 'Submitted',
    description: 'Test claim description',
    claimedAmount: 10000,
    incidentDate: '2026-09-01T10:00:00Z',
    incidentLocation: 'Colombo, Sri Lanka',
    policyId: 'policy-uuid-1111',
    policyHolderId: 'user-uuid-1111',
    submittedAt: '2026-09-02T12:00:00Z',
    createdAt: '2026-09-01T11:00:00Z',
    updatedAt: '2026-09-02T12:00:00Z',
    documents,
  });

  // 1. Property claim shows correct required documents
  it('1. Property claim shows correct required documents and heading', async () => {
    const claim = createBaseClaim('Property');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Property',
      requiredDocuments: [
        { type: 'Photos of Damage', required: true, uploaded: false },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Property Valuation', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 0,
      missingCount: 3,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = container.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Property Claim')).toBeDefined();
      expect(within(card).getByText('Photos of Damage')).toBeDefined();
      expect(within(card).getByText('Repair Estimate')).toBeDefined();
      expect(within(card).getByText('Property Valuation')).toBeDefined();
      expect(within(card).getByText('0 of 3 required documents uploaded')).toBeDefined();
    });
  });

  // 2. Motor claim shows correct required documents
  it('2. Motor claim shows correct required documents and heading', async () => {
    const claim = createBaseClaim('Motor');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Motor',
      requiredDocuments: [
        { type: 'Police Report', required: true, uploaded: false },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Photos of Damage', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 0,
      missingCount: 3,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = container.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Motor Claim')).toBeDefined();
      expect(within(card).getByText('Police Report')).toBeDefined();
      expect(within(card).getByText('Repair Estimate')).toBeDefined();
      expect(within(card).getByText('Photos of Damage')).toBeDefined();
    });
  });

  // 3. Health claim shows correct required documents
  it('3. Health claim shows correct required documents and heading', async () => {
    const claim = createBaseClaim('Health');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Health',
      requiredDocuments: [
        { type: 'Medical Report', required: true, uploaded: false },
        { type: 'Itemized Medical Bills', required: true, uploaded: false },
      ],
      requiredCount: 2,
      uploadedRequiredCount: 0,
      missingCount: 2,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = container.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Health Claim')).toBeDefined();
      expect(within(card).getByText('Medical Report')).toBeDefined();
      expect(within(card).getByText('Itemized Medical Bills')).toBeDefined();
      expect(within(card).getByText('0 of 2 required documents uploaded')).toBeDefined();
    });
  });

  // 4. Life claim shows: Death Certificate, Policy Document, Beneficiary / Nominee Identification, Claim Form
  it('4. Life claim shows all 4 canonical required documents', async () => {
    const claim = createBaseClaim('Life');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Life',
      requiredDocuments: [
        { type: 'Death Certificate', required: true, uploaded: false },
        { type: 'Policy Document', required: true, uploaded: false },
        { type: 'Beneficiary / Nominee Identification', required: true, uploaded: false },
        { type: 'Claim Form', required: true, uploaded: false },
      ],
      requiredCount: 4,
      uploadedRequiredCount: 0,
      missingCount: 4,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = container.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Life Claim')).toBeDefined();
      expect(within(card).getByText('Death Certificate')).toBeDefined();
      expect(within(card).getByText('Policy Document')).toBeDefined();
      expect(within(card).getByText('Beneficiary / Nominee Identification')).toBeDefined();
      expect(within(card).getByText('Claim Form')).toBeDefined();
      expect(within(card).getByText('0 of 4 required documents uploaded')).toBeDefined();
    });
  });

  // 5. Uploaded required document shows "Uploaded"
  // 6. Missing required document shows "Missing"
  // 7. Correct progress: 3 of 4 required documents uploaded
  it('5-7. renders correct Uploaded and Missing badges and progress count', async () => {
    const claim = createBaseClaim('Life', [
      { id: 'd1', fileName: 'death_cert.pdf', documentType: 'Death Certificate', fileSize: 1000, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'PENDING' },
      { id: 'd2', fileName: 'policy.pdf', documentType: 'Policy Document', fileSize: 1000, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'PENDING' },
      { id: 'd3', fileName: 'beneficiary_id.pdf', documentType: 'Beneficiary ID', fileSize: 1000, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'PENDING' },
    ]);
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Life',
      requiredDocuments: [
        { type: 'Death Certificate', required: true, uploaded: true },
        { type: 'Policy Document', required: true, uploaded: true },
        { type: 'Beneficiary / Nominee Identification', required: true, uploaded: true },
        { type: 'Claim Form', required: true, uploaded: false },
      ],
      requiredCount: 4,
      uploadedRequiredCount: 3,
      missingCount: 1,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      expect(screen.getByText('3 of 4 required documents uploaded')).toBeDefined();
      expect(screen.getByText('Upload the missing required documents before verification.')).toBeDefined();

      const deathCertRow = container.querySelector('#req-death-certificate');
      expect(deathCertRow).not.toBeNull();
      expect(deathCertRow.textContent).toContain('Uploaded');
      expect(deathCertRow.textContent).toContain('✓');

      const claimFormRow = container.querySelector('#req-claim-form');
      expect(claimFormRow).not.toBeNull();
      expect(claimFormRow.textContent).toContain('Missing');
      expect(claimFormRow.textContent).toContain('○');
    });
  });

  // 8. All complete shows ready-to-verify message
  it('8. all required documents complete shows ready-to-verify message and green badge', async () => {
    const claim = createBaseClaim('Life');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Life',
      requiredDocuments: [
        { type: 'Death Certificate', required: true, uploaded: true },
        { type: 'Policy Document', required: true, uploaded: true },
        { type: 'Beneficiary / Nominee Identification', required: true, uploaded: true },
        { type: 'Claim Form', required: true, uploaded: true },
      ],
      requiredCount: 4,
      uploadedRequiredCount: 4,
      missingCount: 0,
      complete: true,
    });

    render(<ClaimDetails />);

    await waitFor(() => {
      expect(screen.getByText('4 of 4 required documents uploaded')).toBeDefined();
      expect(screen.getByText('All required documents have been uploaded. You can now verify the documents.')).toBeDefined();
    });
  });

  // 9. Beneficiary ID satisfies Beneficiary / Nominee Identification
  it('9. Beneficiary ID uploaded satisfies Beneficiary / Nominee Identification requirement', async () => {
    const claim = createBaseClaim('Life', [
      { id: 'd1', fileName: 'nic.pdf', documentType: 'Beneficiary ID', fileSize: 5000, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'PENDING' },
    ]);
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Life',
      requiredDocuments: [
        { type: 'Death Certificate', required: true, uploaded: false },
        { type: 'Policy Document', required: true, uploaded: false },
        { type: 'Beneficiary / Nominee Identification', required: true, uploaded: true },
        { type: 'Claim Form', required: true, uploaded: false },
      ],
      requiredCount: 4,
      uploadedRequiredCount: 1,
      missingCount: 3,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const nomineeRow = container.querySelector('#req-beneficiary---nominee-identification');
      expect(nomineeRow).not.toBeNull();
      expect(nomineeRow.textContent).toContain('Uploaded');
      expect(nomineeRow.textContent).not.toContain('Missing');
    });
  });

  // 10. Unsupported/API failure does not crash ClaimDetails
  it('10. API failure on getDocumentRequirements displays warning but does not crash ClaimDetails', async () => {
    const claim = createBaseClaim('Property');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockRejectedValue(new Error('Network error'));

    render(<ClaimDetails />);

    await waitFor(() => {
      expect(screen.getByText('CLM-2026-TEST')).toBeDefined();
      expect(screen.getByText(/Unable to load document requirements. You can still upload documents./i)).toBeDefined();
    });
  });

  // 11. Existing uploaded document list still displays
  it('11. existing uploaded document list still displays correctly with individual status', async () => {
    const claim = createBaseClaim('Property', [
      { id: 'd1', fileName: 'damage1.jpg', documentType: 'Photos of Damage', fileSize: 1048576, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'PENDING' },
    ]);
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Property',
      requiredDocuments: [
        { type: 'Photos of Damage', required: true, uploaded: true },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Property Valuation', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 1,
      missingCount: 2,
      complete: false,
    });

    render(<ClaimDetails />);

    await waitFor(() => {
      expect(screen.getByText('damage1.jpg')).toBeDefined();
      expect(screen.getByText('PENDING')).toBeDefined();
    });
  });

  // 12. Stale verification behavior remains intact
  it('12. stale verification behavior remains intact after requirements integration', async () => {
    mockAuthRole = 'ClaimsAdjuster';
    const claim = createBaseClaim('Auto', [
      { id: 'd1', fileName: 'police.pdf', documentType: 'Police Report', fileSize: 1000, uploadedAt: '2026-09-02T12:00:00Z', verificationStatus: 'Verified' },
    ]);
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Auto',
      requiredDocuments: [
        { type: 'Police Report', required: true, uploaded: true },
      ],
      requiredCount: 1,
      uploadedRequiredCount: 1,
      missingCount: 0,
      complete: true,
    });
    startWorkflow.mockResolvedValueOnce({
      complete: true,
      missingItems: [],
      inconsistencies: [],
      warnings: [],
      aiUsed: true,
      reasoningSummary: 'Verified Auto claim.',
    });
    uploadDocument.mockResolvedValueOnce({ id: 'd2', fileName: 'repair.pdf' });

    const { container } = render(<ClaimDetails />);
    await waitFor(() => expect(screen.getByText('CLM-2026-TEST')).toBeDefined());

    // Verify
    fireEvent.click(screen.getByRole('button', { name: /verify documents/i }));
    await waitFor(() => expect(screen.getByText('Complete')).toBeDefined());

    // Upload new doc
    const file = new File(['content'], 'repair.pdf', { type: 'application/pdf' });
    const fileInput = container.querySelector('#doc-file');
    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadBtn = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(uploadBtn);

    await waitFor(() => {
      expect(container.querySelector('#reverification-notice')).not.toBeNull();
      expect(screen.getByText(/Needs Re-Verification/i)).toBeDefined();
    });
  });

  // 13. Historical Auto/Home claims remain supported
  it('13. historical Auto and Home claims display appropriate headings safely', async () => {
    const autoClaim = createBaseClaim('Auto');
    getClaim.mockResolvedValue(autoClaim);
    getDocumentRequirements.mockResolvedValue({
      claimId: autoClaim.id,
      claimType: 'Auto',
      requiredDocuments: [
        { type: 'Police Report', required: true, uploaded: false },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Photos of Damage', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 0,
      missingCount: 3,
      complete: false,
    });

    const { unmount, container: autoContainer } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = autoContainer.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Auto Claim')).toBeDefined();
    });

    unmount();

    const homeClaim = createBaseClaim('Home');
    getClaim.mockResolvedValue(homeClaim);
    getDocumentRequirements.mockResolvedValue({
      claimId: homeClaim.id,
      claimType: 'Home',
      requiredDocuments: [
        { type: 'Photos of Damage', required: true, uploaded: false },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Property Valuation', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 0,
      missingCount: 3,
      complete: false,
    });

    const { container: homeContainer } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = homeContainer.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Required Documents for this Home Claim')).toBeDefined();
    });
  });

  // 14. Document dropdown groups Required first and Other / Optional second
  it('14. document upload dropdown groups Required first (with Uploaded marker) and Optional second', async () => {
    const claim = createBaseClaim('Property');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Property',
      requiredDocuments: [
        { type: 'Photos of Damage', required: true, uploaded: true },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Property Valuation', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 1,
      missingCount: 2,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const select = container.querySelector('#doc-type');
      expect(select).not.toBeNull();

      const optgroups = select.querySelectorAll('optgroup');
      expect(optgroups.length).toBe(2);
      expect(optgroups[0].label).toBe('Required');
      expect(optgroups[1].label).toBe('Other / Optional');

      const requiredOptions = Array.from(optgroups[0].querySelectorAll('option')).map((o) => o.textContent);
      expect(requiredOptions).toContain('Photos of Damage (Uploaded)');
      expect(requiredOptions).toContain('Repair Estimate');
      expect(requiredOptions).toContain('Property Valuation');
    });
  });

  // 15. Quick upload action preselects document type
  it('15. clicking quick upload action preselects document type in dropdown', async () => {
    const claim = createBaseClaim('Property');
    getClaim.mockResolvedValue(claim);
    getDocumentRequirements.mockResolvedValue({
      claimId: claim.id,
      claimType: 'Property',
      requiredDocuments: [
        { type: 'Photos of Damage', required: true, uploaded: true },
        { type: 'Repair Estimate', required: true, uploaded: false },
        { type: 'Property Valuation', required: true, uploaded: false },
      ],
      requiredCount: 3,
      uploadedRequiredCount: 1,
      missingCount: 2,
      complete: false,
    });

    const { container } = render(<ClaimDetails />);

    await waitFor(() => {
      const card = container.querySelector('#required-documents-card');
      expect(card).not.toBeNull();
      expect(within(card).getByText('Repair Estimate')).toBeDefined();
    });

    const quickUploadBtn = screen.getByRole('button', { name: 'Select Repair Estimate' });
    fireEvent.click(quickUploadBtn);

    const select = container.querySelector('#doc-type');
    expect(select.value).toBe('Repair Estimate');
  });
});
