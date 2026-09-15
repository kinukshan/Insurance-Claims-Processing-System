import React from 'react'
import { Routes, Route, NavLink, Navigate } from 'react-router-dom'
import ClaimsList from './pages/claims/ClaimsList'
import ClaimDetails from './pages/claims/ClaimDetails'

/**
 * Main App component — shared file, modify carefully.
 *
 * MODIFICATION (Arulkumaran): Added React Router routes for claims pages
 * and navigation header.
 */
function App() {
  return (
    <div className="app-layout">
      <header className="app-header">
        <h1>Insurance Claims Processing System</h1>
        <nav>
          <NavLink to="/claims" className={({ isActive }) => isActive ? 'active' : ''}>
            Claims
          </NavLink>
        </nav>
      </header>
      <main className="app-content">
        <Routes>
          <Route path="/" element={<Navigate to="/claims" replace />} />
          <Route path="/claims" element={<ClaimsList />} />
          <Route path="/claims/:id" element={<ClaimDetails />} />
        </Routes>
      </main>
    </div>
  )
}

export default App
