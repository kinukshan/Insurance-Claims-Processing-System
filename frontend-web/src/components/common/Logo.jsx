import React from 'react'

/**
 * Shield-inspired SVG logo for Insurance Claims Processing System.
 * Original design — not copied from any existing insurer.
 */
function Logo({ size = 38, className = '' }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 48 48"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={`logo-svg ${className}`}
      aria-hidden="true"
    >
      {/* Shield body */}
      <path
        d="M24 4L6 12v12c0 11.1 7.7 21.5 18 24 10.3-2.5 18-12.9 18-24V12L24 4Z"
        fill="#247D87"
      />
      {/* Inner shield highlight */}
      <path
        d="M24 8L10 14.5v9.5c0 9.2 6.3 17.8 14 19.8V8Z"
        fill="#2E949F"
        opacity="0.6"
      />
      {/* Checkmark / protection symbol */}
      <path
        d="M20 26l-4-4-2 2 6 6 12-12-2-2-10 10Z"
        fill="#ffffff"
      />
      {/* Gold accent bar */}
      <rect x="14" y="33" width="20" height="2.5" rx="1.25" fill="#E8B849" />
    </svg>
  )
}

export default Logo
