#!/usr/bin/env bash
# S1.5-001 — DynamoDB table validation
# Verifies all 6 SNAPP tables exist with correct schema and GSIs.
set -euo pipefail

ENDPOINT="${DYNAMODB_ENDPOINT:-http://localhost:8042}"
REGION="${AWS_DEFAULT_REGION:-us-east-1}"
INIT_SCRIPT="src/Snapp.Infrastructure/Scripts/init-dynamo-local.sh"
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

aws_dynamo() {
  aws dynamodb "$@" --endpoint-url "${ENDPOINT}" --region "${REGION}" --no-cli-pager 2>/dev/null
}

# ─── Run init script (idempotent) ──────────────────────────
info "Running init-dynamo-local.sh..."
bash "${INIT_SCRIPT}" 2>&1 | tail -3
echo ""

# Returns expected GSI names for a table (space-separated)
expected_gsis_for() {
  case "$1" in
    snapp-users)    echo "GSI-Email GSI-Specialty" ;;
    snapp-networks) echo "GSI-UserNetworks GSI-PendingApps" ;;
    snapp-content)  echo "GSI-UserPosts" ;;
    snapp-intel)    echo "GSI-BenchmarkLookup GSI-RiskFlags" ;;
    snapp-tx)       echo "GSI-UserReferrals GSI-OpenReferrals" ;;
    snapp-notif)    echo "GSI-UndigestedNotifs GSI-DigestQueue" ;;
  esac
}

info "═══ DynamoDB Table Validation ═══"
echo ""

for table in snapp-users snapp-networks snapp-content snapp-intel snapp-tx snapp-notif; do
  info "Table: ${table}"

  # Describe table
  desc=$(aws_dynamo describe-table --table-name "${table}" 2>&1) || {
    fail "${table} — table does not exist"
    echo ""
    continue
  }

  # Check table status
  status=$(echo "${desc}" | python3 -c "import sys,json; print(json.load(sys.stdin)['Table']['TableStatus'])")
  if [ "${status}" = "ACTIVE" ]; then
    pass "${table} — status ACTIVE"
  else
    fail "${table} — status ${status} (expected ACTIVE)"
  fi

  # Check PK/SK
  keys=$(echo "${desc}" | python3 -c "
import sys,json
ks = json.load(sys.stdin)['Table']['KeySchema']
h = r = ''
for k in ks:
  if k['KeyType']=='HASH': h = k['AttributeName']
  if k['KeyType']=='RANGE': r = k['AttributeName']
print(h + '/' + r)
")
  if [ "${keys}" = "PK/SK" ]; then
    pass "${table} — keys PK/SK correct"
  else
    fail "${table} — keys are ${keys} (expected PK/SK)"
  fi

  # Check GSIs
  expected_gsis=$(expected_gsis_for "${table}")
  actual_gsis=$(echo "${desc}" | python3 -c "
import sys,json
gsis = json.load(sys.stdin)['Table'].get('GlobalSecondaryIndexes',[])
for g in gsis: print(g['IndexName'])
" | sort)

  for gsi in ${expected_gsis}; do
    if echo "${actual_gsis}" | grep -q "^${gsi}$"; then
      pass "${table} — GSI ${gsi} exists"
    else
      fail "${table} — GSI ${gsi} missing"
    fi
  done

  echo ""
done

# ─── Summary ───────────────────────────────────────────────
info "═══ Summary ═══"
echo -e "  ${GREEN}PASS: ${PASS}${NC}  ${RED}FAIL: ${FAIL}${NC}"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}DynamoDB table validation FAILED${NC}"
  exit 1
fi

echo -e "${GREEN}All DynamoDB tables validated${NC}"
exit 0
