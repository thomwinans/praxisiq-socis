#!/usr/bin/env bash
# S1.5-001 — MinIO bucket validation
# Runs init-minio.sh and verifies all buckets exist.
set -euo pipefail

MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
export AWS_ACCESS_KEY_ID="${MINIO_ACCESS_KEY:-minioadmin}"
export AWS_SECRET_ACCESS_KEY="${MINIO_SECRET_KEY:-minioadmin}"
export AWS_DEFAULT_REGION="us-east-1"
INIT_SCRIPT="src/Snapp.Infrastructure/Scripts/init-minio.sh"
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

# ─── Run init script (idempotent) ──────────────────────────
info "Running init-minio.sh..."
bash "${INIT_SCRIPT}" 2>&1 | tail -3
echo ""

# ─── Verify buckets ───────────────────────────────────────
info "═══ MinIO Bucket Validation ═══"
echo ""

EXPECTED_BUCKETS="snapp-media snapp-documents snapp-exports"

for bucket in ${EXPECTED_BUCKETS}; do
  if aws s3api head-bucket --bucket "${bucket}" --endpoint-url "${MINIO_ENDPOINT}" 2>/dev/null; then
    pass "Bucket ${bucket} exists"
  else
    fail "Bucket ${bucket} — not found"
  fi
done

echo ""

# ─── Summary ───────────────────────────────────────────────
info "═══ Summary ═══"
echo -e "  ${GREEN}PASS: ${PASS}${NC}  ${RED}FAIL: ${FAIL}${NC}"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}MinIO bucket validation FAILED${NC}"
  exit 1
fi

echo -e "${GREEN}All MinIO buckets validated${NC}"
exit 0
