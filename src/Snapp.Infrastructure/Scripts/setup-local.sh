#!/usr/bin/env bash
# Master orchestration script for SNAPP local development environment.
# Brings up all infrastructure and initializes data stores.
# Idempotent — safe to run multiple times.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "${SCRIPT_DIR}")"
DOCKER_DIR="${INFRA_DIR}/Docker"
COMPOSE_FILE="${DOCKER_DIR}/docker-compose.yml"

echo "========================================="
echo " SNAPP Local Environment Setup"
echo "========================================="
echo ""

# ─── Step 1: Generate dev keys ──────────────────────────────────
echo "[1/5] Generating dev keys..."
bash "${DOCKER_DIR}/keys/generate-keys.sh"
echo ""

# ─── Step 2: Start Docker Compose ───────────────────────────────
echo "[2/5] Starting Docker Compose services..."
docker compose -f "${COMPOSE_FILE}" up -d
echo ""

# ─── Step 3: Wait for DynamoDB Local ────────────────────────────
echo "[3/5] Initializing DynamoDB Local tables..."
DYNAMODB_ENDPOINT="http://localhost:8042"
MAX_RETRIES=30
RETRY_INTERVAL=2

for i in $(seq 1 ${MAX_RETRIES}); do
  if curl -sf "${DYNAMODB_ENDPOINT}" > /dev/null 2>&1; then
    break
  fi
  if [ "$i" -eq "${MAX_RETRIES}" ]; then
    echo "ERROR: DynamoDB Local did not become ready"
    exit 1
  fi
  sleep ${RETRY_INTERVAL}
done

DYNAMODB_ENDPOINT="${DYNAMODB_ENDPOINT}" bash "${SCRIPT_DIR}/init-dynamo-local.sh"
echo ""

# ─── Step 4: Initialize MinIO ───────────────────────────────────
echo "[4/5] Initializing MinIO buckets..."
bash "${SCRIPT_DIR}/init-minio.sh"
echo ""

# ─── Step 5: Verify Kong ────────────────────────────────────────
echo "[5/5] Verifying Kong API Gateway..."
bash "${SCRIPT_DIR}/init-kong.sh"
echo ""

# ─── Summary ────────────────────────────────────────────────────
echo "========================================="
echo " SNAPP Local Environment Ready"
echo "========================================="
echo ""
echo " DynamoDB Local:  http://localhost:8042"
echo " Kong Proxy:      http://localhost:8000"
echo " Kong Admin:      http://localhost:8001"
echo " MinIO API:       http://localhost:9000"
echo " MinIO Console:   http://localhost:9001"
echo " Papercut SMTP:   localhost:1025"
echo " Papercut Web:    http://localhost:8025"
echo " Swagger UI:      http://localhost:8090"
echo ""
echo " To stop:  docker compose -f ${COMPOSE_FILE} down"
echo " To reset: docker compose -f ${COMPOSE_FILE} down -v"
echo ""
