#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SPECS_DIR="$ROOT_DIR/api/specs"
COMPOSE_DIR="$ROOT_DIR/src/Snapp.Infrastructure/Docker"

mkdir -p "$SPECS_DIR"

# Service name → Kong route prefix → direct port
declare -A SERVICES=(
  [auth]="auth:8081"
  [user]="users:8082"
  [network]="networks:8083"
  [content]="content:8084"
  [intelligence]="intel:8085"
  [transaction]="tx:8086"
  [notification]="notif:8087"
  [linkedin]="linkedin:8088"
)

# ── Step 1: Verify Docker Compose is running ──────────────────────
echo "==> Checking Docker Compose services..."
if ! docker compose -f "$COMPOSE_DIR/docker-compose.yml" ps --format json 2>/dev/null | head -1 | grep -q '"State"'; then
  echo "WARN: Could not confirm Docker Compose is running."
  echo "      Attempting to fetch specs anyway (services may be running outside compose)."
fi

# ── Step 2: Collect specs ─────────────────────────────────────────
collected=0
failed=0
failed_names=""

for svc in "${!SERVICES[@]}"; do
  IFS=':' read -r kong_prefix direct_port <<< "${SERVICES[$svc]}"
  out="$SPECS_DIR/${svc}.json"

  echo -n "  Fetching $svc ... "

  # Try via Kong first
  kong_url="http://localhost:8000/api/${kong_prefix}/openapi/v1.json"
  if curl -sf --max-time 5 "$kong_url" -o "$out" 2>/dev/null; then
    echo "OK (via Kong: $kong_url)"
    collected=$((collected + 1))
    continue
  fi

  # Fallback: direct port
  direct_url="http://localhost:${direct_port}/openapi/v1.json"
  if curl -sf --max-time 5 "$direct_url" -o "$out" 2>/dev/null; then
    echo "OK (direct: $direct_url)"
    collected=$((collected + 1))
    continue
  fi

  echo "FAILED"
  failed=$((failed + 1))
  failed_names="$failed_names $svc"
  rm -f "$out"
done

# ── Step 3: Report ────────────────────────────────────────────────
echo ""
echo "==> Collection complete: $collected succeeded, $failed failed"
if [ $failed -gt 0 ]; then
  echo "    Failed services:$failed_names"
fi

ls -la "$SPECS_DIR"/*.json 2>/dev/null || echo "    No spec files found."
echo ""
exit 0
