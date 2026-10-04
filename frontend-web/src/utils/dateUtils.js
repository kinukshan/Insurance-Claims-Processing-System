/**
 * Date-only formatting and calculation utilities using local calendar dates.
 * Avoids UTC shifting issues in positive/negative timezones.
 */

/**
 * Format a Date object (or date string/timestamp) to local 'YYYY-MM-DD'.
 * Uses local getFullYear, getMonth, getDate to avoid UTC calendar shifts.
 */
export function formatLocalDate(date = new Date()) {
  const d = date instanceof Date ? date : new Date(date);
  if (isNaN(d.getTime())) return '';
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Adds specified days to a local 'YYYY-MM-DD' string, returning a new 'YYYY-MM-DD'.
 */
export function addDaysToLocalDate(dateStr, days = 1) {
  if (!dateStr) return '';
  const [y, m, d] = dateStr.split('-').map(Number);
  const date = new Date(y, m - 1, d + days);
  return formatLocalDate(date);
}

/**
 * Adds specified years to a local 'YYYY-MM-DD' string, returning a new 'YYYY-MM-DD'.
 */
export function addYearsToLocalDate(dateStr, years = 1) {
  if (!dateStr) return '';
  const [y, m, d] = dateStr.split('-').map(Number);
  const date = new Date(y + years, m - 1, d);
  return formatLocalDate(date);
}
