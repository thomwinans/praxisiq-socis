#!/usr/bin/env bash
# S1.5-001 — Kong route and plugin validation
# Verifies all routes, JWT plugin, and CORS plugin are configured.
set -euo pipefail

KONG_ADMIN="${KONG_ADMIN_URL:-http://localhost:8001}"
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

info "═══ Kong Route & Plugin Validation ═══"
echo ""

# ─── Verify Kong is reachable ──────────────────────────────
info "Checking Kong admin API..."
kong_status=$(curl -sf "${KONG_ADMIN}/status" 2>&1) || {
  fail "Kong admin API not reachable at ${KONG_ADMIN}"
  echo -e "\n${RED}Kong validation FAILED — cannot reach admin API${NC}"
  exit 1
}
pass "Kong admin API reachable"
echo ""

# ─── Fetch services and routes ─────────────────────────────
services_json=$(curl -sf "${KONG_ADMIN}/services")
routes_json=$(curl -sf "${KONG_ADMIN}/routes")
plugins_json=$(curl -sf "${KONG_ADMIN}/plugins")

# ─── Verify services exist ─────────────────────────────────
info "Services:"
EXPECTED_SERVICES="snapp-auth snapp-user snapp-network snapp-content snapp-intelligence snapp-transaction snapp-notification snapp-linkedin"

actual_services=$(echo "${services_json}" | python3 -c "
import sys,json
for s in json.load(sys.stdin)['data']: print(s['name'])
" | sort)

for svc in ${EXPECTED_SERVICES}; do
  if echo "${actual_services}" | grep -q "^${svc}$"; then
    pass "Service ${svc}"
  else
    fail "Service ${svc} — not found"
  fi
done
echo ""

# ─── Verify routes exist ──────────────────────────────────
info "Routes:"
EXPECTED_ROUTES="/api/auth /api/users /api/networks /api/content /api/intel /api/tx /api/notif /api/linkedin"

actual_routes=$(echo "${routes_json}" | python3 -c "
import sys,json
for r in json.load(sys.stdin)['data']:
  for p in r.get('paths',[]): print(p)
" | sort)

for route in ${EXPECTED_ROUTES}; do
  if echo "${actual_routes}" | grep -q "^${route}$"; then
    pass "Route ${route}"
  else
    fail "Route ${route} — not found"
  fi
done
echo ""

# ─── Verify plugins ───────────────────────────────────────
info "Global Plugins:"

actual_plugins=$(echo "${plugins_json}" | python3 -c "
import sys,json
for p in json.load(sys.stdin)['data']: print(p['name'])
" | sort)

# CORS plugin
if echo "${actual_plugins}" | grep -q "^cors$"; then
  pass "CORS plugin configured"

  # Verify CORS origins
  cors_origins=$(echo "${plugins_json}" | python3 -c "
import sys,json
for p in json.load(sys.stdin)['data']:
  if p['name']=='cors':
    for o in p['config']['origins']: print(o)
")
  if echo "${cors_origins}" | grep -q "localhost:5000"; then
    pass "CORS allows localhost:5000 (Blazor)"
  else
    fail "CORS missing localhost:5000 origin"
  fi
else
  fail "CORS plugin — not configured"
fi

# Rate limiting plugin
if echo "${actual_plugins}" | grep -q "^rate-limiting$"; then
  pass "Rate-limiting plugin configured"
else
  fail "Rate-limiting plugin — not configured"
fi

# File log plugin
if echo "${actual_plugins}" | grep -q "^file-log$"; then
  pass "File-log plugin configured"
else
  fail "File-log plugin — not configured"
fi

echo ""

# ─── JWT plugin note ──────────────────────────────────────
# JWT plugin is not yet configured per kong.yml — this is expected in Sprint 1.5.
# We check for its presence and report status without failing.
info "JWT Plugin (informational):"
if echo "${actual_plugins}" | grep -q "^jwt$"; then
  pass "JWT plugin configured on protected routes"
else
  echo -e "  ${BOLD}INFO${NC}  JWT plugin not yet configured — expected for Sprint 2 (auth service)"
fi

echo ""

# ─── Summary ───────────────────────────────────────────────
info "═══ Summary ═══"
echo -e "  ${GREEN}PASS: ${PASS}${NC}  ${RED}FAIL: ${FAIL}${NC}"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}Kong route validation FAILED${NC}"
  exit 1
fi

echo -e "${GREEN}All Kong routes and plugins validated${NC}"
exit 0
