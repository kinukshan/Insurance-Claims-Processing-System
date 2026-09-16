// Fraud case history — Component C (Member 3)
// Staff-facing fraud case history with search by policyholder and status updates

import React, { useState } from 'react'
import { getFraudHistory, updateFraudCase } from '../../services/riskService'
import './risk.css'

const STATUS_CLASS = {
  Open: 'open',
  UnderInvestigation: 'under-investigation',
  Resolved: 'resolved',
  Dismissed: 'dismissed',
}

function FraudCaseHistory() {
  const [policyholderId, setPolicyholderId] = useState('')
  const [cases, setCases] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [searched, setSearched] = useState(false)
  const [editingCase, setEditingCase] = useState(null)
  const [editStatus, setEditStatus] = useState('')
  const [editNotes, setEditNotes] = useState('')
  const [editResolution, setEditResolution] = useState('')

  async function handleSearch(e) {
    e.preventDefault()
    if (!policyholderId.trim()) return

    setLoading(true)
    setError(null)
    setSearched(true)
    try {
      const data = await getFraudHistory(policyholderId.trim())
      setCases(data || [])
    } catch (err) {
      setError(err.message || 'Failed to load fraud history')
    } finally {
      setLoading(false)
    }
  }

  function startEdit(fraudCase) {
    setEditingCase(fraudCase.id)
    setEditStatus(fraudCase.statusDisplay || 'Open')
    setEditNotes(fraudCase.notes || '')
    setEditResolution(fraudCase.resolution || '')
  }

  async function handleUpdate() {
    if (!editingCase) return
    try {
      const updateData = { status: editStatus }
      if (editNotes) updateData.notes = editNotes
      if (editResolution) updateData.resolution = editResolution

      await updateFraudCase(editingCase, updateData)
      setEditingCase(null)
      // Refresh
      const data = await getFraudHistory(policyholderId.trim())
      setCases(data || [])
    } catch (err) {
      setError(err.message || 'Update failed')
    }
  }

  return (
    <div className="risk-dashboard">
      <h2>Fraud Case History</h2>

      {/* Search */}
      <form onSubmit={handleSearch}>
        <div className="risk-search-bar">
          <input
            id="search-policyholder"
            type="text"
            placeholder="Enter Policyholder ID..."
            value={policyholderId}
            onChange={(e) => setPolicyholderId(e.target.value)}
          />
          <button type="submit" disabled={loading}>
            {loading ? 'Searching...' : 'Search'}
          </button>
        </div>
      </form>

      {/* Error */}
      {error && (
        <div className="state-message error">
          <p>{error}</p>
        </div>
      )}

      {/* Loading */}
      {loading && (
        <div className="loading-spinner"><div className="spinner" /></div>
      )}

      {/* Results */}
      {!loading && searched && cases.length === 0 && (
        <div className="state-message">
          <div className="icon">📁</div>
          <p>No fraud cases found for this policyholder.</p>
        </div>
      )}

      {!loading && cases.length > 0 && (
        <div className="risk-table-wrapper">
          <table className="risk-table" id="fraud-history-table">
            <thead>
              <tr>
                <th>Case ID</th>
                <th>Claim ID</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Assigned To</th>
                <th>Created</th>
                <th>Closed</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {cases.map((fc) => (
                <tr key={fc.id}>
                  <td title={fc.id}>{fc.id?.substring(0, 8)}...</td>
                  <td title={fc.claimId}>{fc.claimId?.substring(0, 8)}...</td>
                  <td>
                    <span className={`status-badge ${STATUS_CLASS[fc.statusDisplay] || 'open'}`}>
                      {fc.statusDisplay}
                    </span>
                  </td>
                  <td>
                    <span className={`severity-badge ${(fc.priorityDisplay || 'medium').toLowerCase()}`}>
                      {fc.priorityDisplay}
                    </span>
                  </td>
                  <td>{fc.assignedReviewer || '—'}</td>
                  <td>{new Date(fc.createdAt).toLocaleDateString()}</td>
                  <td>{fc.closedAt ? new Date(fc.closedAt).toLocaleDateString() : '—'}</td>
                  <td>
                    {editingCase === fc.id ? (
                      <div style={{ display: 'flex', gap: '0.5rem', flexDirection: 'column' }}>
                        <select value={editStatus} onChange={(e) => setEditStatus(e.target.value)} style={{ padding: '0.3rem' }}>
                          <option value="Open">Open</option>
                          <option value="UnderInvestigation">Under Investigation</option>
                          <option value="Resolved">Resolved</option>
                          <option value="Dismissed">Dismissed</option>
                        </select>
                        <input
                          type="text"
                          placeholder="Resolution..."
                          value={editResolution}
                          onChange={(e) => setEditResolution(e.target.value)}
                          style={{ padding: '0.3rem', fontSize: '0.8rem' }}
                        />
                        <div style={{ display: 'flex', gap: '0.3rem' }}>
                          <button className="btn btn-primary" onClick={handleUpdate} style={{ fontSize: '0.75rem', padding: '0.3rem 0.5rem' }}>Save</button>
                          <button className="btn btn-secondary" onClick={() => setEditingCase(null)} style={{ fontSize: '0.75rem', padding: '0.3rem 0.5rem' }}>Cancel</button>
                        </div>
                      </div>
                    ) : (
                      <button
                        className="btn btn-secondary"
                        onClick={() => startEdit(fc)}
                        style={{ fontSize: '0.75rem' }}
                        id={`edit-case-${fc.id}`}
                      >
                        Update
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

export default FraudCaseHistory
