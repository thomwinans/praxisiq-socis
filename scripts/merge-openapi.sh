#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║         SNAPP — OpenAPI Spec Collection & Merge         ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# ── Step 1: Collect specs from running services ───────────────────
echo "── Step 1: Collecting OpenAPI specs from services ──"
"$SCRIPT_DIR/collect-openapi.sh"

spec_count=$(find "$ROOT_DIR/api/specs" -name "*.json" 2>/dev/null | wc -l | tr -d ' ')
if [ "$spec_count" -eq 0 ]; then
  echo "ERROR: No specs collected. Are the services running?"
  echo "       Run: docker compose -f src/Snapp.Infrastructure/Docker/docker-compose.yml up -d"
  exit 1
fi

echo "── Step 2: Merging specs ──"
cd "$ROOT_DIR"
dotnet run --project src/Snapp.Tools.SpecMerge
merge_exit=$?

if [ $merge_exit -ne 0 ]; then
  echo ""
  echo "ERROR: Spec merge failed (exit code $merge_exit)"
  exit 1
fi

# ── Step 3: Copy merged spec to swagger-ui volume (if running) ────
echo ""
echo "── Step 3: Updating Swagger UI ──"
COMPOSE_FILE="$ROOT_DIR/src/Snapp.Infrastructure/Docker/docker-compose.yml"

if docker compose -f "$COMPOSE_FILE" ps swagger-ui --format '{{.State}}' 2>/dev/null | grep -q "running"; then
  docker compose -f "$COMPOSE_FILE" cp "$ROOT_DIR/api/snapp-api.json" swagger-ui:/usr/share/nginx/html/specs/snapp-api.json
  echo "  Copied snapp-api.json to Swagger UI container"
  # Also copy per-service specs
  for spec in "$ROOT_DIR/api/specs/"*.json; do
    svc=$(basename "$spec")
    docker compose -f "$COMPOSE_FILE" cp "$spec" "swagger-ui:/usr/share/nginx/html/specs/$svc"
    echo "  Copied $svc to Swagger UI container"
  done
  echo "  Swagger UI available at http://localhost:8090"
else
  echo "  Swagger UI not running — skipping copy."
  echo "  Merged spec available at: api/snapp-api.json"
fi

# ── Summary ───────────────────────────────────────────────────────
echo ""
echo "── Summary ──"
if [ -f "$ROOT_DIR/api/snapp-api.json" ]; then
  endpoint_count=$(python3 -c "
import json, sys
with open('$ROOT_DIR/api/snapp-api.json') as f:
    spec = json.load(f)
paths = spec.get('paths', {})
count = sum(len([m for m in p if m in ('get','post','put','patch','delete')]) for p in paths.values())
print(count)
" 2>/dev/null || echo "?")
  echo "  Specs collected: $spec_count"
  echo "  Merged spec: api/snapp-api.json"
  echo "  YAML output: api/snapp-api.yaml"
  echo "  Total endpoints: $endpoint_count"
  echo "  Status: SUCCESS"
else
  echo "  Status: FAILED — no output file"
  exit 1
fi
