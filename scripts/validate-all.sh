#!/usr/bin/env bash
# S1.5-001 — Master infrastructure validation
# Runs all validation scripts and produces a final summary.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOTAL_PASS=0
TOTAL_FAIL=0
SUITE_RESULTS=()

# ─── Color output ───────────────────────────────────────────
if [ -t 1 ]; then
  GREEN='\033[0;32m'; RED='\033[0;31m'; BOLD='\033[1m'; NC='\033[0m'
else
  GREEN=''; RED=''; BOLD=''; NC=''
fi

info() { echo -e "${BOLD}$1${NC}"; }

run_suite() {
  local name="$1" script="$2"
  echo ""
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  info "  ${name}"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""

  if bash "${SCRIPT_DIR}/${script}"; then
    SUITE_RESULTS+=("${GREEN}PASS${NC}  ${name}")
  else
    SUITE_RESULTS+=("${RED}FAIL${NC}  ${name}")
    ((TOTAL_FAIL++))
  fi
}

echo ""
info "╔══════════════════════════════════════════════════════════╗"
info "║        SNAPP Infrastructure Validation Suite            ║"
info "╚══════════════════════════════════════════════════════════╝"

# ─── Run all suites ────────────────────────────────────────
run_suite "1. Docker Compose Health"    "test-infra.sh"
run_suite "2. DynamoDB Tables"          "test-dynamo-tables.sh"
run_suite "3. Kong Routes & Plugins"    "test-kong-routes.sh"
run_suite "4. MinIO Buckets"            "test-minio-buckets.sh"

# ─── Final Summary ─────────────────────────────────────────
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Final Results"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

for result in "${SUITE_RESULTS[@]}"; do
  echo -e "  ${result}"
done

echo ""

if [ $TOTAL_FAIL -gt 0 ]; then
  echo -e "${RED}${BOLD}INFRASTRUCTURE VALIDATION FAILED${NC} — ${TOTAL_FAIL} suite(s) failed"
  exit 1
fi

echo -e "${GREEN}${BOLD}ALL INFRASTRUCTURE VALIDATED SUCCESSFULLY${NC}"
exit 0
