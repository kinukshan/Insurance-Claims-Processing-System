#!/usr/bin/env python3
"""
Insurance Claims Management System — Payout Management Viva Testing Runner.

Interactive terminal program and CLI tool for executing payout-specific
automated tests across all four application components:
  1. ASP.NET Core Backend / API  (xUnit / dotnet test)
  2. Python Agentic AI Service   (pytest)
  3. React Web Frontend          (Vitest)
  4. Flutter Mobile Application  (flutter test)

Usage:
  Interactive:   python3 scripts/payout_viva_runner.py
  CLI:           python3 scripts/payout_viva_runner.py --component backend
                 python3 scripts/payout_viva_runner.py --component ai
                 python3 scripts/payout_viva_runner.py --component web
                 python3 scripts/payout_viva_runner.py --component flutter
                 python3 scripts/payout_viva_runner.py --component all
                 python3 scripts/payout_viva_runner.py --scope full --component all
"""

import argparse
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import textwrap
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# ── Constants ────────────────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent
ARTIFACTS_DIR = PROJECT_ROOT / "test-artifacts" / "payout-viva"

BACKEND_TEST_PROJECT = (
    PROJECT_ROOT / "backend" / "tests" / "InsuranceClaims.UnitTests"
)
AI_SERVICE_DIR = PROJECT_ROOT / "ai-service"
FRONTEND_WEB_DIR = PROJECT_ROOT / "frontend-web"
MOBILE_DIR = PROJECT_ROOT / "mobile"

# Test filters per component — payout-specific only
BACKEND_PAYOUT_FILTER = "FullyQualifiedName~PayoutProcessing"
AI_PAYOUT_TEST_FILE = "tests/test_payout_validation.py"
AI_PAYOUT_FILTER = "tests/test_payout_validation.py or tests/test_validation_agent.py"
WEB_PAYOUT_PATTERN = "src/__tests__/payout/"
FLUTTER_PAYOUT_PATTERN = "test/payout/"

# ANSI colours
C_RESET = "\033[0m"
C_BOLD = "\033[1m"
C_GREEN = "\033[92m"
C_RED = "\033[91m"
C_YELLOW = "\033[93m"
C_CYAN = "\033[96m"
C_DIM = "\033[2m"
C_BLUE = "\033[94m"
C_MAGENTA = "\033[95m"


# ── Signal handling ──────────────────────────────────────────────────

_interrupted = False


def _handle_sigint(signum, frame):
    global _interrupted
    _interrupted = True
    print(f"\n{C_YELLOW}⚠  Ctrl+C received — aborting safely...{C_RESET}")


signal.signal(signal.SIGINT, _handle_sigint)


# ── Data classes ─────────────────────────────────────────────────────


class TestResult:
    """Result of a single component test run."""

    def __init__(self, component: str):
        self.component = component
        self.passed = 0
        self.failed = 0
        self.skipped = 0
        self.total = 0
        self.status = "NOT RUN"
        self.start_time: Optional[float] = None
        self.end_time: Optional[float] = None
        self.duration: float = 0.0
        self.command: str = ""
        self.output: str = ""
        self.test_names: List[str] = []
        self.failure_details: List[str] = []
        self.exit_code: int = -1
        self.is_payout_specific = True
        self.pre_existing_tests: int = 0
        self.new_tests: int = 0

    @property
    def duration_str(self) -> str:
        if self.duration < 1:
            return f"{self.duration * 1000:.0f}ms"
        return f"{self.duration:.2f}s"


# ── Prerequisite checks ─────────────────────────────────────────────


def check_prerequisite(name: str, command: str) -> bool:
    """Check if a CLI tool is available."""
    try:
        subprocess.run(
            command.split(),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=10,
        )
        return True
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return False


def check_all_prerequisites() -> Dict[str, bool]:
    """Check all component prerequisites."""
    return {
        "dotnet": check_prerequisite("dotnet", "dotnet --version"),
        "node": check_prerequisite("node", "node --version"),
        "npm": check_prerequisite("npm", "npm --version"),
        "python3": check_prerequisite("python3", "python3 --version"),
        "pytest": check_prerequisite("pytest", "python3 -m pytest --version"),
        "flutter": check_prerequisite("flutter", "flutter --version"),
    }


def validate_project_paths() -> Dict[str, bool]:
    """Validate that project directories exist."""
    return {
        "backend": BACKEND_TEST_PROJECT.exists(),
        "ai-service": AI_SERVICE_DIR.exists(),
        "frontend-web": FRONTEND_WEB_DIR.exists(),
        "mobile": MOBILE_DIR.exists(),
    }


def strip_ansi(text: str) -> str:
    """Remove ANSI escape sequences and non-printable control codes from output."""
    if not text:
        return ""
    # Strip terminal title and OSC sequences first (before \x1b] is broken by bracket range)
    cleaned = re.sub(r"\x1b\][0-9];[^\x07\x1b]*[\x07\x1b\\]+", "", text)
    # Standard ANSI escape codes
    ansi_escape = re.compile(r"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")
    cleaned = ansi_escape.sub("", cleaned)
    return cleaned


# ── Output parsing ───────────────────────────────────────────────────


def parse_dotnet_output(output: str, result: TestResult) -> None:
    """Parse dotnet test TRX/console output for test counts."""
    clean = strip_ansi(output)

    # Match: Passed! - Failed: X, Passed: Y, Skipped: Z, Total: W
    # or: Failed! - Failed: X, Passed: Y, Skipped: Z, Total: W
    summary_match = re.search(
        r"(?:Passed!|Failed!)\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)",
        clean,
    )
    if summary_match:
        result.failed = int(summary_match.group(1))
        result.passed = int(summary_match.group(2))
        result.skipped = int(summary_match.group(3))
        result.total = int(summary_match.group(4))
    else:
        # Fallback: search for individual metrics
        p_m = re.search(r"\bPassed:\s*(\d+)", clean)
        f_m = re.search(r"\bFailed:\s*(\d+)", clean)
        s_m = re.search(r"\bSkipped:\s*(\d+)", clean)
        t_m = re.search(r"\bTotal:\s*(\d+)", clean)
        if p_m or f_m:
            result.passed = int(p_m.group(1)) if p_m else 0
            result.failed = int(f_m.group(1)) if f_m else 0
            result.skipped = int(s_m.group(1)) if s_m else 0
            result.total = int(t_m.group(1)) if t_m else (result.passed + result.failed + result.skipped)
        else:
            passed = len(re.findall(r"\bPassed\s+[\w.]+", clean))
            failed = len(re.findall(r"\bFailed\s+[\w.]+", clean))
            result.passed = passed
            result.failed = failed
            result.total = passed + failed

    # Extract test names
    test_names = re.findall(r"(?:Passed|Failed)\s+([\w.]+)", clean)
    result.test_names = test_names

    # Extract failure details
    failures = re.findall(r"Failed\s+([\w.]+).*?(?=(?:Passed|Failed|\Z))", clean, re.DOTALL)
    result.failure_details = failures


def parse_pytest_output(output: str, result: TestResult) -> None:
    """Parse pytest output for test counts."""
    clean = strip_ansi(output)

    # Match: X passed, Y failed, Z skipped
    # or: X passed in Ys
    passed_m = re.search(r"(\d+)\s+passed", clean)
    failed_m = re.search(r"(\d+)\s+failed", clean)
    skipped_m = re.search(r"(\d+)\s+skipped", clean)
    error_m = re.search(r"(\d+)\s+error", clean)

    result.passed = int(passed_m.group(1)) if passed_m else 0
    result.failed = int(failed_m.group(1)) if failed_m else 0
    result.skipped = int(skipped_m.group(1)) if skipped_m else 0
    if error_m:
        result.failed += int(error_m.group(1))
    result.total = result.passed + result.failed + result.skipped

    # Extract test names (e.g. "tests/test_payout_validation.py::test_name PASSED")
    test_names = re.findall(r"\b(test_\w+)\s+(?:PASSED|FAILED|SKIPPED)", clean)
    result.test_names = test_names

    # Extract failure details
    failures = re.findall(r"FAILED\s+([\w/:.-]+)", clean)
    result.failure_details = failures


def parse_vitest_output(output: str, result: TestResult) -> None:
    """Parse Vitest output for test counts, handling ANSI and formatting variations."""
    clean = strip_ansi(output)

    # 1. Match the summary line starting with Tests (ignoring Test Files)
    # e.g.: "      Tests  76 passed (76)"
    #       "      Tests  70 passed | 2 failed | 4 skipped (76)"
    tests_summary_match = re.search(
        r"^[ \t]*Tests[ \t]+([^\n\r]+)$", clean, re.MULTILINE
    )

    if tests_summary_match:
        details_line = tests_summary_match.group(1).strip()
        passed_m = re.search(r"(\d+)\s+passed", details_line)
        failed_m = re.search(r"(\d+)\s+failed", details_line)
        skipped_m = re.search(r"(\d+)\s+(?:skipped|todo)", details_line)
        total_paren_m = re.search(r"\((\d+)\)", details_line)

        result.passed = int(passed_m.group(1)) if passed_m else 0
        result.failed = int(failed_m.group(1)) if failed_m else 0
        result.skipped = int(skipped_m.group(1)) if skipped_m else 0

        if total_paren_m:
            result.total = int(total_paren_m.group(1))
        else:
            result.total = result.passed + result.failed + result.skipped

    # Fallback 1: match anywhere in text if line-based regex missed
    if result.total == 0 and result.passed == 0 and result.failed == 0:
        fallback_m = re.search(
            r"Tests\s+.*?(?:(\d+)\s+passed)?.*?(?:(\d+)\s+failed)?.*?(?:(\d+)\s+skipped)?.*?(?:\((\d+)\))?",
            clean,
        )
        if fallback_m and any(fallback_m.groups()):
            p_val = fallback_m.group(1)
            f_val = fallback_m.group(2)
            s_val = fallback_m.group(3)
            tot_val = fallback_m.group(4)
            result.passed = int(p_val) if p_val else 0
            result.failed = int(f_val) if f_val else 0
            result.skipped = int(s_val) if s_val else 0
            result.total = int(tot_val) if tot_val else (result.passed + result.failed + result.skipped)

    # Extract test names and failures from individual test rows
    test_lines = re.findall(
        r"^[ \t]*([✓✔✕×✗✖])\s+(.+?)(?:\s+\d+m?s)?\s*$", clean, re.MULTILINE
    )
    passed_from_lines = 0
    failed_from_lines = 0
    test_names = []
    failure_details = []

    for mark, name in test_lines:
        trimmed = name.strip()
        if not trimmed:
            continue
        test_names.append(trimmed)
        if mark in ("✓", "✔"):
            passed_from_lines += 1
        else:
            failed_from_lines += 1
            failure_details.append(trimmed)

    result.test_names = test_names
    result.failure_details = failure_details

    # Fallback 2: if summary line was completely absent, derive from row counts
    if result.total == 0 and (passed_from_lines > 0 or failed_from_lines > 0):
        result.passed = passed_from_lines
        result.failed = failed_from_lines
        result.total = passed_from_lines + failed_from_lines
    elif result.total > 0 and result.passed == 0 and result.failed == 0 and passed_from_lines > 0:
        result.passed = passed_from_lines
        if result.failed == 0:
            result.failed = failed_from_lines


def parse_flutter_output(output: str, result: TestResult) -> None:
    """Parse flutter test output for test counts."""
    clean = strip_ansi(output)

    # Expanded reporter format: "00:00 +6: All tests passed!" or "00:01 +5 -1: Some tests failed."
    status_lines = re.findall(
        r"\d+:\d+\s+\+(\d+)(?:\s+~(\d+))?(?:\s+-(\d+))?\s*:", clean
    )
    if status_lines:
        last = status_lines[-1]
        result.passed = int(last[0] or 0)
        result.skipped = int(last[1]) if last[1] else 0
        result.failed = int(last[2]) if last[2] else 0
        result.total = result.passed + result.failed + result.skipped
    else:
        # Fallback: check for compact format ✓/✗
        result.passed = clean.count("✓")
        result.failed = clean.count("✗")
        result.total = result.passed + result.failed

    # Extract test names from expanded format: "00:00 +N: TestGroup TestName"
    test_names = re.findall(
        r"\d+:\d+\s+\+\d+(?:\s+[~-]\d+)*:\s+(.+?)(?:\s*$)", clean, re.MULTILINE
    )
    # Filter out summary/loading lines
    result.test_names = [
        t.strip() for t in test_names
        if not t.startswith("loading ")
        and "All tests passed" not in t
        and "Some tests failed" not in t
    ]

    failures = re.findall(r"✗\s+(.+)", clean)
    result.failure_details = failures


# ── Test execution ───────────────────────────────────────────────────


def run_command(
    cmd: List[str],
    cwd: Path,
    result: TestResult,
    env: Optional[dict] = None,
) -> None:
    """Execute a test command safely and stream output."""
    global _interrupted

    result.command = " ".join(cmd)
    result.start_time = time.time()

    print(f"\n{C_CYAN}▶ Command:{C_RESET} {result.command}")
    print(f"{C_DIM}  Working directory: {cwd}{C_RESET}")
    print(f"{C_DIM}{'─' * 72}{C_RESET}")

    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)

    try:
        proc = subprocess.Popen(
            cmd,
            cwd=str(cwd),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            env=merged_env,
        )

        output_lines = []
        for line in iter(proc.stdout.readline, ""):
            if _interrupted:
                proc.terminate()
                break
            print(f"  {line}", end="")
            output_lines.append(line)

        proc.wait(timeout=300)
        result.exit_code = proc.returncode
        result.output = "".join(output_lines)

    except subprocess.TimeoutExpired:
        proc.kill()
        result.exit_code = -1
        result.output = "".join(output_lines) if output_lines else ""
        result.status = "ERROR"
    except FileNotFoundError:
        result.exit_code = -1
        result.status = "ERROR"
        result.output = f"Command not found: {cmd[0]}"
        print(f"  {C_RED}✕ Command not found: {cmd[0]}{C_RESET}")
    except Exception as e:
        result.exit_code = -1
        result.status = "ERROR"
        result.output = str(e)
        print(f"  {C_RED}✕ Error: {e}{C_RESET}")
    finally:
        result.end_time = time.time()
        result.duration = result.end_time - result.start_time

    print(f"{C_DIM}{'─' * 72}{C_RESET}")


def determine_status(result: TestResult) -> None:
    """Determine the test status from parsed results."""
    if result.status == "ERROR":
        return

    # Non-zero process exit code must never report PASS or NO TESTS
    if result.exit_code != 0:
        result.status = "FAIL" if result.failed > 0 else "ERROR"
        return

    if result.total == 0:
        result.status = "NO TESTS"
    elif result.failed > 0:
        result.status = "FAIL"
    elif result.skipped > 0 and result.passed == 0:
        result.status = "SKIP"
    elif result.passed == 0 and result.total > 0:
        # Prevent PASS when 0 tests were counted as passed despite total > 0
        result.status = "ERROR"
    else:
        result.status = "PASS"


# ── Component test runners ───────────────────────────────────────────


def run_backend_payout_tests(scope: str = "payout") -> TestResult:
    """Run backend payout tests with dotnet test."""
    result = TestResult("Backend Payout")
    prereqs = check_all_prerequisites()

    if not prereqs["dotnet"]:
        result.status = "ERROR"
        result.output = "dotnet SDK not found. Install .NET SDK to run backend tests."
        print(f"  {C_RED}✕ dotnet not found{C_RESET}")
        return result

    if not BACKEND_TEST_PROJECT.exists():
        result.status = "ERROR"
        result.output = f"Test project not found at {BACKEND_TEST_PROJECT}"
        return result

    cmd = [
        "dotnet", "test",
        str(BACKEND_TEST_PROJECT),
        "--no-restore",
        "--verbosity", "normal",
        "--logger", "console;verbosity=detailed",
    ]

    if scope == "payout":
        cmd.extend(["--filter", BACKEND_PAYOUT_FILTER])

    run_command(cmd, PROJECT_ROOT / "backend", result)
    parse_dotnet_output(result.output, result)
    determine_status(result)
    return result


def run_ai_payout_tests(scope: str = "payout") -> TestResult:
    """Run AI service payout tests with pytest."""
    result = TestResult("AI Payout")
    prereqs = check_all_prerequisites()

    if not prereqs["python3"]:
        result.status = "ERROR"
        result.output = "python3 not found."
        return result

    if not AI_SERVICE_DIR.exists():
        result.status = "ERROR"
        result.output = f"AI service not found at {AI_SERVICE_DIR}"
        return result

    # Use the project's virtual environment if it exists
    venv_python = AI_SERVICE_DIR / ".venv" / "bin" / "python"
    python_cmd = str(venv_python) if venv_python.exists() else "python3"

    cmd = [python_cmd, "-m", "pytest", "-v", "--tb=short", "-x"]

    if scope == "payout":
        cmd.extend([
            "tests/test_payout_validation.py",
            "tests/test_validation_agent.py",
        ])
    else:
        cmd.append("tests/")

    # Add markers to exclude live AI tests
    cmd.extend(["-m", "not live_ai"])

    run_command(
        cmd,
        AI_SERVICE_DIR,
        result,
        env={
            "PYTHONDONTWRITEBYTECODE": "1",
            "PYTHONWARNINGS": "ignore:.*urllib3 v2 only supports OpenSSL.*:Warning",
        },
    )
    parse_pytest_output(result.output, result)
    determine_status(result)
    return result


def run_web_payout_tests(scope: str = "payout") -> TestResult:
    """Run React web payout tests with Vitest."""
    result = TestResult("React Payout")
    prereqs = check_all_prerequisites()

    if not prereqs["node"] or not prereqs["npm"]:
        result.status = "ERROR"
        result.output = "Node.js or npm not found."
        return result

    if not FRONTEND_WEB_DIR.exists():
        result.status = "ERROR"
        result.output = f"Frontend not found at {FRONTEND_WEB_DIR}"
        return result

    # Check node_modules
    if not (FRONTEND_WEB_DIR / "node_modules").exists():
        print(f"  {C_YELLOW}⚠ node_modules missing — running npm install...{C_RESET}")
        subprocess.run(
            ["npm", "install"],
            cwd=str(FRONTEND_WEB_DIR),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=120,
        )

    cmd = ["npx", "vitest", "run", "--reporter=verbose"]

    if scope == "payout":
        cmd.append(WEB_PAYOUT_PATTERN)

    run_command(cmd, FRONTEND_WEB_DIR, result)
    parse_vitest_output(result.output, result)
    determine_status(result)
    return result


def run_flutter_payout_tests(scope: str = "payout") -> TestResult:
    """Run Flutter mobile payout tests."""
    result = TestResult("Flutter Payout")
    prereqs = check_all_prerequisites()

    if not prereqs["flutter"]:
        result.status = "ERROR"
        result.output = "Flutter SDK not found."
        return result

    if not MOBILE_DIR.exists():
        result.status = "ERROR"
        result.output = f"Mobile directory not found at {MOBILE_DIR}"
        return result

    if scope == "payout":
        # Check if payout test directory exists
        payout_test_dir = MOBILE_DIR / "test" / "payout"
        if not payout_test_dir.exists() or not list(payout_test_dir.glob("*_test.dart")):
            result.status = "NO TESTS"
            result.output = "No payout-specific Flutter tests found."
            result.total = 0
            return result

        cmd = ["flutter", "test", FLUTTER_PAYOUT_PATTERN, "--reporter=expanded"]
    else:
        cmd = ["flutter", "test", "--reporter=expanded"]

    run_command(cmd, MOBILE_DIR, result)
    parse_flutter_output(result.output, result)
    determine_status(result)
    return result


# ── Artifacts & reporting ────────────────────────────────────────────


def ensure_artifacts_dir() -> Path:
    """Create artifacts directory."""
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)
    return ARTIFACTS_DIR


def sanitize_output(text: str) -> str:
    """Remove ANSI codes and sensitive data from output."""
    # Strip ANSI escape sequences
    ansi_escape = re.compile(r"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")
    text = ansi_escape.sub("", text)
    # Strip potential API keys / JWTs
    text = re.sub(r"(eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_.-]+)", "[JWT_REDACTED]", text)
    text = re.sub(r"(AIza[A-Za-z0-9_-]{35})", "[API_KEY_REDACTED]", text)
    text = re.sub(r"(sk-[A-Za-z0-9]{48})", "[API_KEY_REDACTED]", text)
    return text


def save_log(result: TestResult, run_id: str) -> Path:
    """Save test log to artifacts directory."""
    artifacts = ensure_artifacts_dir()
    safe_name = result.component.lower().replace(" ", "_")
    log_path = artifacts / f"{run_id}_{safe_name}.log"
    content = sanitize_output(result.output)
    log_path.write_text(content, encoding="utf-8")
    return log_path


def save_json_summary(results: List[TestResult], run_id: str) -> Path:
    """Save JSON summary of all test results."""
    artifacts = ensure_artifacts_dir()
    json_path = artifacts / f"{run_id}_summary.json"

    summary = {
        "run_id": run_id,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "components": [],
    }

    for r in results:
        summary["components"].append({
            "component": r.component,
            "passed": r.passed,
            "failed": r.failed,
            "skipped": r.skipped,
            "total": r.total,
            "status": r.status,
            "duration_seconds": round(r.duration, 2),
            "command": r.command,
            "test_names": r.test_names,
            "failure_details": r.failure_details,
            "is_payout_specific": r.is_payout_specific,
        })

    json_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    return json_path


def save_html_summary(results: List[TestResult], run_id: str) -> Path:
    """Generate an HTML summary report."""
    artifacts = ensure_artifacts_dir()
    html_path = artifacts / f"{run_id}_summary.html"

    rows = ""
    for r in results:
        status_color = {
            "PASS": "#22c55e",
            "FAIL": "#ef4444",
            "SKIP": "#f59e0b",
            "NO TESTS": "#6b7280",
            "ERROR": "#ef4444",
            "NOT RUN": "#9ca3af",
        }.get(r.status, "#9ca3af")

        rows += f"""
        <tr>
            <td>{r.component}</td>
            <td>{r.passed}</td>
            <td>{r.failed}</td>
            <td>{r.skipped}</td>
            <td style="color: {status_color}; font-weight: bold;">{r.status}</td>
            <td>{r.duration_str}</td>
        </tr>"""

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Payout Viva Test Report — {run_id}</title>
<style>
    body {{ font-family: 'Segoe UI', system-ui, sans-serif; margin: 40px; background: #f9fafb; }}
    h1 {{ color: #1e293b; }}
    table {{ border-collapse: collapse; width: 100%; margin-top: 20px; }}
    th, td {{ border: 1px solid #e2e8f0; padding: 10px 14px; text-align: left; }}
    th {{ background: #1e293b; color: white; }}
    tr:nth-child(even) {{ background: #f1f5f9; }}
    .meta {{ color: #64748b; font-size: 0.9em; }}
</style>
</head>
<body>
<h1>🏥 Payout Management — Viva Test Report</h1>
<p class="meta">Run ID: {run_id} | Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}</p>
<table>
<tr><th>Component</th><th>Passed</th><th>Failed</th><th>Skipped</th><th>Status</th><th>Duration</th></tr>
{rows}
</table>
</body>
</html>"""

    html_path.write_text(html, encoding="utf-8")
    return html_path


# ── Display functions ────────────────────────────────────────────────


def display_banner():
    """Display the viva testing banner."""
    print(f"""
{C_BOLD}{C_CYAN}{'=' * 56}
  INSURANCE CLAIMS MANAGEMENT SYSTEM
  PAYOUT MANAGEMENT — VIVA TESTING
{'=' * 56}{C_RESET}
""")


def display_menu():
    """Display interactive menu."""
    print(f"""
  {C_BOLD}1.{C_RESET}  Test Payout Backend / API
  {C_BOLD}2.{C_RESET}  Test Payout AI Service
  {C_BOLD}3.{C_RESET}  Test Payout React Web Frontend
  {C_BOLD}4.{C_RESET}  Test Payout Flutter Mobile
  {C_BOLD}5.{C_RESET}  Run All Payout Tests
  {C_BOLD}6.{C_RESET}  Run Full Component Regression Tests
  {C_BOLD}7.{C_RESET}  View Latest Test Results
  {C_BOLD}0.{C_RESET}  Exit
""")


def display_result_table(results: List[TestResult]):
    """Display a formatted results table."""
    print(f"\n{C_BOLD}{'=' * 80}")
    print(f"  PAYOUT MANAGEMENT — TEST RESULTS SUMMARY")
    print(f"{'=' * 80}{C_RESET}")
    print(
        f"  {'Component':<22} {'Passed':>8} {'Failed':>8} {'Skipped':>8} {'Status':>12} {'Duration':>10}"
    )
    print(f"  {'─' * 70}")

    for r in results:
        status_color = {
            "PASS": C_GREEN,
            "FAIL": C_RED,
            "SKIP": C_YELLOW,
            "NO TESTS": C_DIM,
            "ERROR": C_RED,
            "NOT RUN": C_DIM,
        }.get(r.status, C_RESET)

        print(
            f"  {r.component:<22} {r.passed:>8} {r.failed:>8} {r.skipped:>8} "
            f"{status_color}{r.status:>12}{C_RESET} {r.duration_str:>10}"
        )

    print(f"  {'─' * 70}")

    total_passed = sum(r.passed for r in results)
    total_failed = sum(r.failed for r in results)
    total_skipped = sum(r.skipped for r in results)
    total_duration = sum(r.duration for r in results)

    overall = C_GREEN + "ALL PASSED" if total_failed == 0 else C_RED + "FAILURES"
    print(
        f"  {'TOTAL':<22} {total_passed:>8} {total_failed:>8} {total_skipped:>8} "
        f"{overall:>12}{C_RESET} {total_duration:.2f}s"
    )
    print(f"{C_BOLD}{'=' * 80}{C_RESET}\n")


def display_prerequisites():
    """Display prerequisite check results."""
    print(f"\n{C_BOLD}Prerequisite Check:{C_RESET}")
    prereqs = check_all_prerequisites()
    paths = validate_project_paths()

    for name, available in prereqs.items():
        icon = f"{C_GREEN}✓{C_RESET}" if available else f"{C_RED}✕{C_RESET}"
        print(f"  {icon} {name}")

    print()
    for name, exists in paths.items():
        icon = f"{C_GREEN}✓{C_RESET}" if exists else f"{C_RED}✕{C_RESET}"
        print(f"  {icon} {name}/ directory")
    print()


def display_latest_results():
    """Display the latest test results from artifacts."""
    if not ARTIFACTS_DIR.exists():
        print(f"  {C_YELLOW}No test artifacts found.{C_RESET}")
        return

    json_files = sorted(ARTIFACTS_DIR.glob("*_summary.json"), reverse=True)
    if not json_files:
        print(f"  {C_YELLOW}No test summaries found.{C_RESET}")
        return

    latest = json_files[0]
    print(f"\n{C_BOLD}Latest Test Results:{C_RESET} {latest.name}")

    data = json.loads(latest.read_text())
    print(f"  Run ID:    {data['run_id']}")
    print(f"  Timestamp: {data['timestamp']}")
    print()

    for comp in data["components"]:
        status_color = {
            "PASS": C_GREEN,
            "FAIL": C_RED,
            "SKIP": C_YELLOW,
            "NO TESTS": C_DIM,
            "ERROR": C_RED,
        }.get(comp["status"], C_RESET)

        print(
            f"  {comp['component']:<22} "
            f"P:{comp['passed']} F:{comp['failed']} S:{comp['skipped']}  "
            f"{status_color}{comp['status']}{C_RESET}"
        )

    # Show artifact paths
    print(f"\n  {C_DIM}Artifacts directory: {ARTIFACTS_DIR}{C_RESET}")
    html_files = sorted(ARTIFACTS_DIR.glob("*_summary.html"), reverse=True)
    if html_files:
        print(f"  {C_DIM}HTML report:        {html_files[0]}{C_RESET}")


# ── Run orchestration ────────────────────────────────────────────────


def run_payout_tests(components: List[str], scope: str = "payout") -> List[TestResult]:
    """Run tests for specified components."""
    global _interrupted
    results = []
    run_id = datetime.now().strftime("%Y%m%d_%H%M%S")

    component_map = {
        "backend": ("Backend Payout", run_backend_payout_tests),
        "ai": ("AI Payout", run_ai_payout_tests),
        "web": ("React Payout", run_web_payout_tests),
        "flutter": ("Flutter Payout", run_flutter_payout_tests),
    }

    for comp_key in components:
        if _interrupted:
            break

        if comp_key not in component_map:
            print(f"  {C_RED}Unknown component: {comp_key}{C_RESET}")
            continue

        name, runner = component_map[comp_key]
        print(f"\n{C_BOLD}{C_BLUE}━━━ {name} Tests ━━━{C_RESET}")
        result = runner(scope)
        results.append(result)

        # Save log
        save_log(result, run_id)

        # Display individual result
        status_icon = {
            "PASS": f"{C_GREEN}✓ PASS{C_RESET}",
            "FAIL": f"{C_RED}✕ FAIL{C_RESET}",
            "SKIP": f"{C_YELLOW}⊘ SKIP{C_RESET}",
            "NO TESTS": f"{C_DIM}⊘ NO TESTS{C_RESET}",
            "ERROR": f"{C_RED}✕ ERROR{C_RESET}",
        }.get(result.status, result.status)

        print(f"\n  Result: {status_icon}")
        print(f"  Passed: {result.passed}  Failed: {result.failed}  Skipped: {result.skipped}")
        print(f"  Duration: {result.duration_str}")

        if result.failure_details:
            print(f"\n  {C_RED}Failure details:{C_RESET}")
            for detail in result.failure_details[:10]:
                print(f"    • {detail}")

    # Save summary
    if results:
        json_path = save_json_summary(results, run_id)
        html_path = save_html_summary(results, run_id)

        display_result_table(results)

        print(f"  {C_DIM}Test logs:    {ARTIFACTS_DIR}/{C_RESET}")
        print(f"  {C_DIM}JSON summary: {json_path}{C_RESET}")
        print(f"  {C_DIM}HTML report:  {html_path}{C_RESET}")

    return results


# ── Interactive mode ─────────────────────────────────────────────────


def interactive_mode():
    """Run the interactive menu loop."""
    global _interrupted

    display_banner()
    display_prerequisites()

    while not _interrupted:
        display_menu()
        try:
            choice = input(f"  {C_BOLD}Select an option:{C_RESET} ").strip()
        except (EOFError, KeyboardInterrupt):
            print(f"\n{C_YELLOW}Exiting...{C_RESET}")
            break

        if choice == "0":
            print(f"\n{C_GREEN}Goodbye!{C_RESET}")
            break
        elif choice == "1":
            run_payout_tests(["backend"])
        elif choice == "2":
            run_payout_tests(["ai"])
        elif choice == "3":
            run_payout_tests(["web"])
        elif choice == "4":
            run_payout_tests(["flutter"])
        elif choice == "5":
            run_payout_tests(["backend", "ai", "web", "flutter"])
        elif choice == "6":
            run_payout_tests(["backend", "ai", "web", "flutter"], scope="full")
        elif choice == "7":
            display_latest_results()
        else:
            print(f"  {C_YELLOW}Invalid option. Please try again.{C_RESET}")


# ── CLI mode ─────────────────────────────────────────────────────────


def cli_mode(args: argparse.Namespace):
    """Run in CLI mode."""
    component = args.component
    scope = getattr(args, "scope", "payout") or "payout"

    if component == "all":
        components = ["backend", "ai", "web", "flutter"]
    else:
        components = [component]

    display_banner()
    results = run_payout_tests(components, scope)

    # Return nonzero exit code if any tests failed
    if any(r.status == "FAIL" or r.status == "ERROR" for r in results):
        sys.exit(1)


# ── Entry point ──────────────────────────────────────────────────────


def main():
    parser = argparse.ArgumentParser(
        description="Payout Management Viva Testing Runner",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=textwrap.dedent("""
            Examples:
              python3 scripts/payout_viva_runner.py                           # Interactive menu
              python3 scripts/payout_viva_runner.py --component backend       # Backend payout tests
              python3 scripts/payout_viva_runner.py --component ai            # AI service payout tests
              python3 scripts/payout_viva_runner.py --component web           # Web frontend payout tests
              python3 scripts/payout_viva_runner.py --component flutter       # Flutter mobile payout tests
              python3 scripts/payout_viva_runner.py --component all           # All payout tests
              python3 scripts/payout_viva_runner.py --scope full --component all  # Full regression
        """),
    )
    parser.add_argument(
        "--component",
        choices=["backend", "ai", "web", "flutter", "all"],
        help="Component to test",
    )
    parser.add_argument(
        "--scope",
        choices=["payout", "full"],
        default="payout",
        help="Test scope: payout (default) or full regression",
    )

    args = parser.parse_args()

    if args.component:
        cli_mode(args)
    else:
        interactive_mode()


if __name__ == "__main__":
    main()
