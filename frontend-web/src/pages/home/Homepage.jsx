import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import Logo from '../../components/common/Logo'

// Import images
import heroFamily from '../../assets/images/hero-family.jpg'
import motorInsurance from '../../assets/images/motor-insurance.jpg'
import lifeInsurance from '../../assets/images/life-insurance.jpg'
import healthInsurance from '../../assets/images/health-insurance.jpg'
import homeInsurance from '../../assets/images/home-insurance.jpg'
import portalBanner from '../../assets/images/portal-banner.jpg'

/**
 * Public-facing Homepage — visible to unauthenticated visitors.
 * Authenticated users are redirected to /dashboard by App.jsx routing.
 */
function Homepage() {
  const { isAuthenticated } = useAuth()

  const claimLink = isAuthenticated ? '/claims' : '/login'
  const policyLink = isAuthenticated ? '/policies' : '/login'
  const dashboardLink = isAuthenticated ? '/dashboard' : '/login'

  return (
    <div className="homepage fade-in">
      {/* ── Hero Section ── */}
      <section className="hero-section" aria-label="Hero">
        <div className="hero-content-wrapper">
          <div className="hero-text">
            <h1>
              Protect What<br />
              <span>Matters Most</span>
            </h1>
            <p>
              Manage your insurance policies, submit and track claims,
              view payouts and access important documents—all in one secure place.
            </p>
            <div className="hero-cta">
              <Link to={claimLink} className="btn btn--primary">
                Submit Claim →
              </Link>
              <Link to={policyLink} className="btn btn--outline">
                View Policies →
              </Link>
            </div>
            <div className="hero-trust-badges">
              <div className="trust-badge">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
                </svg>
                <span>Trusted by<br/>Sri Lankan Families</span>
              </div>
              <div className="trust-badge">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>
                </svg>
                <span>Secure &amp; Easy<br/>to Use</span>
              </div>
              <div className="trust-badge">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
                </svg>
                <span>Fast Claims<br/>Processing</span>
              </div>
            </div>
          </div>
          <div className="hero-image">
            <img
              src={heroFamily}
              alt="A happy Sri Lankan family sitting together in their living room"
              width="420"
              height="380"
            />
            <div className="hero-image-decoration" aria-hidden="true" />
          </div>
        </div>
      </section>

      {/* ── Insurance Product Cards ── */}
      <section className="products-section" aria-label="Insurance Products">
        <div className="section-label">Our Products</div>
        <h2 className="section-title">Insurance Solutions for Every Need</h2>
        <p className="section-subtitle">
          Comprehensive coverage options to protect your family, health, vehicle and property.
        </p>
        <div className="product-cards-grid">
          <Link to={policyLink} className="product-card">
            <img
              src={motorInsurance}
              alt="A modern car on a scenic Sri Lankan road"
              className="product-card-image"
              loading="lazy"
            />
            <div className="product-card-body">
              <h3>Motor Insurance</h3>
              <p>Stay protected on every journey with comprehensive vehicle coverage.</p>
              <span className="product-card-link">Learn more →</span>
            </div>
          </Link>

          <Link to={policyLink} className="product-card">
            <img
              src={lifeInsurance}
              alt="A multi-generational Sri Lankan family together in a garden"
              className="product-card-image"
              loading="lazy"
            />
            <div className="product-card-body">
              <h3>Life Insurance</h3>
              <p>Financial security for a brighter tomorrow for you and your loved ones.</p>
              <span className="product-card-link">Learn more →</span>
            </div>
          </Link>

          <Link to={policyLink} className="product-card">
            <img
              src={healthInsurance}
              alt="A doctor consulting with a patient in a modern medical clinic"
              className="product-card-image"
              loading="lazy"
            />
            <div className="product-card-body">
              <h3>Health Insurance</h3>
              <p>Quality healthcare support for you and your family when you need it most.</p>
              <span className="product-card-link">Learn more →</span>
            </div>
          </Link>

          <Link to={policyLink} className="product-card">
            <img
              src={homeInsurance}
              alt="A beautiful Sri Lankan home surrounded by tropical gardens"
              className="product-card-image"
              loading="lazy"
            />
            <div className="product-card-body">
              <h3>Home Insurance</h3>
              <p>Protect what you've built with confidence and comprehensive property coverage.</p>
              <span className="product-card-link">Learn more →</span>
            </div>
          </Link>
        </div>
      </section>

      {/* ── Quick Services ── */}
      <section className="services-section" aria-label="Quick Services">
        <div className="section-label">Quick Services</div>
        <h2 className="section-title" style={{ marginBottom: '2rem' }}>
          Everything You Need, Right at Your Fingertips
        </h2>
        <div className="services-grid">
          <Link to={claimLink} className="service-card">
            <div className="service-card-icon">📝</div>
            <div>
              <h4>Submit a Claim</h4>
              <p>Start a new claim easily</p>
            </div>
          </Link>
          <Link to={claimLink} className="service-card">
            <div className="service-card-icon">🔍</div>
            <div>
              <h4>Track Claim Status</h4>
              <p>Get real-time updates</p>
            </div>
          </Link>
          <Link to={policyLink} className="service-card">
            <div className="service-card-icon">📋</div>
            <div>
              <h4>View Policies</h4>
              <p>Browse your coverage</p>
            </div>
          </Link>
          <Link to={isAuthenticated ? '/payouts' : '/login'} className="service-card">
            <div className="service-card-icon">💳</div>
            <div>
              <h4>Payout History</h4>
              <p>Check settlement status</p>
            </div>
          </Link>
          <Link to={isAuthenticated ? '/notifications' : '/login'} className="service-card">
            <div className="service-card-icon">🔔</div>
            <div>
              <h4>Notifications</h4>
              <p>View notification history</p>
            </div>
          </Link>
        </div>
      </section>

      {/* ── Why Use Our Portal ── */}
      <section className="features-section" aria-label="Portal Features">
        <div className="features-inner">
          <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
            <div className="section-label" style={{ textAlign: 'center' }}>Why Choose Us</div>
            <h2 className="section-title" style={{ textAlign: 'center' }}>
              Why Use Our Claims Portal?
            </h2>
            <p className="section-subtitle" style={{ textAlign: 'center', margin: '0.5rem auto 0' }}>
              Modern tools to simplify your insurance experience
            </p>
          </div>
          <div className="features-grid">
            <div className="feature-card">
              <div className="feature-icon">📤</div>
              <h4>Digital Claim Submission</h4>
              <p>Submit claims online anytime, from anywhere. No paperwork, no queues.</p>
            </div>
            <div className="feature-card">
              <div className="feature-icon">📎</div>
              <h4>Document Upload</h4>
              <p>Upload supporting documents securely with our built-in document management system.</p>
            </div>
            <div className="feature-card">
              <div className="feature-icon">📊</div>
              <h4>Claim Status Tracking</h4>
              <p>Track your claim progress in real-time through every stage of processing.</p>
            </div>
            <div className="feature-card">
              <div className="feature-icon">🔔</div>
              <h4>Notification History</h4>
              <p>Stay informed with email notifications for every important update on your claims.</p>
            </div>
            <div className="feature-card">
              <div className="feature-icon">🔒</div>
              <h4>Secure Access</h4>
              <p>Your policy information is protected with role-based access control and secure authentication.</p>
            </div>
          </div>
        </div>
      </section>

      {/* ── Portal CTA Banner ── */}
      <section className="portal-banner" aria-label="Customer Portal">
        <div className="portal-banner-inner">
          <div className="portal-banner-text">
            <div className="portal-banner-label">★ Your Insurance, Always With You</div>
            <h2>Manage Your Claims and Policies Online</h2>
            <p>
              Access your policies, submit claims, track progress and stay updated
              through your secure insurance portal.
            </p>
            <Link to={dashboardLink} className="btn btn--accent btn--lg">
              Go to Customer Portal →
            </Link>
          </div>
          <div className="portal-banner-image">
            <img
              src={portalBanner}
              alt="A Sri Lankan professional managing insurance online on a laptop"
              loading="lazy"
              width="380"
              height="260"
            />
          </div>
        </div>
      </section>

      {/* ── Footer ── */}
      <footer className="site-footer" role="contentinfo">
        <div className="footer-inner">
          <div className="footer-brand">
            <div className="footer-logo">
              <Logo size={32} />
              Insurance Claims<br />Processing System
            </div>
            <p>
              Your trusted partner for managing insurance policies, claims and payouts securely online.
            </p>
          </div>
          <div className="footer-column">
            <h4>Quick Links</h4>
            <ul>
              <li><Link to="/">Home</Link></li>
              <li><Link to={policyLink}>Policies</Link></li>
              <li><Link to={claimLink}>Claims</Link></li>
              <li><Link to={isAuthenticated ? '/payouts' : '/login'}>Payouts</Link></li>
            </ul>
          </div>
          <div className="footer-column">
            <h4>Services</h4>
            <ul>
              <li><Link to={claimLink}>Submit a Claim</Link></li>
              <li><Link to={claimLink}>Track Claims</Link></li>
              <li><Link to={isAuthenticated ? '/notifications' : '/login'}>Notifications</Link></li>
            </ul>
          </div>
          <div className="footer-column">
            <h4>Account</h4>
            <ul>
              <li><Link to={isAuthenticated ? '/dashboard' : '/login'}>{isAuthenticated ? 'Dashboard' : 'Sign In'}</Link></li>
              {!isAuthenticated && <li><Link to="/register">Register</Link></li>}
            </ul>
          </div>
        </div>
        <div className="footer-bottom">
          <span>© {new Date().getFullYear()} Insurance Claims Processing System. All rights reserved.</span>
          <span>Built for educational purposes</span>
        </div>
      </footer>
    </div>
  )
}

export default Homepage
