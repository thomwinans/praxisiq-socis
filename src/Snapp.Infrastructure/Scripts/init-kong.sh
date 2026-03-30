#!/usr/bin/env bash
# Validates Kong is running and declarative config is loaded.
# Kong uses DB-less mode with kong.yml mounted as declarative config,
# so no imperative setup is needed — this script just verifies.
set -euo pipefail

KONG_ADMIN="${KONG_ADMIN_URL:-http://localhost:8001}"
MAX_RETRIES=30
RETRY_INTERVAL=2

echo "Waiting for Kong admin API at ${KONG_ADMIN}..."

for i in $(seq 1 ${MAX_RETRIES}); do
  if curl -sf "${KONG_ADMIN}/status" > /dev/null 2>&1; then
    echo "Kong is ready."
    break
  fi
  if [ "$i" -eq "${MAX_RETRIES}" ]; then
    echo "ERROR: Kong did not become ready after $((MAX_RETRIES * RETRY_INTERVAL))s"
    exit 1
  fi
  sleep ${RETRY_INTERVAL}
done

echo ""
echo "Configured services:"
curl -sf "${KONG_ADMIN}/services" | python3 -m json.tool 2>/dev/null | grep '"name"' || echo "(none yet — services will appear when containers are added)"

echo ""
echo "Configured routes:"
curl -sf "${KONG_ADMIN}/routes" | python3 -m json.tool 2>/dev/null | grep '"name"' || echo "(none yet)"

echo ""
echo "Kong initialization complete."
