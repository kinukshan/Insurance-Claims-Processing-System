/**
 * Claim Details — Component B (Member 2)
 * Staff-facing claim detail view with documents, coverage validation,
 * and Document Verification Agent integration.
 */

import React, { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  getClaim,
  uploadDocument,
  validateCoverage,
  startWorkflow,
} from '../../services/claimService';

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

  const [claim, setClaim] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Upload state
  const [uploadFile, setUploadFile] = useState(null);
  const [uploadDocType, setUploadDocType] = useState('Supporting Document');
  const [uploading, setUploading] = useState(false);

  // Verification / Coverage state
  const [verificationResult, setVerificationResult] = useState(null);
  const [coverageResult, setCoverageResult] = useState(null);
  const [actionLoading, setActionLoading] = useState(null);

  const fetchClaim = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getClaim(id);
      setClaim(data);
    } catch (err) {
      setError(err.message || 'Failed to load claim');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchClaim();
  }, [fetchClaim]);

  const handleUpload = async (e) => {
    e.preventDefault();
    if (!uploadFile) return;
    setUploading(true);
    try {
      await uploadDocument(id, uploadFile, uploadDocType);
      setUploadFile(null);
      setUploadDocType('Supporting Document');
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
    } catch (err) {
      setCoverageResult({ isValid: false, issues: [err.message] });
    } finally {
      setActionLoading(null);
    }
  };

  const handleStartWorkflow = async () => {
    setActionLoading('workflow');
    setVerificationResult(null);
    try {
      const result = await startWorkflow(id);
      setVerificationResult(result);
    } catch (err) {
      setVerificationResult({
        complete: false,
        missingItems: [],
        inconsistencies: [],
        warnings: [err.message],
      });
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
        <div style={{ display: 'flex', gap: '0.5rem' }}>
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
        </div>
      </div>

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
        <h3 style={{ marginBottom: '0.75rem' }}>Description</h3>
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

      {/* Document Verification Result */}
      {verificationResult && (
        <div className="card" style={{
          marginBottom: '2rem',
          borderColor: verificationResult.complete ? 'rgba(16, 185, 129, 0.3)' : 'rgba(245, 158, 11, 0.3)',
        }}>
          <h3 style={{ marginBottom: '1rem' }}>
            {verificationResult.complete ? '✅' : '⚠️'} Document Verification Result
          </h3>
          <p style={{ color: 'var(--text-secondary)', marginBottom: '1rem' }}>
            {verificationResult.complete
              ? 'All required documents are present and consistent.'
              : 'Verification found issues that need attention.'}
          </p>

          {verificationResult.missingItems?.length > 0 && (
            <div style={{ marginBottom: '1rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Missing Documents
              </label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-additional-docs)' }}>
                {verificationResult.missingItems.map((item, i) => <li key={i}>{item}</li>)}
              </ul>
            </div>
          )}

          {verificationResult.inconsistencies?.length > 0 && (
            <div style={{ marginBottom: '1rem' }}>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Inconsistencies
              </label>
              <div className="document-list" style={{ marginTop: '0.5rem' }}>
                {verificationResult.inconsistencies.map((inc, i) => (
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

          {verificationResult.warnings?.length > 0 && (
            <div>
              <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                Warnings
              </label>
              <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: 'var(--color-pending)' }}>
                {verificationResult.warnings.map((w, i) => <li key={i}>{w}</li>)}
              </ul>
            </div>
          )}
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
              <div key={doc.id} className="document-item" id={`doc-${doc.id}`}>
                <div className="document-icon">📄</div>
                <div className="document-info">
                  <h4>{doc.fileName}</h4>
                  <p>{doc.documentType} • {formatFileSize(doc.fileSize)} • {formatDate(doc.uploadedAt)}</p>
                </div>
                <span className={getVerifStatusClass(doc.verificationStatus)}>
                  {doc.verificationStatus}
                </span>
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
