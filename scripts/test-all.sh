#!/usr/bin/env bash
# S1.5-004 — Automated test runner for SNAPP
# Runs infrastructure validation, unit tests, integration tests, and component tests.
# Aggregates results and generates a markdown test report.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TEST_DIR="${REPO_ROOT}/test"
REPORT_DIR="${REPO_ROOT}/.claude/reports"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
REPORT_FILE="${REPORT_DIR}/test-report_${TIMESTAMP}.md"

# ─── Color output ───────────────────────────────────────────
if [ -t 1 ]; then
  GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[0;33m'
  BOLD='\033[1m'; NC='\033[0m'
else
  GREEN=''; RED=''; YELLOW=''; BOLD=''; NC=''
fi

info()  { echo -e "${BOLD}$1${NC}"; }
pass()  { echo -e "${GREEN}$1${NC}"; }
fail()  { echo -e "${RED}$1${NC}"; }
warn()  { echo -e "${YELLOW}$1${NC}"; }

# ─── Result tracking ────────────────────────────────────────
TOTAL_PASSED=0
TOTAL_FAILED=0
TOTAL_SKIPPED=0
TOTAL_TESTS=0
SUITE_RESULTS=()
OVERALL_EXIT=0

# Parse dotnet test TRX-style console output for counts
parse_test_output() {
  local output="$1"
  local passed=0 failed=0 skipped=0

  # dotnet test outputs: "Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3"
  # or individual lines like "  Passed  TestName"
  local summary_line
  summary_line=$(echo "$output" | grep -E '(Passed|Failed)!\s+' | tail -1)

  if [ -n "$summary_line" ]; then
    passed=$(echo "$summary_line" | grep -oE 'Passed:\s*[0-9]+' | grep -oE '[0-9]+' || echo 0)
    failed=$(echo "$summary_line" | grep -oE 'Failed:\s*[0-9]+' | grep -oE '[0-9]+' || echo 0)
    skipped=$(echo "$summary_line" | grep -oE 'Skipped:\s*[0-9]+' | grep -oE '[0-9]+' || echo 0)
  fi

  # Fallback: count from "Total tests:" line
  if [ "$passed" = "0" ] && [ "$failed" = "0" ]; then
    local total_line
    total_line=$(echo "$output" | grep -E 'Total tests:' | tail -1)
    if [ -n "$total_line" ]; then
      local total
      total=$(echo "$total_line" | grep -oE 'Total tests:\s*[0-9]+' | grep -oE '[0-9]+' || echo 0)
      passed_line=$(echo "$output" | grep -E '^\s*Passed\s+' | wc -l | tr -d ' ')
      failed_line=$(echo "$output" | grep -E '^\s*Failed\s+' | wc -l | tr -d ' ')
      passed=${passed_line:-0}
      failed=${failed_line:-0}
      skipped=$(( ${total:-0} - ${passed:-0} - ${failed:-0} ))
      [ "$skipped" -lt 0 ] && skipped=0
    fi
  fi

  echo "${passed:-0} ${failed:-0} ${skipped:-0}"
}

run_dotnet_tests() {
  local name="$1"
  local project_path="$2"
  local category="$3"

  if [ ! -d "$project_path" ]; then
    warn "  SKIP  ${name} (directory not found)"
    SUITE_RESULTS+=("SKIP|${category}|${name}|0|0|0|Directory not found")
    return
  fi

  if ! ls "${project_path}"/*.csproj > /dev/null 2>&1; then
    warn "  SKIP  ${name} (no .csproj found)"
    SUITE_RESULTS+=("SKIP|${category}|${name}|0|0|0|No .csproj found")
    return
  fi

  echo ""
  info "  Running: ${name}"

  local output
  local exit_code
  output=$(dotnet test "$project_path" --logger 'console;verbosity=normal' --no-restore 2>&1) || true
  exit_code=${PIPESTATUS[0]:-$?}

  # Try to build first if no-restore failed
  if echo "$output" | grep -q "Could not locate the assembly"; then
    output=$(dotnet test "$project_path" --logger 'console;verbosity=normal' 2>&1) || true
    exit_code=${PIPESTATUS[0]:-$?}
  fi

  local counts
  counts=$(parse_test_output "$output")
  local p f s
  p=$(echo "$counts" | awk '{print $1}')
  f=$(echo "$counts" | awk '{print $2}')
  s=$(echo "$counts" | awk '{print $3}')

  TOTAL_PASSED=$((TOTAL_PASSED + p))
  TOTAL_FAILED=$((TOTAL_FAILED + f))
  TOTAL_SKIPPED=$((TOTAL_SKIPPED + s))
  TOTAL_TESTS=$((TOTAL_TESTS + p + f + s))

  if [ "$f" -gt 0 ] || [ "$exit_code" -ne 0 ]; then
    fail "  FAIL  ${name} — Passed: ${p}, Failed: ${f}, Skipped: ${s}"
    SUITE_RESULTS+=("FAIL|${category}|${name}|${p}|${f}|${s}|")
    OVERALL_EXIT=1
  else
    pass "  PASS  ${name} — Passed: ${p}, Failed: ${f}, Skipped: ${s}"
    SUITE_RESULTS+=("PASS|${category}|${name}|${p}|${f}|${s}|")
  fi
}

# ═══════════════════════════════════════════════════════════════
#  Phase 1: Infrastructure Validation
# ═══════════════════════════════════════════════════════════════
echo ""
info "╔══════════════════════════════════════════════════════════╗"
info "║              SNAPP Test Runner                          ║"
info "╚══════════════════════════════════════════════════════════╝"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Phase 1: Infrastructure Validation"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ -x "${SCRIPT_DIR}/validate-all.sh" ]; then
  if bash "${SCRIPT_DIR}/validate-all.sh"; then
    SUITE_RESULTS+=("PASS|Infrastructure|Infrastructure Validation|0|0|0|")
  else
    SUITE_RESULTS+=("FAIL|Infrastructure|Infrastructure Validation|0|0|0|")
    OVERALL_EXIT=1
  fi
else
  warn "  SKIP  validate-all.sh not found"
  SUITE_RESULTS+=("SKIP|Infrastructure|Infrastructure Validation|0|0|0|Script not found")
fi

# ═══════════════════════════════════════════════════════════════
#  Phase 2: Restore solution (once, for all test projects)
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Restoring solution packages..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

SLN_FILE="${REPO_ROOT}/snapp.sln"
if [ -f "$SLN_FILE" ]; then
  dotnet restore "$SLN_FILE" 2>&1 | tail -3
else
  warn "  No solution file found — individual project restore will be used"
fi

# ═══════════════════════════════════════════════════════════════
#  Phase 3: Unit Tests (Shared library, TestHelpers)
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Phase 2: Unit Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

run_dotnet_tests "Snapp.Shared.Tests"        "${TEST_DIR}/Snapp.Shared.Tests"        "Unit"
run_dotnet_tests "Snapp.TestHelpers.Tests"    "${TEST_DIR}/Snapp.TestHelpers.Tests"    "Unit"
run_dotnet_tests "Snapp.Sdk.Tests"            "${TEST_DIR}/Snapp.Sdk.Tests"            "Unit"

# ═══════════════════════════════════════════════════════════════
#  Phase 4: Service Integration Tests
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Phase 3: Service Integration Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

run_dotnet_tests "Snapp.Service.Auth.Tests"         "${TEST_DIR}/Snapp.Service.Auth.Tests"         "Integration"
run_dotnet_tests "Snapp.Service.User.Tests"          "${TEST_DIR}/Snapp.Service.User.Tests"          "Integration"
run_dotnet_tests "Snapp.Service.Network.Tests"       "${TEST_DIR}/Snapp.Service.Network.Tests"       "Integration"
run_dotnet_tests "Snapp.Service.Content.Tests"       "${TEST_DIR}/Snapp.Service.Content.Tests"       "Integration"
run_dotnet_tests "Snapp.Service.Intelligence.Tests"  "${TEST_DIR}/Snapp.Service.Intelligence.Tests"  "Integration"
run_dotnet_tests "Snapp.Service.Transaction.Tests"   "${TEST_DIR}/Snapp.Service.Transaction.Tests"   "Integration"
run_dotnet_tests "Snapp.Service.Notification.Tests"  "${TEST_DIR}/Snapp.Service.Notification.Tests"  "Integration"
run_dotnet_tests "Snapp.Service.DigestJob.Tests"     "${TEST_DIR}/Snapp.Service.DigestJob.Tests"     "Integration"
run_dotnet_tests "Snapp.Service.LinkedIn.Tests"      "${TEST_DIR}/Snapp.Service.LinkedIn.Tests"      "Integration"

# ═══════════════════════════════════════════════════════════════
#  Phase 5: Blazor Component Tests
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Phase 4: Blazor Component Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

run_dotnet_tests "Snapp.Client.Tests" "${TEST_DIR}/Snapp.Client.Tests" "Component"

# ═══════════════════════════════════════════════════════════════
#  Phase 6: E2E Tests (skipped unless --e2e flag)
# ═══════════════════════════════════════════════════════════════
if [[ "${1:-}" == "--e2e" ]]; then
  echo ""
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  info "  Phase 5: E2E Tests"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  run_dotnet_tests "Snapp.E2E.Tests" "${TEST_DIR}/Snapp.E2E.Tests" "E2E"
else
  SUITE_RESULTS+=("SKIP|E2E|Snapp.E2E.Tests|0|0|0|Pass --e2e flag to run")
fi

# ═══════════════════════════════════════════════════════════════
#  Generate Report
# ═══════════════════════════════════════════════════════════════
mkdir -p "${REPORT_DIR}"

{
  echo "# SNAPP Test Report"
  echo ""
  echo "**Generated:** $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
  echo "**Branch:** $(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo 'unknown')"
  echo "**Commit:** $(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
  echo ""
  echo "## Summary"
  echo ""
  echo "| Metric | Count |"
  echo "|--------|-------|"
  echo "| Total Tests | ${TOTAL_TESTS} |"
  echo "| Passed | ${TOTAL_PASSED} |"
  echo "| Failed | ${TOTAL_FAILED} |"
  echo "| Skipped | ${TOTAL_SKIPPED} |"
  echo ""
  echo "## Results by Suite"
  echo ""
  echo "| Status | Category | Suite | Passed | Failed | Skipped | Notes |"
  echo "|--------|----------|-------|--------|--------|---------|-------|"

  for result in "${SUITE_RESULTS[@]}"; do
    IFS='|' read -r status category name p f s notes <<< "$result"
    case "$status" in
      PASS) icon="PASS" ;;
      FAIL) icon="FAIL" ;;
      SKIP) icon="SKIP" ;;
    esac
    echo "| ${icon} | ${category} | ${name} | ${p} | ${f} | ${s} | ${notes} |"
  done

  echo ""
  if [ "$OVERALL_EXIT" -eq 0 ]; then
    echo "## Result: ALL TESTS PASSED"
  else
    echo "## Result: TESTS FAILED"
  fi
} > "${REPORT_FILE}"

# ═══════════════════════════════════════════════════════════════
#  Final Summary
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Test Summary"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "  Total:   ${TOTAL_TESTS}"
pass "  Passed:  ${TOTAL_PASSED}"
if [ "$TOTAL_FAILED" -gt 0 ]; then
  fail "  Failed:  ${TOTAL_FAILED}"
else
  echo "  Failed:  ${TOTAL_FAILED}"
fi
if [ "$TOTAL_SKIPPED" -gt 0 ]; then
  warn "  Skipped: ${TOTAL_SKIPPED}"
else
  echo "  Skipped: ${TOTAL_SKIPPED}"
fi
echo ""
echo "  Report:  ${REPORT_FILE}"
echo ""

if [ "$OVERALL_EXIT" -ne 0 ]; then
  fail "TESTS FAILED"
else
  pass "ALL TESTS PASSED"
fi

exit $OVERALL_EXIT
