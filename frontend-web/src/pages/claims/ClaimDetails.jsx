/**
 * Claim Details — Component B (Member 2)
 * Claim detail view with documents, coverage validation,
 * and Document Verification Agent integration with Gemini AI reasoning.
 */

import React, { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import {
  getClaim,
  deleteClaim,
  withdrawClaim,
  uploadDocument,
  deleteDocument,
  validateCoverage,
  startWorkflow,
} from '../../services/claimService';
import {
  assessClaim,
  getAssessment,
} from '../../services/riskService';
import {
  getPayoutByClaim,
} from '../../services/payoutService';

const DOCUMENT_TYPES = [
  'Police Report', 'Photos of Damage', 'Repair Estimate', 'Driver License',
  'Property Deed', 'Medical Report', 'Hospital Bills', 'Prescription',
  'Doctor Referral', 'Death Certificate', 'Beneficiary ID', 'Policy Document',
  'Travel Itinerary', 'Receipts', 'Incident Report', 'Property Valuation',
  'Third Party Claim', 'Legal Notice', 'Supporting Document',
];

function ClaimDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { role, user } = useAuth();

  const isStaff = role === 'ClaimsAdjuster' || role === 'Underwriter' || role === 'Admin';
  const isPolicyholder = role === 'Policyholder';

  const [claim, setClaim] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [successMessage, setSuccessMessage] = useState(null);

  // Upload state
  const [uploadFile, setUploadFile] = useState(null);
  const [uploadDocType, setUploadDocType] = useState('Supporting Document');
  const [uploading, setUploading] = useState(false);

  // Verification / Coverage / Risk / Payout state
  const [verificationResult, setVerificationResult] = useState(null);
  const [documentsChangedSinceVerification, setDocumentsChangedSinceVerification] = useState(false);
  const [coverageResult, setCoverageResult] = useState(null);
  const [riskResult, setRiskResult] = useState(null);
  const [existingPayout, setExistingPayout] = useState(null);
  const [actionLoading, setActionLoading] = useState(null);

  const fetchClaim = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getClaim(id);
      setClaim(data);
      if (isStaff) {
        try {
          const existingRisk = await getAssessment(id);
          if (existingRisk) {
            setRiskResult(existingRisk);
          }
        } catch {
          // No assessment or not found yet
        }

        try {
          const existingPayoutData = await getPayoutByClaim(id);
          setExistingPayout(existingPayoutData || null);
        } catch (payoutErr) {
          if (payoutErr.status === 404) {
            // Expected: no payout exists yet for this claim
            setExistingPayout(null);
          } else {
            // 401/403/500/network failures: treat as real errors
            setError(payoutErr.message || 'Failed to check existing payout.');
            setExistingPayout(null);
          }
        }
      }
    } catch (err) {
      setError(err.message || 'Failed to load claim');
    } finally {
      setLoading(false);
    }
  }, [id, isStaff]);

  useEffect(() => {
    fetchClaim();
  }, [fetchClaim]);

  const handleUpload = async (e) => {
    e.preventDefault();
    if (!uploadFile) return;
    setUploading(true);
    setError(null);
    try {
      await uploadDocument(id, uploadFile, uploadDocType);
      setUploadFile(null);
      setUploadDocType('Supporting Document');
      if (verificationResult || documentsChangedSinceVerification) {
        setVerificationResult(null);
        setDocumentsChangedSinceVerification(true);
      }
      await fetchClaim();
    } catch (err) {
      setError(err.message);
    } finally {
      setUploading(false);
    }
  };

  const handleValidateCoverage = async () => {
    setActionLoading('coverage');
    setCoverageResult(null);
    try {
      const result = await validateCoverage(id);
      setCoverageResult(result);
      await fetchClaim();
    } catch (err) {
      setCoverageResult({ isValid: false, isCovered: false, issues: [err.message] });
    } finally {
      setActionLoading(null);
    }
  };

  const handleStartWorkflow = async () => {
    setActionLoading('workflow');
    setError(null);
    try {
      const result = await startWorkflow(id);
      setVerificationResult(result);
      setDocumentsChangedSinceVerification(false);
      await fetchClaim();
    } catch (err) {
      setError(err.message || 'Document verification failed.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleRunRiskAssessment = async () => {
    if (!normVerif?.complete || documentsChangedSinceVerification || actionLoading === 'risk') return;
    setActionLoading('risk');
    setError(null);
    setSuccessMessage(null);
    try {
      const result = await assessClaim(id, { includeAiAnalysis: true });
      setRiskResult(result);
      setSuccessMessage('Risk assessment completed successfully.');
      await fetchClaim();
    } catch (err) {
      setError(err.message || 'Risk assessment failed.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeleteClaim = async () => {
    if (!window.confirm('Delete Claim?\nAre you sure you want to delete this draft claim?')) return;
    setActionLoading('delete');
    setError(null);
    try {
      await deleteClaim(id);
      navigate('/claims');
    } catch (err) {
      if (err.status === 403 || err.message?.includes('permission')) {
        setError('You do not have permission to delete this item.');
      } else {
        setError(err.message || 'Unable to delete the item. Please try again.');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleWithdrawClaim = async () => {
    if (!window.confirm(`Withdraw Claim?\nAre you sure you want to withdraw claim ${claim?.claimNumber}?`)) return;
    setActionLoading('withdraw');
    setError(null);
    try {
      await withdrawClaim(id);
      setSuccessMessage('Claim has been withdrawn successfully.');
      await fetchClaim();
    } catch (err) {
      if (err.status === 403 || err.message?.includes('permission')) {
        setError('You do not have permission to delete this item.');
      } else {
        setError(err.message || 'Unable to delete the item. Please try again.');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeleteDocument = async (doc) => {
    if (!window.confirm(`Delete Document?\nAre you sure you want to remove ${doc.fileName}?`)) return;
    setActionLoading(`doc-delete-${doc.id}`);
    setError(null);
    try {
      await deleteDocument(id, doc.id);
      setSuccessMessage(`Document ${doc.fileName} deleted successfully.`);
      if (verificationResult || documentsChangedSinceVerification) {
        setVerificationResult(null);
        setDocumentsChangedSinceVerification(true);
      }
      await fetchClaim();
    } catch (err) {
      if (err.status === 403 || err.message?.includes('permission')) {
        setError('You do not have permission to delete this item.');
      } else if (err.status === 409 || err.message?.includes('referenced') || err.message?.includes('processed')) {
        setError('This item cannot be deleted because it is already referenced or processed.');
      } else {
        setError(err.message || 'Unable to delete the item. Please try again.');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
    });
  };

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency', currency: 'USD',
    }).format(amount);
  };

  const getStatusClass = (status) => {
    return `status-badge status-badge--${status.toLowerCase().replace(/\s+/g, '')}`;
  };

  const getVerifStatusClass = (status) => {
    const map = {
      Pending: 'status-badge--pending',
      Verified: 'status-badge--approved',
      Rejected: 'status-badge--rejected',
      Flagged: 'status-badge--additionaldocumentsrequired',
    };
    return `status-badge ${map[status] || 'status-badge--draft'}`;
  };

  const formatFileSize = (bytes) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  // Loading state
  if (loading) {
    return (
      <div className="fade-in">
        <div className="loading-state">
          <div className="loading-spinner" />
          <p>Loading claim details…</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error && !claim) {
    return (
      <div className="fade-in">
        <div className="error-state">
          <p>⚠️ {error}</p>
          <button className="btn btn--primary" onClick={() => navigate('/claims')} style={{ marginTop: '1rem' }}>
            Back to Claims
          </button>
        </div>
      </div>
    );
  }

  if (!claim) return null;

  const isFinalStatus = ['Approved', 'Rejected', 'Withdrawn', 'Closed'].includes(claim.status);
  const isDraft = claim.status === 'Draft';
  const isOwner = Boolean(user?.userId && claim.policyHolderId && user.userId === claim.policyHolderId);
  const canDeleteClaim = isDraft && isPolicyholder && isOwner;
  const canWithdrawClaim = ['Submitted', 'UnderReview'].includes(claim.status) && isPolicyholder && isOwner;
  const canDeleteDocument = !isFinalStatus && (isStaff || (isPolicyholder && isOwner));

  // Normalization for snake_case / camelCase compatibility
  const normVerif = verificationResult ? {
    complete: verificationResult.complete ?? false,
    missingItems: verificationResult.missingItems || verificationResult.missing_items || [],
    inconsistencies: verificationResult.inconsistencies || [],
    warnings: verificationResult.warnings || [],
    aiUsed: verificationResult.aiUsed ?? verificationResult.ai_used ?? false,
    aiProvider: verificationResult.aiProvider || verificationResult.ai_provider || null,
    aiModel: verificationResult.aiModel || verificationResult.ai_model || null,
    reasoningSummary: verificationResult.reasoningSummary || verificationResult.reasoning_summary || null,
    fallbackUsed: verificationResult.fallbackUsed ?? verificationResult.fallback_used ?? false,
  } : null;

  const isRiskReady = Boolean(normVerif && normVerif.complete && !documentsChangedSinceVerification);
  const hasExistingPayout = Boolean(existingPayout);
  const isDocVerifComplete = Boolean(normVerif && normVerif.complete && !documentsChangedSinceVerification);
  const hasRiskAssessment = Boolean(riskResult);
  const isBlockedStatus = ['Draft', 'Withdrawn', 'Rejected', 'Closed'].includes(claim.status);
  const isPayoutReady = !isBlockedStatus && isDocVerifComplete && hasRiskAssessment;

  let payoutTooltip = '';
  if (isBlockedStatus) {
    payoutTooltip = `Cannot prepare payout for a ${claim.status.toLowerCase()} claim.`;
  } else if (!isDocVerifComplete) {
    payoutTooltip = 'Complete Document Verification before preparing payout.';
  } else if (!hasRiskAssessment) {
    payoutTooltip = 'Complete Risk Assessment before preparing payout.';
  } else {
    payoutTooltip = 'Prepare payout proposal';
  }

  const handlePreparePayout = () => {
    if (!isPayoutReady) return;
    navigate(`/payouts/calculate?claimId=${claim.id}`);
  };

  return (
    <div className="fade-in">
      {/* Page Header */}
      <div className="page-header">
        <div>
          <button className="btn btn--secondary btn--sm" onClick={() => navigate('/claims')} style={{ marginBottom: '0.75rem' }}>
            ← Back to Claims
          </button>
          <h2 style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            {claim.claimNumber}
            <span className={getStatusClass(claim.status)}>
              {claim.status.replace(/([A-Z])/g, ' $1').trim()}
            </span>
          </h2>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          {canDeleteClaim && (
            <button
              id="delete-claim-btn"
              className="btn btn--secondary"
              style={{ color: 'var(--color-rejected)', borderColor: 'rgba(239, 68, 68, 0.3)' }}
              onClick={handleDeleteClaim}
              disabled={actionLoading === 'delete'}
            >
              {actionLoading === 'delete' ? 'Deleting…' : 'Delete Claim'}
            </button>
          )}
          {canWithdrawClaim && (
            <button
              id="withdraw-claim-btn"
              className="btn btn--secondary"
              style={{ color: 'var(--color-rejected)', borderColor: 'rgba(239, 68, 68, 0.3)' }}
              onClick={handleWithdrawClaim}
              disabled={actionLoading === 'withdraw'}
            >
              {actionLoading === 'withdraw' ? 'Withdrawing…' : 'Withdraw Claim'}
            </button>
          )}
          <button
            id="validate-coverage-btn"
            className="btn btn--secondary"
            onClick={handleValidateCoverage}
            disabled={actionLoading === 'coverage'}
          >
            {actionLoading === 'coverage' ? 'Validating…' : '🔍 Validate Coverage'}
          </button>
          <button
            id="start-workflow-btn"
            className="btn btn--primary"
            onClick={handleStartWorkflow}
            disabled={actionLoading === 'workflow'}
          >
            {actionLoading === 'workflow' ? 'Verifying…' : '🤖 Verify Documents'}
          </button>
          {isStaff && (
            <button
              id="run-risk-assessment-btn"
              className="btn btn--primary"
              onClick={handleRunRiskAssessment}
              disabled={!isRiskReady || actionLoading === 'risk'}
              title={!isRiskReady ? 'Document verification must be completed first' : 'Run Risk Assessment'}
            >
              {actionLoading === 'risk' ? 'Assessing Risk…' : '🛡️ Run Risk Assessment'}
            </button>
          )}
          {isStaff && (
            hasExistingPayout ? (
              <button
                id="view-payout-btn"
                className="btn btn--secondary"
                onClick={() => navigate(`/payouts/calculate?claimId=${claim.id}`)}
                title="View existing payout proposal"
              >
                💰 View Payout
              </button>
            ) : (
              <button
                id="prepare-payout-btn"
                className="btn btn--primary"
                onClick={handlePreparePayout}
                disabled={!isPayoutReady}
                title={payoutTooltip}
              >
                💰 Prepare Payout
              </button>
            )
          )}
        </div>
      </div>

      {successMessage && (
        <div className="card" style={{ borderColor: 'rgba(16, 185, 129, 0.3)', backgroundColor: 'rgba(16, 185, 129, 0.05)', marginBottom: '1.5rem' }}>
          <p style={{ color: 'var(--color-approved)', margin: 0 }}>✅ {successMessage}</p>
        </div>
      )}

      {error && (
        <div className="card" style={{ borderColor: 'rgba(239, 68, 68, 0.3)', marginBottom: '1.5rem' }}>
          <p style={{ color: 'var(--color-rejected)' }}>⚠️ {error}</p>
        </div>
      )}

      {/* Claim Details Grid */}
      <div className="detail-grid" style={{ marginBottom: '2rem' }}>
        <div className="detail-item">
          <label>Claim Type</label>
          <div className="value">{claim.claimType}</div>
        </div>
        <div className="detail-item">
          <label>Claimed Amount</label>
          <div className="value" style={{ color: 'var(--color-accent)' }}>{formatCurrency(claim.claimedAmount)}</div>
        </div>
        <div className="detail-item">
          <label>Incident Date</label>
          <div className="value">{formatDate(claim.incidentDate)}</div>
        </div>
        <div className="detail-item">
          <label>Incident Location</label>
          <div className="value">{claim.incidentLocation}</div>
        </div>
        <div className="detail-item">
          <label>Policy ID</label>
          <div className="value" style={{ fontSize: '0.8rem', fontFamily: 'monospace' }}>{claim.policyId}</div>
        </div>
        <div className="detail-item">
          <label>Policyholder ID</label>
          <div className="value" style={{ fontSize: '0.8rem', fontFamily: 'monospace' }}>{claim.policyHolderId}</div>
        </div>
        <div className="detail-item">
          <label>Submitted At</label>
          <div className="value">{formatDate(claim.submittedAt)}</div>
        </div>
        <div className="detail-item">
          <label>Created At</label>
          <div className="value">{formatDate(claim.createdAt)}</div>
        </div>
      </div>

      {/* Description */}
      <div className="card" style={{ marginBottom: '2rem' }}>
        <h3 style={{ marginBottom: '0.75rem' }}>Incident Description</h3>
        <p style={{ color: 'var(--text-secondary)', lineHeight: '1.7' }}>{claim.description}</p>
      </div>

      {/* Coverage Validation Result */}
      {coverageResult && (
        <div className="card" style={{
          marginBottom: '2rem',
          borderColor: coverageResult.isCovered ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)',
        }}>
          <h3 style={{ marginBottom: '1rem' }}>
            {coverageResult.isCovered ? '✅' : '❌'} Coverage Validation
          </h3>
          <div className="detail-grid">
            <div className="detail-item">
              <label>Covered</label>
              <div className="value">{coverageResult.isCovered ? 'Yes' : 'No'}</div>
            </div>
            {coverageResult.coverageLimit != null && (
              <div className="detail-item">
                <label>Coverage Limit</label>
                <div className="value">{formatCurrency(coverageResult.coverageLimit)}</div>
              </div>
            )}
            {coverageResult.deductibleAmount != null && (
              <div className="detail-item">
                <label>Deductible</label>
                <div className="value">{formatCurrency(coverageResult.deductibleAmount)}</div>
              </div>
            )}
            {coverageResult.coverageType && (
              <div className="detail-item">
                <label>Coverage Type</label>
                <div className="value">{coverageResult.coverageType}</div>
              </div>
            )}
          </div>
          {coverageResult.issues?.length > 0 && (
            <div style={{ marginTop: '1rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Issues</label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-rejected)' }}>
                {coverageResult.issues.map((issue, i) => <li key={i}>{issue}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Re-verification Required Notice */}
      {documentsChangedSinceVerification && (
        <div
          id="reverification-notice"
          className="card"
          style={{
            marginBottom: '2rem',
            borderColor: 'rgba(245, 158, 11, 0.4)',
            backgroundColor: 'rgba(245, 158, 11, 0.05)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem', flexWrap: 'wrap', gap: '0.5rem' }}>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#d97706' }}>
              ⚠️ Documents Changed
            </h3>
            <span className="status-badge status-badge--pending" id="reverification-badge">
              Needs Re-Verification
            </span>
          </div>
          <p style={{ color: 'var(--text-secondary)', margin: 0, lineHeight: '1.6' }}>
            The claim documents have changed since the last verification. Run document verification again.
          </p>
        </div>
      )}

      {/* Role-Aware Document Verification Result */}
      {normVerif && (
        <div
          id="verification-result-panel"
          className="card"
          style={{
            marginBottom: '2rem',
            borderColor: normVerif.complete ? 'rgba(16, 185, 129, 0.3)' : 'rgba(245, 158, 11, 0.3)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              {normVerif.complete ? '✅' : '⚠️'} Document Verification Result
            </h3>
            <span className={`status-badge ${normVerif.complete ? 'status-badge--approved' : 'status-badge--riskassessment'}`}>
              {normVerif.complete ? 'Complete' : 'Action Required'}
            </span>
          </div>

          <p style={{ color: 'var(--text-secondary)', marginBottom: '1rem' }}>
            {normVerif.complete
              ? 'All required documents are present and verified.'
              : 'Verification found issues that need attention.'}
          </p>

          {/* Fallback Notice */}
          {normVerif.fallbackUsed && (
            <div
              id="verification-fallback-notice"
              style={{
                marginBottom: '1rem',
                padding: '0.75rem 1rem',
                background: 'rgba(245, 158, 11, 0.1)',
                border: '1px solid rgba(245, 158, 11, 0.3)',
                borderRadius: 'var(--radius-sm)',
                color: 'var(--color-pending)',
                fontSize: '0.875rem',
              }}
            >
              ⚠️ Fallback Rule-Based Verification was used (Gemini AI service unavailable or bypassed).
            </div>
          )}

          {/* Policyholder Actionable Guidance */}
          {isPolicyholder && (
            <div
              id="policyholder-guidance"
              style={{
                marginBottom: '1.25rem',
                padding: '0.85rem 1.25rem',
                background: normVerif.complete ? 'rgba(16, 185, 129, 0.08)' : 'rgba(245, 158, 11, 0.08)',
                border: `1px solid ${normVerif.complete ? 'rgba(16, 185, 129, 0.25)' : 'rgba(245, 158, 11, 0.25)'}`,
                borderRadius: 'var(--radius-md)',
              }}
            >
              <h4 style={{ margin: '0 0 0.5rem 0', fontSize: '0.95rem', color: normVerif.complete ? '#10b981' : '#f59e0b' }}>
                {normVerif.complete ? 'Guidance for Policyholder' : 'Action Required on Your Claim'}
              </h4>
              <p style={{ margin: 0, fontSize: '0.875rem', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
                {normVerif.complete
                  ? 'All necessary documents have been verified. Your claim is proceeding to the next processing stage.'
                  : 'Please upload the missing documents or corrected versions listed below so that our team can continue evaluating your claim.'}
              </p>
            </div>
          )}

          {/* Missing Documents Section */}
          {normVerif.missingItems.length > 0 && (
            <div id="missing-documents-section" style={{ marginBottom: '1.25rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Missing Documents ({normVerif.missingItems.length})
              </label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-additional-docs)' }}>
                {normVerif.missingItems.map((item, i) => <li key={i}>{item}</li>)}
              </ul>
            </div>
          )}

          {/* Inconsistencies Section */}
          {normVerif.inconsistencies.length > 0 && (
            <div id="inconsistencies-section" style={{ marginBottom: '1.25rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Inconsistencies ({normVerif.inconsistencies.length})
              </label>
              <div className="document-list" style={{ marginTop: '0.5rem' }}>
                {normVerif.inconsistencies.map((inc, i) => (
                  <div key={i} className="document-item" style={{
                    borderColor: inc.severity === 'error'
                      ? 'rgba(239, 68, 68, 0.3)' : 'rgba(245, 158, 11, 0.3)',
                  }}>
                    <div className="document-icon" style={{
                      background: inc.severity === 'error'
                        ? 'rgba(239, 68, 68, 0.15)' : 'rgba(245, 158, 11, 0.15)',
                      color: inc.severity === 'error' ? '#ef4444' : '#f59e0b',
                    }}>
                      {inc.severity === 'error' ? '✕' : '!'}
                    </div>
                    <div className="document-info">
                      <h4>{inc.field}</h4>
                      <p>{inc.description}</p>
                    </div>
                    <span className={`status-badge ${
                      inc.severity === 'error' ? 'status-badge--rejected' : 'status-badge--riskassessment'
                    }`}>
                      {inc.severity}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Warnings Section */}
          {normVerif.warnings.length > 0 && (
            <div id="verification-warnings-section" style={{ marginBottom: '1.25rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Warnings ({normVerif.warnings.length})
              </label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-pending)' }}>
                {normVerif.warnings.map((w, i) => <li key={i}>{w}</li>)}
              </ul>
            </div>
          )}

          {/* Staff-Only Gemini AI Analysis Section */}
          {isStaff && (normVerif.aiUsed || normVerif.reasoningSummary) && (
            <div
              id="staff-gemini-analysis"
              style={{
                marginTop: '1.5rem',
                padding: '1.25rem',
                background: 'rgba(99, 102, 241, 0.06)',
                border: '1px solid rgba(99, 102, 241, 0.25)',
                borderRadius: 'var(--radius-md)',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                <h4 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#4f46e5' }}>
                  🤖 Gemini AI Analysis (Staff Only)
                </h4>
                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                  {normVerif.aiProvider && (
                    <span className="user-badge adjuster" style={{ fontSize: '0.75rem' }}>
                      Provider: {normVerif.aiProvider}
                    </span>
                  )}
                  {normVerif.aiModel && (
                    <span className="user-badge" style={{ fontSize: '0.75rem' }}>
                      Model: {normVerif.aiModel}
                    </span>
                  )}
                </div>
              </div>
              {normVerif.reasoningSummary && (
                <div style={{ marginTop: '0.5rem' }}>
                  <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                    AI Reasoning & Contextual Summary
                  </label>
                  <p style={{ margin: '0.35rem 0 0 0', color: 'var(--text-secondary)', lineHeight: '1.6', fontSize: '0.9rem' }}>
                    {normVerif.reasoningSummary}
                  </p>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Risk Assessment Result Panel */}
      {isStaff && riskResult && (
        <div
          id="risk-assessment-result-panel"
          className="card"
          style={{
            marginBottom: '2rem',
            borderColor: riskResult.riskLevelDisplay === 'High' || riskResult.riskLevelDisplay === 'Critical'
              ? 'rgba(239, 68, 68, 0.4)'
              : 'rgba(59, 130, 246, 0.4)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              🛡️ Risk Assessment Result
            </h3>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <span className={`status-badge status-badge--${(riskResult.riskLevelDisplay || '').toLowerCase()}`}>
                {riskResult.riskLevelDisplay || 'Assessed'}
              </span>
              <span className={`status-badge ${riskResult.recommendationDisplay === 'Escalate' ? 'under-investigation' : 'resolved'}`}>
                {riskResult.recommendationDisplay}
              </span>
            </div>
          </div>

          <div className="detail-grid" style={{ marginBottom: '1rem' }}>
            <div className="detail-item">
              <label>Risk Score</label>
              <div className="value" style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--color-accent)' }}>
                {riskResult.riskScore != null ? `${Number(riskResult.riskScore).toFixed(1)}/100` : '—'}
              </div>
            </div>
            <div className="detail-item">
              <label>Risk Level</label>
              <div className="value">{riskResult.riskLevelDisplay ?? '—'}</div>
            </div>
            <div className="detail-item">
              <label>Recommendation</label>
              <div className="value">{riskResult.recommendationDisplay ?? '—'}</div>
            </div>
            <div className="detail-item">
              <label>Fraud Flags</label>
              <div className="value">{riskResult.fraudFlagCount ?? riskResult.flags?.length ?? 0}</div>
            </div>
          </div>

          {/* AI Reasoning / Contextual Explanation */}
          {(riskResult.reasoningSummary || (riskResult.aiUsed && riskResult.summary) || (riskResult.summary?.includes('AI Context:'))) && (
            <div
              id="risk-ai-reasoning"
              style={{
                marginBottom: '1rem',
                padding: '0.85rem 1.25rem',
                background: 'rgba(59, 130, 246, 0.08)',
                border: '1px solid rgba(59, 130, 246, 0.25)',
                borderRadius: 'var(--radius-md)',
              }}
            >
              <h4 style={{ margin: '0 0 0.5rem 0', fontSize: '0.95rem', color: '#3b82f6', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                🤖 Gemini AI Contextual Explanation
                {riskResult.aiModel && (
                  <span style={{ fontSize: '0.75rem', fontWeight: 400, opacity: 0.8 }}>({riskResult.aiModel})</span>
                )}
              </h4>
              <p style={{ margin: 0, fontSize: '0.875rem', color: 'var(--text-secondary)', lineHeight: '1.6' }}>
                {riskResult.reasoningSummary || (riskResult.summary?.includes('AI Context:') ? riskResult.summary.split('AI Context:')[1].trim() : riskResult.summary)}
              </p>
            </div>
          )}

          {/* Fallback Notice */}
          {riskResult.fallbackUsed && (
            <div
              id="risk-fallback-notice"
              style={{
                marginBottom: '1rem',
                padding: '0.75rem 1rem',
                background: 'rgba(245, 158, 11, 0.1)',
                border: '1px solid rgba(245, 158, 11, 0.3)',
                borderRadius: 'var(--radius-sm)',
                color: 'var(--color-pending)',
                fontSize: '0.875rem',
              }}
            >
              ⚠️ Fallback Rule-Based Assessment was used (Gemini AI service unavailable, timed out, or quota exhausted). Deterministic rules remain 100% authoritative.
            </div>
          )}

          {/* Fraud Flags List */}
          {riskResult.flags && riskResult.flags.length > 0 && (
            <div id="risk-flags-section" style={{ marginTop: '1rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Fraud Flags ({riskResult.flags.length})
              </label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-rejected)' }}>
                {riskResult.flags.map((flag, i) => (
                  <li key={flag.id || i} style={{ marginBottom: '0.25rem' }}>
                    <strong>{flag.flagTypeDisplay || flag.flagType}</strong>: {flag.description} ({flag.severityDisplay || flag.severity})
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Assessment Metadata */}
          <div style={{ marginTop: '1rem', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
            Assessed: {formatDate(riskResult.assessmentTimestamp)} | Assessor: {riskResult.assessorType === 1 ? 'AI-Assisted' : riskResult.assessorType === 2 ? 'Manual' : 'System Rules'}
          </div>
        </div>
      )}

      {/* Documents Section */}
      <div className="card" style={{ marginBottom: '2rem' }}>
        <div className="card-header">
          <h3>Documents ({claim.documents?.length || 0})</h3>
        </div>

        {claim.documents?.length > 0 ? (
          <div className="document-list">
            {claim.documents.map((doc) => (
              <div key={doc.id} className="document-item" id={`doc-${doc.id}`} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flex: 1 }}>
                  <div className="document-icon">📄</div>
                  <div className="document-info">
                    <h4>{doc.fileName}</h4>
                    <p>{doc.documentType} • {formatFileSize(doc.fileSize)} • {formatDate(doc.uploadedAt)}</p>
                  </div>
                  <span className={getVerifStatusClass(doc.verificationStatus)}>
                    {doc.verificationStatus}
                  </span>
                </div>
                {canDeleteDocument && (
                  <button
                    id={`delete-doc-${doc.id}`}
                    className="btn btn--secondary btn--sm"
                    style={{ color: 'var(--color-rejected)', borderColor: 'rgba(239, 68, 68, 0.3)', marginLeft: '1rem' }}
                    onClick={() => handleDeleteDocument(doc)}
                    disabled={actionLoading === `doc-delete-${doc.id}`}
                  >
                    {actionLoading === `doc-delete-${doc.id}` ? 'Deleting…' : 'Delete'}
                  </button>
                )}
              </div>
            ))}
          </div>
        ) : (
          <div className="empty-state" style={{ padding: '2rem' }}>
            <p>No documents uploaded yet.</p>
          </div>
        )}

        {/* Upload Form */}
        {!isFinalStatus && (
          <form onSubmit={handleUpload} style={{
            marginTop: '1.25rem', paddingTop: '1.25rem',
            borderTop: '1px solid var(--border-color)',
            display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap',
          }}>
            <div className="form-group" style={{ flex: 1, minWidth: '200px', marginBottom: 0 }}>
              <label htmlFor="doc-file">File</label>
              <input
                id="doc-file"
                type="file"
                onChange={(e) => setUploadFile(e.target.files[0])}
                accept="image/*,.pdf,.doc,.docx,.txt"
              />
            </div>
            <div className="form-group" style={{ minWidth: '180px', marginBottom: 0 }}>
              <label htmlFor="doc-type">Document Type</label>
              <select
                id="doc-type"
                value={uploadDocType}
                onChange={(e) => setUploadDocType(e.target.value)}
              >
                {DOCUMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <button
              id="upload-doc-btn"
              type="submit"
              className="btn btn--primary btn--sm"
              disabled={!uploadFile || uploading}
            >
              {uploading ? 'Uploading…' : '📎 Upload'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

export default ClaimDetails;
