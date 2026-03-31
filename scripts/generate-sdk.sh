#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SPEC_FILE="$ROOT_DIR/api/snapp-api.json"
OUTPUT_DIR="$ROOT_DIR/src/Snapp.Sdk/Generated"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║         SNAPP — SDK Generation via Kiota                ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# ── Step 1: Ensure merged spec is current ────────────────────────
if [ "${SKIP_MERGE:-}" != "1" ]; then
  echo "── Step 1: Updating merged OpenAPI spec ──"
  "$SCRIPT_DIR/merge-openapi.sh"
else
  echo "── Step 1: Skipping spec merge (SKIP_MERGE=1) ──"
fi

if [ ! -f "$SPEC_FILE" ]; then
  echo "ERROR: Merged spec not found at $SPEC_FILE"
  echo "       Run merge-openapi.sh first or set SKIP_MERGE=1 if spec already exists."
  exit 1
fi

# ── Step 2: Clean previous generation ────────────────────────────
echo ""
echo "── Step 2: Cleaning previous SDK output ──"
if [ -d "$OUTPUT_DIR" ]; then
  rm -rf "$OUTPUT_DIR"
  echo "  Removed $OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

# ── Step 3: Generate SDK via Kiota ───────────────────────────────
echo ""
echo "── Step 3: Generating C# SDK with Kiota ──"
cd "$ROOT_DIR"
dotnet tool restore

dotnet kiota generate \
  --language CSharp \
  --openapi "$SPEC_FILE" \
  --output "$OUTPUT_DIR" \
  --class-name SnappApiClient \
  --namespace-name Snapp.Sdk \
  --exclude-backward-compatible

kiota_exit=$?
if [ $kiota_exit -ne 0 ]; then
  echo ""
  echo "ERROR: Kiota generation failed (exit code $kiota_exit)"
  exit 1
fi

# ── Step 4: Build SDK project ────────────────────────────────────
echo ""
echo "── Step 4: Building Snapp.Sdk ──"
dotnet build src/Snapp.Sdk/ --no-restore -c Release -v q
build_exit=$?

# ── Summary ──────────────────────────────────────────────────────
echo ""
echo "── Summary ──"
generated_count=$(find "$OUTPUT_DIR" -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')
echo "  Generated files: $generated_count"
echo "  Output: $OUTPUT_DIR"
if [ $build_exit -eq 0 ]; then
  echo "  Build: SUCCESS"
else
  echo "  Build: FAILED (exit code $build_exit)"
  exit 1
fi
