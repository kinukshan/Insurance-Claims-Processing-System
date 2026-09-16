/**
 * Claims List — Component B (Member 2)
 * Staff-facing claims list with search, filter, and navigation to details.
 */

import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { getAllClaims } from '../../services/claimService';

const STATUSES = [
  'Draft', 'Submitted', 'UnderReview', 'DocumentVerification',
  'AdditionalDocumentsRequired', 'RiskAssessment', 'PendingApproval',
  'Approved', 'Rejected', 'Withdrawn', 'PayoutProcessing', 'Closed',
];

function ClaimsList() {
  const navigate = useNavigate();
  const [claims, setClaims] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [searchInput, setSearchInput] = useState('');

  const fetchClaims = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAllClaims({
        status: statusFilter || undefined,
        search: searchTerm || undefined,
      });
      setClaims(data);
    } catch (err) {
      setError(err.message || 'Failed to load claims');
    } finally {
      setLoading(false);
    }
  }, [statusFilter, searchTerm]);

  useEffect(() => {
    fetchClaims();
  }, [fetchClaims]);

  const handleSearch = (e) => {
    e.preventDefault();
    setSearchTerm(searchInput);
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric',
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

  // Loading state
  if (loading) {
    return (
      <div className="fade-in">
        <div className="page-header">
          <h2>Claims Management</h2>
        </div>
        <div className="loading-state">
          <div className="loading-spinner" />
          <p>Loading claims…</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="fade-in">
        <div className="page-header">
          <h2>Claims Management</h2>
        </div>
        <div className="error-state">
          <p>⚠️ {error}</p>
          <button className="btn btn--primary" onClick={fetchClaims} style={{ marginTop: '1rem' }}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="fade-in">
      <div className="page-header">
        <h2>Claims Management</h2>
        <span className="btn btn--secondary btn--sm" style={{ cursor: 'default' }}>
          {claims.length} claim{claims.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Search & Filter Bar */}
      <form className="search-filter-bar" onSubmit={handleSearch}>
        <input
          id="claims-search-input"
          className="search-input"
          type="text"
          placeholder="🔍  Search by claim number, description, or location…"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
        <select
          id="claims-status-filter"
          className="filter-select"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
        >
          <option value="">All Statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{s.replace(/([A-Z])/g, ' $1').trim()}</option>
          ))}
        </select>
        <button type="submit" className="btn btn--primary btn--sm">Search</button>
      </form>

      {/* Empty state */}
      {claims.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📋</div>
          <p>No claims found{statusFilter || searchTerm ? ' matching your filters' : ''}.</p>
        </div>
      ) : (
        /* Claims table */
        <div className="card">
          <table className="data-table" id="claims-table">
            <thead>
              <tr>
                <th>Claim #</th>
                <th>Type</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Incident Date</th>
                <th>Submitted</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {claims.map((claim) => (
                <tr
                  key={claim.id}
                  onClick={() => navigate(`/claims/${claim.id}`)}
                  id={`claim-row-${claim.id}`}
                >
                  <td style={{ fontWeight: 500, color: 'var(--color-primary-light)' }}>
                    {claim.claimNumber}
                  </td>
                  <td>{claim.claimType}</td>
                  <td>{formatCurrency(claim.claimedAmount)}</td>
                  <td>
                    <span className={getStatusClass(claim.status)}>
                      {claim.status.replace(/([A-Z])/g, ' $1').trim()}
                    </span>
                  </td>
                  <td>{formatDate(claim.incidentDate)}</td>
                  <td>{formatDate(claim.submittedAt)}</td>
                  <td>{formatDate(claim.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default ClaimsList;
