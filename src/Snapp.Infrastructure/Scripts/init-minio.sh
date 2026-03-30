#!/usr/bin/env bash
# Creates MinIO buckets for SNAPP using AWS CLI (S3-compatible). Idempotent.
set -euo pipefail

MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
export AWS_ACCESS_KEY_ID="${MINIO_ACCESS_KEY:-minioadmin}"
export AWS_SECRET_ACCESS_KEY="${MINIO_SECRET_KEY:-minioadmin}"
export AWS_DEFAULT_REGION="us-east-1"

MAX_RETRIES=30
RETRY_INTERVAL=2

echo "Waiting for MinIO at ${MINIO_ENDPOINT}..."

for i in $(seq 1 ${MAX_RETRIES}); do
  if curl -sf "${MINIO_ENDPOINT}/minio/health/live" > /dev/null 2>&1; then
    echo "MinIO is ready."
    break
  fi
  if [ "$i" -eq "${MAX_RETRIES}" ]; then
    echo "ERROR: MinIO did not become ready after $((MAX_RETRIES * RETRY_INTERVAL))s"
    exit 1
  fi
  sleep ${RETRY_INTERVAL}
done

BUCKETS=(
  "snapp-media"        # Profile photos, network logos
  "snapp-documents"    # Deal room documents
  "snapp-exports"      # Report exports
)

for bucket in "${BUCKETS[@]}"; do
  if aws s3api head-bucket --bucket "${bucket}" --endpoint-url "${MINIO_ENDPOINT}" 2>/dev/null; then
    echo "Bucket ${bucket} already exists — skipping"
  else
    aws s3api create-bucket --bucket "${bucket}" --endpoint-url "${MINIO_ENDPOINT}" --no-cli-pager > /dev/null 2>&1
    echo "Created bucket ${bucket}"
  fi
done

echo ""
echo "MinIO initialization complete."
aws s3 ls --endpoint-url "${MINIO_ENDPOINT}" --no-cli-pager
