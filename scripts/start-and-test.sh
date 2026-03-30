#!/usr/bin/env bash
# S1.5-004 — One-command start-and-test for SNAPP
# Starts Docker Compose, initializes infrastructure, runs all tests,
# and optionally tears down when finished.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
INFRA_DIR="${REPO_ROOT}/src/Snapp.Infrastructure"
DOCKER_DIR="${INFRA_DIR}/Docker"
COMPOSE_FILE="${DOCKER_DIR}/docker-compose.yml"
SETUP_SCRIPT="${INFRA_DIR}/Scripts/setup-local.sh"

TEARDOWN=false
E2E_FLAG=""

# ─── Parse flags ─────────────────────────────────────────────
for arg in "$@"; do
  case "$arg" in
    --teardown) TEARDOWN=true ;;
    --e2e) E2E_FLAG="--e2e" ;;
    --help|-h)
      echo "Usage: $(basename "$0") [--teardown] [--e2e]"
      echo ""
      echo "  --teardown  Bring down Docker Compose after tests complete"
      echo "  --e2e       Include E2E (Playwright) tests"
      echo ""
      exit 0
      ;;
  esac
done

# ─── Color output ───────────────────────────────────────────
if [ -t 1 ]; then
  GREEN='\033[0;32m'; RED='\033[0;31m'; BOLD='\033[1m'; NC='\033[0m'
else
  GREEN=''; RED=''; BOLD=''; NC=''
fi

info() { echo -e "${BOLD}$1${NC}"; }

# ═══════════════════════════════════════════════════════════════
echo ""
info "╔══════════════════════════════════════════════════════════╗"
info "║         SNAPP Start & Test Orchestrator                 ║"
info "╚══════════════════════════════════════════════════════════╝"
echo ""

# ═══════════════════════════════════════════════════════════════
#  Step 1: Start Docker Compose if not running
# ═══════════════════════════════════════════════════════════════
info "[1/4] Checking Docker Compose services..."

RUNNING_SERVICES=$(docker compose -f "${COMPOSE_FILE}" ps --status running -q 2>/dev/null | wc -l | tr -d ' ')

if [ "${RUNNING_SERVICES}" -ge 4 ]; then
  echo "  Docker services already running (${RUNNING_SERVICES} containers)"
else
  echo "  Starting Docker Compose environment..."
  bash "${SETUP_SCRIPT}"
fi
echo ""

# ═══════════════════════════════════════════════════════════════
#  Step 2: Wait for all services to be healthy
# ═══════════════════════════════════════════════════════════════
info "[2/4] Waiting for services to be healthy..."

MAX_WAIT=120
INTERVAL=3
ELAPSED=0

check_health() {
  # DynamoDB Local returns 400 on bare GET / (expected); check it responds at all
  curl -so /dev/null -w '%{http_code}' http://localhost:8042 2>/dev/null | grep -q '400' || return 1
  # Kong Admin
  curl -sf http://localhost:8001/status > /dev/null 2>&1 || return 1
  # MinIO
  curl -sf http://localhost:9000/minio/health/live > /dev/null 2>&1 || return 1
  # Papercut
  curl -sf http://localhost:8025 > /dev/null 2>&1 || return 1
  return 0
}

while ! check_health; do
  if [ "$ELAPSED" -ge "$MAX_WAIT" ]; then
    echo -e "${RED}  ERROR: Services did not become healthy within ${MAX_WAIT}s${NC}"
    exit 1
  fi
  sleep $INTERVAL
  ELAPSED=$((ELAPSED + INTERVAL))
  echo "  Waiting... (${ELAPSED}s / ${MAX_WAIT}s)"
done

echo -e "${GREEN}  All services healthy${NC}"
echo ""

# ═══════════════════════════════════════════════════════════════
#  Step 3: Run init scripts (idempotent)
# ═══════════════════════════════════════════════════════════════
info "[3/4] Running init scripts (idempotent)..."

INIT_SCRIPT_DIR="${INFRA_DIR}/Scripts"

DYNAMODB_ENDPOINT="http://localhost:8042" bash "${INIT_SCRIPT_DIR}/init-dynamo-local.sh" 2>&1 | tail -5
bash "${INIT_SCRIPT_DIR}/init-minio.sh" 2>&1 | tail -5
bash "${INIT_SCRIPT_DIR}/init-kong.sh" 2>&1 | tail -3
echo ""

# ═══════════════════════════════════════════════════════════════
#  Step 4: Run test-all.sh
# ═══════════════════════════════════════════════════════════════
info "[4/4] Running test suite..."
echo ""

TEST_EXIT=0
bash "${SCRIPT_DIR}/test-all.sh" $E2E_FLAG || TEST_EXIT=$?

# ═══════════════════════════════════════════════════════════════
#  Teardown (optional)
# ═══════════════════════════════════════════════════════════════
if [ "$TEARDOWN" = true ]; then
  echo ""
  info "Tearing down Docker Compose..."
  docker compose -f "${COMPOSE_FILE}" down
  echo "  Docker services stopped"
fi

# ═══════════════════════════════════════════════════════════════
#  Final Summary
# ═══════════════════════════════════════════════════════════════
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
info "  Start & Test Complete"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if [ "$TEST_EXIT" -eq 0 ]; then
  echo -e "${GREEN}${BOLD}  ALL TESTS PASSED${NC}"
else
  echo -e "${RED}${BOLD}  TESTS FAILED${NC} (exit code: ${TEST_EXIT})"
fi

if [ "$TEARDOWN" = true ]; then
  echo "  Docker: torn down"
else
  echo "  Docker: still running (use --teardown to stop after tests)"
fi
echo ""

exit $TEST_EXIT
