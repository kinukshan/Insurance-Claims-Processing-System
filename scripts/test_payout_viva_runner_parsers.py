#!/usr/bin/env python3
"""
Regression test suite for Payout Viva Runner result parsers.

Verifies that all four component parsers (dotnet, pytest, Vitest, flutter)
correctly handle ANSI escape codes, spacing variations, mixed statuses,
edge cases, and status determination rules.
"""

import unittest
import sys
from pathlib import Path

# Add scripts directory to path
SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))

from payout_viva_runner import (
    TestResult,
    strip_ansi,
    parse_vitest_output,
    parse_dotnet_output,
    parse_pytest_output,
    parse_flutter_output,
    determine_status,
)


class TestStripAnsi(unittest.TestCase):
    """Test ANSI escape sequence removal."""

    def test_strip_color_and_formatting(self):
        colored = "\x1b[1m\x1b[32mTests\x1b[39m\x1b[22m  \x1b[32m76 passed\x1b[39m (76)"
        self.assertEqual(strip_ansi(colored), "Tests  76 passed (76)")

    def test_strip_osc_sequences(self):
        osc = "\x1b]0;Terminal Title\x07Clean Text"
        self.assertEqual(strip_ansi(osc), "Clean Text")

    def test_empty_string(self):
        self.assertEqual(strip_ansi(""), "")

    def test_plain_text_unchanged(self):
        plain = "Plain output without ANSI"
        self.assertEqual(strip_ansi(plain), plain)


class TestVitestParser(unittest.TestCase):
    """Test Vitest result parser against real and synthetic outputs."""

    def test_vitest_with_ansi_codes_exact_reproduction(self):
        """The bug reported by the user: ANSI codes inside summary line."""
        output = (
            "\x1b[1m\x1b[32m ✓ \x1b[39m\x1b[22m"
            "src/__tests__/payout/PayoutApproval.test.jsx > PayoutApproval > displays payment 10ms\n"
            "   Test Files  5 passed (5)\n"
            "      Tests  \x1b[32m76 passed\x1b[39m (76)\n"
            "   Duration  1.05s\n"
        )
        result = TestResult("React Payout")
        parse_vitest_output(output, result)
        self.assertEqual(result.passed, 76)
        self.assertEqual(result.failed, 0)
        self.assertEqual(result.skipped, 0)
        self.assertEqual(result.total, 76)
        self.assertEqual(len(result.test_names), 1)

    def test_vitest_clean_all_passed(self):
        output = (
            " ✓ src/__tests__/payout/PayoutApproval.test.jsx > PayoutApproval > test 1 10ms\n"
            " ✓ src/__tests__/payout/PayoutApproval.test.jsx > PayoutApproval > test 2 12ms\n"
            " Test Files  5 passed (5)\n"
            "      Tests  76 passed (76)\n"
            "   Start at  00:05:41\n"
        )
        result = TestResult("React Payout")
        parse_vitest_output(output, result)
        self.assertEqual(result.passed, 76)
        self.assertEqual(result.failed, 0)
        self.assertEqual(result.skipped, 0)
        self.assertEqual(result.total, 76)
        self.assertEqual(len(result.test_names), 2)

    def test_vitest_mixed_results(self):
        output = (
            " ✓ src/__tests__/payout/test1.js > ok 5ms\n"
            " × src/__tests__/payout/test2.js > broken 15ms\n"
            " Test Files  1 failed | 4 passed (5)\n"
            "      Tests  70 passed | 2 failed | 4 skipped (76)\n"
        )
        result = TestResult("React Payout")
        parse_vitest_output(output, result)
        self.assertEqual(result.passed, 70)
        self.assertEqual(result.failed, 2)
        self.assertEqual(result.skipped, 4)
        self.assertEqual(result.total, 76)
        self.assertEqual(len(result.failure_details), 1)

    def test_vitest_fallback_from_row_marks(self):
        """When summary line is missing, derive counts from row marks."""
        output = (
            " ✓ src/__tests__/payout/t1.js > pass 1\n"
            " ✓ src/__tests__/payout/t2.js > pass 2\n"
            " ✕ src/__tests__/payout/t3.js > fail 1\n"
        )
        result = TestResult("React Payout")
        parse_vitest_output(output, result)
        self.assertEqual(result.passed, 2)
        self.assertEqual(result.failed, 1)
        self.assertEqual(result.total, 3)


class TestDotnetParser(unittest.TestCase):
    """Test ASP.NET Core dotnet test output parser."""

    def test_dotnet_passed_clean(self):
        output = (
            "Passed!  - Failed:     0, Passed:   184, Skipped:     0, Total:   184, Duration: 1 s "
            "- InsuranceClaims.UnitTests.dll (net10.0)\n"
        )
        result = TestResult("Backend Payout")
        parse_dotnet_output(output, result)
        self.assertEqual(result.passed, 184)
        self.assertEqual(result.failed, 0)
        self.assertEqual(result.skipped, 0)
        self.assertEqual(result.total, 184)

    def test_dotnet_with_ansi_and_failures(self):
        output = (
            "\x1b[31mFailed!\x1b[0m  - Failed:     2, Passed:   182, Skipped:     1, Total:   185, "
            "Duration: 2 s - InsuranceClaims.UnitTests.dll (net10.0)\n"
        )
        result = TestResult("Backend Payout")
        parse_dotnet_output(output, result)
        self.assertEqual(result.passed, 182)
        self.assertEqual(result.failed, 2)
        self.assertEqual(result.skipped, 1)
        self.assertEqual(result.total, 185)


class TestPytestParser(unittest.TestCase):
    """Test pytest output parser."""

    def test_pytest_clean_all_passed(self):
        output = (
            "tests/test_payout_validation.py::test_one PASSED\n"
            "tests/test_payout_validation.py::test_two PASSED\n"
            "============================== 35 passed in 0.28s ==============================\n"
        )
        result = TestResult("AI Payout")
        parse_pytest_output(output, result)
        self.assertEqual(result.passed, 35)
        self.assertEqual(result.failed, 0)
        self.assertEqual(result.skipped, 0)
        self.assertEqual(result.total, 35)
        self.assertEqual(len(result.test_names), 2)

    def test_pytest_with_failures_and_errors(self):
        output = (
            "FAILED tests/test_payout_validation.py::test_fail\n"
            "=================== 30 passed, 2 failed, 3 skipped, 1 error in 0.45s ===================\n"
        )
        result = TestResult("AI Payout")
        parse_pytest_output(output, result)
        self.assertEqual(result.passed, 30)
        # 2 failed + 1 error = 3
        self.assertEqual(result.failed, 3)
        self.assertEqual(result.skipped, 3)
        self.assertEqual(result.total, 36)
        self.assertEqual(len(result.failure_details), 1)


class TestFlutterParser(unittest.TestCase):
    """Test Flutter expanded reporter parser."""

    def test_flutter_expanded_clean_passed(self):
        output = (
            "00:00 +0: loading test/payout/payout_model_test.dart\n"
            "00:00 +1: Payout Model fromJson creates Payout with correct fields\n"
            "00:00 +12: All tests passed!\n"
        )
        result = TestResult("Flutter Payout")
        parse_flutter_output(output, result)
        self.assertEqual(result.passed, 12)
        self.assertEqual(result.failed, 0)
        self.assertEqual(result.skipped, 0)
        self.assertEqual(result.total, 12)
        self.assertIn("Payout Model fromJson creates Payout with correct fields", result.test_names)
        # Summary and loading lines should be excluded from test names
        self.assertNotIn("All tests passed!", result.test_names)

    def test_flutter_expanded_with_failures(self):
        output = (
            "00:00 +10 ~1 -1: Some tests failed.\n"
        )
        result = TestResult("Flutter Payout")
        parse_flutter_output(output, result)
        self.assertEqual(result.passed, 10)
        self.assertEqual(result.skipped, 1)
        self.assertEqual(result.failed, 1)
        self.assertEqual(result.total, 12)


class TestDetermineStatus(unittest.TestCase):
    """Test status decision logic."""

    def test_all_passed(self):
        r = TestResult("Test")
        r.passed = 76
        r.total = 76
        r.exit_code = 0
        determine_status(r)
        self.assertEqual(r.status, "PASS")

    def test_zero_passed_with_total_must_not_pass(self):
        """CRITICAL: Must NOT report PASS when 0 tests were counted as passed despite total > 0."""
        r = TestResult("Test")
        r.passed = 0
        r.failed = 0
        r.skipped = 0
        r.total = 76
        r.exit_code = 0
        determine_status(r)
        self.assertNotEqual(r.status, "PASS")
        self.assertEqual(r.status, "ERROR")

    def test_nonzero_exit_code_must_not_pass(self):
        """Process failure must not report PASS even if failures weren't parsed."""
        r = TestResult("Test")
        r.passed = 10
        r.total = 10
        r.exit_code = 1
        determine_status(r)
        self.assertEqual(r.status, "ERROR")

    def test_failed_tests(self):
        r = TestResult("Test")
        r.passed = 70
        r.failed = 2
        r.total = 72
        r.exit_code = 1
        determine_status(r)
        self.assertEqual(r.status, "FAIL")

    def test_no_tests(self):
        r = TestResult("Test")
        r.total = 0
        r.exit_code = 0
        determine_status(r)
        self.assertEqual(r.status, "NO TESTS")

    def test_skipped_only(self):
        r = TestResult("Test")
        r.passed = 0
        r.skipped = 5
        r.total = 5
        r.exit_code = 0
        determine_status(r)
        self.assertEqual(r.status, "SKIP")


if __name__ == "__main__":
    unittest.main()
