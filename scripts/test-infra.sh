#!/usr/bin/env bash
# S1.5-001 — Docker Compose infrastructure health validation
# Starts Docker Compose and verifies all services are healthy.
set -euo pipefail

COMPOSE_FILE="src/Snapp.Infrastructure/Docker/docker-compose.yml"
TIMEOUT=120
INTERVAL=3
PASS=0
FAIL=0

# ─── Color output ───────────────────────────────────────────
if [ -t 1 ]; then
  GREEN='\033[0;32m'; RED='\033[0;31m'; BOLD='\033[1m'; NC='\033[0m'
else
  GREEN=''; RED=''; BOLD=''; NC=''
fi

pass() { echo -e "  ${GREEN}PASS${NC}  $1"; PASS=$((PASS + 1)); }
fail() { echo -e "  ${RED}FAIL${NC}  $1"; FAIL=$((FAIL + 1)); }
info() { echo -e "${BOLD}$1${NC}"; }

# ─── Start Docker Compose ──────────────────────────────────
info "Starting Docker Compose..."
docker compose -f "${COMPOSE_FILE}" up -d --quiet-pull 2>&1 | tail -5

# Wait for a service to respond. Mode determines what counts as success:
#   "any"  — any HTTP response (even 400/500) means alive
#   "ok"   — HTTP 2xx required
#   "json" — HTTP 2xx + validator command run against response body
wait_for() {
  local name="$1" url="$2" mode="$3" validator="${4:-true}"
  local elapsed=0
  info "Checking ${name}..."
  while [ $elapsed -lt $TIMEOUT ]; do
    if [ "${mode}" = "any" ]; then
      local code
      code=$(curl -so /dev/null -w "%{http_code}" --max-time 5 "${url}" 2>/dev/null) || true
      if [ "${code}" != "000" ]; then
        pass "${name} — ${url} (HTTP ${code})"
        return 0
      fi
    elif [ "${mode}" = "json" ]; then
      local response
      response=$(curl -sf --max-time 5 "${url}" 2>/dev/null) && {
        if eval "${validator}" <<< "${response}" 2>/dev/null; then
          pass "${name} — ${url}"
          return 0
        fi
      }
    else
      if curl -sf --max-time 5 "${url}" > /dev/null 2>&1; then
        pass "${name} — ${url}"
        return 0
      fi
    fi
    sleep $INTERVAL
    elapsed=$((elapsed + INTERVAL))
  done
  fail "${name} — timed out after ${TIMEOUT}s (${url})"
  return 1
}

echo ""
info "═══ Infrastructure Health Checks ═══"
echo ""

# DynamoDB Local: any HTTP response (even 400) means it's alive
wait_for "DynamoDB Local" "http://localhost:8042" "any" || true

# Kong: admin API returns JSON with server info (DB-less mode has no database field)
wait_for "Kong Admin API" "http://localhost:8001/status" "json" \
  'python3 -c "import sys,json; d=json.load(sys.stdin); assert \"server\" in d"' || true

# MinIO: health endpoint returns 200
wait_for "MinIO" "http://localhost:9000/minio/health/live" "ok" || true

# Papercut: web UI returns 200
wait_for "Papercut SMTP" "http://localhost:8025" "ok" || true

# Swagger UI: returns 200
wait_for "Swagger UI" "http://localhost:8090" "ok" || true

# ─── Summary ───────────────────────────────────────────────
echo ""
info "═══ Summary ═══"
echo -e "  ${GREEN}PASS: ${PASS}${NC}  ${RED}FAIL: ${FAIL}${NC}"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}Infrastructure validation FAILED${NC}"
  exit 1
fi

echo -e "${GREEN}All infrastructure services healthy${NC}"
exit 0
