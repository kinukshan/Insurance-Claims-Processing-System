import React from 'react'

/**
 * Coverage details table component.
 */
function CoverageTable({ coverages }) {
  if (!coverages || coverages.length === 0) {
    return <p style={{ color: '#9ca3af', fontStyle: 'italic' }}>No coverage details available.</p>
  }

  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
      <thead>
        <tr style={{ backgroundColor: '#f9fafb', borderBottom: '2px solid #e5e7eb' }}>
          <th style={{ textAlign: 'left', padding: '8px 12px' }}>Type</th>
          <th style={{ textAlign: 'left', padding: '8px 12px' }}>Description</th>
          <th style={{ textAlign: 'right', padding: '8px 12px' }}>Limit</th>
          <th style={{ textAlign: 'right', padding: '8px 12px' }}>Deductible</th>
          <th style={{ textAlign: 'right', padding: '8px 12px' }}>Coverage %</th>
          <th style={{ textAlign: 'center', padding: '8px 12px' }}>Active</th>
        </tr>
      </thead>
      <tbody>
        {coverages.map((cov) => (
          <tr key={cov.id} style={{ borderBottom: '1px solid #e5e7eb' }}>
            <td style={{ padding: '8px 12px', fontWeight: 500 }}>{cov.coverageType}</td>
            <td style={{ padding: '8px 12px', color: '#6b7280' }}>{cov.description || '—'}</td>
            <td style={{ padding: '8px 12px', textAlign: 'right' }}>${Number(cov.coverageLimit).toLocaleString()}</td>
            <td style={{ padding: '8px 12px', textAlign: 'right' }}>${Number(cov.deductibleAmount).toLocaleString()}</td>
            <td style={{ padding: '8px 12px', textAlign: 'right' }}>{cov.percentageOfCoverage}%</td>
            <td style={{ padding: '8px 12px', textAlign: 'center' }}>{cov.isActive ? '✓' : '✗'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

export default CoverageTable
