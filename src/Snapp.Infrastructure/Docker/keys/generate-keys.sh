#!/usr/bin/env bash
# Generates dev-only encryption keys. NOT for production use.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# AES-256 master key for field-level PII encryption (32 bytes = 256 bits)
KEY_FILE="${SCRIPT_DIR}/dev-master.key"
if [ ! -f "${KEY_FILE}" ]; then
  openssl rand 32 > "${KEY_FILE}"
  chmod 600 "${KEY_FILE}"
  echo "Generated dev-master.key"
else
  echo "dev-master.key already exists — skipping"
fi

# RSA key for JWT signing
PEM_FILE="${SCRIPT_DIR}/jwt-signing.pem"
if [ ! -f "${PEM_FILE}" ]; then
  openssl genpkey -algorithm RSA -out "${PEM_FILE}" -pkeyopt rsa_keygen_bits:2048 2>/dev/null
  chmod 600 "${PEM_FILE}"
  echo "Generated jwt-signing.pem"
else
  echo "jwt-signing.pem already exists — skipping"
fi

# RSA public key for Kong JWT validation (derived from private key)
PUB_FILE="${SCRIPT_DIR}/jwt-signing.pub"
if [ ! -f "${PUB_FILE}" ] && [ -f "${PEM_FILE}" ]; then
  openssl rsa -in "${PEM_FILE}" -pubout -out "${PUB_FILE}" 2>/dev/null
  chmod 644 "${PUB_FILE}"
  echo "Generated jwt-signing.pub"
else
  echo "jwt-signing.pub already exists — skipping"
fi
