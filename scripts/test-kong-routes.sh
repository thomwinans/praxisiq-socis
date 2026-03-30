#!/usr/bin/env bash
# S2-004 — Kong route, plugin, and JWT validation
# Verifies all routes, JWT plugin on protected routes, and CORS plugin.
set -euo pipefail

KONG_ADMIN="${KONG_ADMIN_URL:-http://localhost:8001}"
KONG_PROXY="${KONG_PROXY_URL:-http://localhost:8000}"
PASS=0
FAIL=0

# ─── Color output ───────────────────────────────────────────
if [ -t 1 ]; then
  GREEN='\033[0;32m'; RED='\033[0;31m'; BOLD='\033[1m'; YELLOW='\033[0;33m'; NC='\033[0m'
else
  GREEN=''; RED=''; BOLD=''; YELLOW=''; NC=''
fi

pass() { echo -e "  ${GREEN}PASS${NC}  $1"; PASS=$((PASS + 1)); }
fail() { echo -e "  ${RED}FAIL${NC}  $1"; FAIL=$((FAIL + 1)); }
warn() { echo -e "  ${YELLOW}WARN${NC}  $1"; }
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
EXPECTED_ROUTES="/api/auth/magic-link /api/auth/validate /api/auth/refresh /api/auth/logout /api/users /api/networks /api/content /api/intel /api/tx /api/notif /api/linkedin"

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

# ─── JWT Plugin validation ─────────────────────────────────
info "JWT Plugin (per-route):"

# Get all JWT plugins and their associated route names
jwt_plugin_routes=$(echo "${plugins_json}" | python3 -c "
import sys,json
plugins = json.load(sys.stdin)['data']
jwt_plugins = [p for p in plugins if p['name'] == 'jwt']
print(len(jwt_plugins))
" 2>/dev/null || echo "0")

if [ "${jwt_plugin_routes}" -gt 0 ]; then
  pass "JWT plugin configured (${jwt_plugin_routes} route-level instances)"
else
  fail "JWT plugin — not configured on any routes"
fi

# Verify JWT is applied to each protected route
PROTECTED_ROUTES="auth-logout-route users-route networks-route content-route intel-route tx-route notif-route linkedin-route"

for route_name in ${PROTECTED_ROUTES}; do
  # Get route ID by name
  route_id=$(echo "${routes_json}" | python3 -c "
import sys,json
for r in json.load(sys.stdin)['data']:
  if r['name'] == '${route_name}': print(r['id'])
" 2>/dev/null || echo "")

  if [ -z "${route_id}" ]; then
    fail "JWT on ${route_name} — route not found"
    continue
  fi

  has_jwt=$(echo "${plugins_json}" | python3 -c "
import sys,json
plugins = json.load(sys.stdin)['data']
for p in plugins:
  if p['name'] == 'jwt' and p.get('route', {}).get('id') == '${route_id}':
    print('yes')
    break
" 2>/dev/null || echo "")

  if [ "${has_jwt}" = "yes" ]; then
    pass "JWT on ${route_name}"
  else
    fail "JWT on ${route_name} — plugin not found"
  fi
done

# Verify JWT is NOT on public auth routes
PUBLIC_ROUTES="auth-magic-link-route auth-validate-route auth-refresh-route"

for route_name in ${PUBLIC_ROUTES}; do
  route_id=$(echo "${routes_json}" | python3 -c "
import sys,json
for r in json.load(sys.stdin)['data']:
  if r['name'] == '${route_name}': print(r['id'])
" 2>/dev/null || echo "")

  if [ -z "${route_id}" ]; then
    warn "Public route ${route_name} — not found (skipping JWT check)"
    continue
  fi

  has_jwt=$(echo "${plugins_json}" | python3 -c "
import sys,json
plugins = json.load(sys.stdin)['data']
for p in plugins:
  if p['name'] == 'jwt' and p.get('route', {}).get('id') == '${route_id}':
    print('yes')
    break
" 2>/dev/null || echo "")

  if [ "${has_jwt}" = "yes" ]; then
    fail "JWT on ${route_name} — public route should NOT have JWT"
  else
    pass "No JWT on ${route_name} (public)"
  fi
done

echo ""

# ─── JWT Consumer validation ──────────────────────────────
info "JWT Consumer:"
consumers_json=$(curl -sf "${KONG_ADMIN}/consumers" 2>/dev/null || echo '{"data":[]}')
has_consumer=$(echo "${consumers_json}" | python3 -c "
import sys,json
for c in json.load(sys.stdin)['data']:
  if c['username'] == 'snapp-auth-issuer':
    print('yes')
    break
" 2>/dev/null || echo "")

if [ "${has_consumer}" = "yes" ]; then
  pass "Consumer snapp-auth-issuer exists"

  # Verify JWT credentials exist for consumer
  consumer_id=$(echo "${consumers_json}" | python3 -c "
import sys,json
for c in json.load(sys.stdin)['data']:
  if c['username'] == 'snapp-auth-issuer': print(c['id'])
" 2>/dev/null || echo "")

  jwt_creds=$(curl -sf "${KONG_ADMIN}/consumers/${consumer_id}/jwt" 2>/dev/null || echo '{"data":[]}')
  has_rs256=$(echo "${jwt_creds}" | python3 -c "
import sys,json
for c in json.load(sys.stdin)['data']:
  if c.get('algorithm') == 'RS256' and c.get('key') == 'snapp-auth':
    print('yes')
    break
" 2>/dev/null || echo "")

  if [ "${has_rs256}" = "yes" ]; then
    pass "RS256 JWT credential with key=snapp-auth"
  else
    fail "RS256 JWT credential — not found or misconfigured"
  fi
else
  fail "Consumer snapp-auth-issuer — not found"
fi

echo ""

# ─── Proxy-level JWT tests ────────────────────────────────
info "Proxy-level JWT enforcement:"

# Test protected route without token → 401
http_code=$(curl -s -o /dev/null -w "%{http_code}" "${KONG_PROXY}/api/users/me" 2>/dev/null || echo "000")
if [ "${http_code}" = "401" ]; then
  pass "GET /api/users/me without token → 401"
elif [ "${http_code}" = "000" ]; then
  warn "GET /api/users/me — Kong proxy not reachable (skipping)"
else
  fail "GET /api/users/me without token → ${http_code} (expected 401)"
fi

# Test public route without token → NOT 401
http_code=$(curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{"email":"test@test.com"}' "${KONG_PROXY}/api/auth/magic-link" 2>/dev/null || echo "000")
if [ "${http_code}" != "401" ] && [ "${http_code}" != "000" ]; then
  pass "POST /api/auth/magic-link without token → ${http_code} (not 401)"
elif [ "${http_code}" = "000" ]; then
  warn "POST /api/auth/magic-link — Kong proxy not reachable (skipping)"
else
  fail "POST /api/auth/magic-link without token → 401 (should be public)"
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
