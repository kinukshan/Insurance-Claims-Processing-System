#!/usr/bin/env bash
# =============================================================================
# Run Newman API Tests for Insurance Claims Processing System
# SE3090 Assignment 1
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASE_URL="http://127.0.0.1:5080"
COLLECTION="$SCRIPT_DIR/InsuranceClaims_API_Tests.postman_collection.json"
ENVIRONMENT="$SCRIPT_DIR/InsuranceClaims_Local.postman_environment.json"
RESULTS_DIR="$SCRIPT_DIR/results"
REPORT_HTML="$RESULTS_DIR/newman-report.html"

mkdir -p "$RESULTS_DIR"

echo "=========================================================="
echo " Starting Newman API Test Execution"
echo "=========================================================="

# 1. Check if backend API is running
if ! curl -s -f -o /dev/null "$BASE_URL/api/PolicyTypes"; then
    echo "ERROR: Backend API is not responding at $BASE_URL."
    echo "Please start the API first before executing tests."
    echo "See testing/postman/README.md for instructions."
    exit 1
fi

# 2. Check for staff password
if [ -z "$STAFF_PASSWORD" ]; then
    echo -n "Enter Staff Password (Seed:StaffPassword): "
    read -s STAFF_PASSWORD
    echo
fi

if [ -z "$POLICYHOLDER_PASSWORD" ]; then
    POLICYHOLDER_PASSWORD="TestPass123!@#"
fi

# 3. Locate Newman binary
NEWMAN_BIN=""
if [ -x "$SCRIPT_DIR/node_modules/.bin/newman" ]; then
    NEWMAN_BIN="$SCRIPT_DIR/node_modules/.bin/newman"
elif command -v newman &>/dev/null; then
    NEWMAN_BIN="newman"
else
    echo "ERROR: Newman is not installed."
    echo "Run 'npm install' inside testing/postman or 'npm install -g newman newman-reporter-htmlextra'"
    exit 1
fi

echo "Using Newman: $NEWMAN_BIN"
echo "Target Base URL: $BASE_URL"
echo "Results Directory: $RESULTS_DIR"
echo "----------------------------------------------------------"

# 4. Execute tests
"$NEWMAN_BIN" run "$COLLECTION" \
  -e "$ENVIRONMENT" \
  --env-var "baseUrl=$BASE_URL" \
  --env-var "staffPassword=$STAFF_PASSWORD" \
  --env-var "policyholderPassword=$POLICYHOLDER_PASSWORD" \
  -r cli,htmlextra \
  --reporter-htmlextra-export "$REPORT_HTML"

echo "----------------------------------------------------------"
echo "Newman tests completed successfully!"
echo "HTML Report generated: $REPORT_HTML"
echo "=========================================================="
