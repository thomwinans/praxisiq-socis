#!/usr/bin/env bash
# =============================================================================
# SNAPP Full Build — Runs all sprints in sequence
# =============================================================================
# Chains all sprint files in order. After each sprint, checks for failures.
# By default, stops on first sprint with failures. Use --continue-on-failure
# to push through and attempt all sprints regardless.
#
# Usage:
#   ./scripts/snapp-build-all.sh [--continue-on-failure] [--start-from S3]
#
# Example:
#   ./scripts/snapp-build-all.sh                          # run all, stop on failure
#   ./scripts/snapp-build-all.sh --continue-on-failure    # run all, skip failures
#   ./scripts/snapp-build-all.sh --start-from S2          # resume from Sprint 2
# =============================================================================

set -uo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TASKS_DIR="${PROJECT_ROOT}/.claude/tasks"
REPORT_DIR="${PROJECT_ROOT}/.claude/reports"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
MASTER_REPORT="${REPORT_DIR}/master-report_${TIMESTAMP}.md"
CONTINUE_ON_FAILURE=false
START_FROM=""

# Parse flags
while [[ $# -gt 0 ]]; do
    case $1 in
        --continue-on-failure) CONTINUE_ON_FAILURE=true; shift ;;
        --start-from) START_FROM="$2"; shift 2 ;;
        *) echo "Unknown flag: $1"; exit 1 ;;
    esac
done

# Sprint order — add new sprint files here as they're created
SPRINTS=(
    "sprint-1.json"
    "sprint-1.5.json"
    "sprint-2.json"
    "sprint-3.json"
    "sprint-4.json"
    "sprint-5.json"
    "sprint-6.json"
    "sprint-7.json"
    "sprint-8.json"
    "sprint-9.json"
    "sprint-10.json"
    "sprint-11.json"
    "sprint-12.json"
    "sprint-13.json"
    "sprint-14.json"
    "sprint-15.json"
    "sprint-16.json"
)

mkdir -p "$REPORT_DIR"

log() {
    echo "[$(date '+%H:%M:%S')] $*"
}

# ── Master Report Header ──────────────────────────────────────────────────
cat > "$MASTER_REPORT" <<EOF
# SNAPP Master Build Report
**Started**: $(date '+%Y-%m-%d %H:%M:%S')
**Mode**: $(if $CONTINUE_ON_FAILURE; then echo 'Continue on failure'; else echo 'Stop on failure'; fi)
$(if [[ -n "$START_FROM" ]]; then echo "**Resumed from**: ${START_FROM}"; fi)

## Sprint Results

| Sprint | File | Status | Completed | Failed | Duration |
|--------|------|--------|-----------|--------|----------|
EOF

# ── Main Loop ─────────────────────────────────────────────────────────────
total_sprints=0
passed_sprints=0
failed_sprints=0
skipped_sprints=0
skipping=true

if [[ -z "$START_FROM" ]]; then
    skipping=false
fi

overall_start=$(date +%s)

for sprint_file in "${SPRINTS[@]}"; do
    sprint_path="${TASKS_DIR}/${sprint_file}"

    # Skip if file doesn't exist
    if [[ ! -f "$sprint_path" ]]; then
        log "SKIP: ${sprint_file} (file not found)"
        continue
    fi

    sprint_name=$(jq -r '.sprint' "$sprint_path")
    total_tasks=$(jq '.tasks | length' "$sprint_path")

    # Handle --start-from
    if [[ "$skipping" == "true" ]]; then
        if [[ "$sprint_name" == "$START_FROM" ]]; then
            skipping=false
        else
            # Check if already completed
            all_done=true
            while IFS= read -r task_id; do
                status=$(jq -r --arg id "$task_id" '.[$id] // "pending"' "${TASKS_DIR}/progress.json")
                if [[ "$status" != "completed" ]]; then
                    all_done=false
                    break
                fi
            done < <(jq -r '.tasks[].id' "$sprint_path")

            if [[ "$all_done" == "true" ]]; then
                log "ALREADY DONE: ${sprint_name} — all ${total_tasks} tasks completed"
                echo "| ${sprint_name} | ${sprint_file} | Already done | ${total_tasks} | 0 | — |" >> "$MASTER_REPORT"
                ((passed_sprints++))
            else
                log "SKIP: ${sprint_name} (before --start-from ${START_FROM})"
                echo "| ${sprint_name} | ${sprint_file} | Skipped | — | — | — |" >> "$MASTER_REPORT"
                ((skipped_sprints++))
            fi
            ((total_sprints++))
            continue
        fi
    fi

    ((total_sprints++))

    # Check if sprint is already completed
    all_done=true
    while IFS= read -r task_id; do
        status=$(jq -r --arg id "$task_id" '.[$id] // "pending"' "${TASKS_DIR}/progress.json")
        if [[ "$status" != "completed" ]]; then
            all_done=false
            break
        fi
    done < <(jq -r '.tasks[].id' "$sprint_path")

    if [[ "$all_done" == "true" ]]; then
        log "ALREADY DONE: ${sprint_name} — all ${total_tasks} tasks completed"
        echo "| ${sprint_name} | ${sprint_file} | Already done | ${total_tasks} | 0 | — |" >> "$MASTER_REPORT"
        ((passed_sprints++))
        continue
    fi

    log ""
    log "╔══════════════════════════════════════════════════════════╗"
    log "║  SPRINT: ${sprint_name} (${total_tasks} tasks)"
    log "╚══════════════════════════════════════════════════════════╝"

    sprint_start=$(date +%s)

    # Run the sprint
    "${PROJECT_ROOT}/scripts/snapp-build.sh" "$sprint_path"
    sprint_exit=$?

    sprint_end=$(date +%s)
    sprint_duration=$(( sprint_end - sprint_start ))
    sprint_minutes=$(( sprint_duration / 60 ))
    sprint_seconds=$(( sprint_duration % 60 ))

    # Count results from progress
    completed=0
    failed=0
    while IFS= read -r task_id; do
        status=$(jq -r --arg id "$task_id" '.[$id] // "pending"' "${TASKS_DIR}/progress.json")
        case "$status" in
            completed) ((completed++)) ;;
            failed|blocked) ((failed++)) ;;
        esac
    done < <(jq -r '.tasks[].id' "$sprint_path")

    if [[ $failed -eq 0 ]]; then
        status_text="PASSED"
        ((passed_sprints++))
        log "SPRINT ${sprint_name}: PASSED (${completed}/${total_tasks} tasks, ${sprint_minutes}m ${sprint_seconds}s)"

        # Commit and push after successful sprint
        cd "$PROJECT_ROOT"
        if [[ -n "$(git status --porcelain)" ]]; then
            git add -A
            git commit -m "[OPS] Sprint ${sprint_name} completed — ${completed}/${total_tasks} tasks passed"
            git push origin main 2>/dev/null || true
        fi
    else
        status_text="FAILED"
        ((failed_sprints++))
        log "SPRINT ${sprint_name}: FAILED (${completed} passed, ${failed} failed, ${sprint_minutes}m ${sprint_seconds}s)"

        if [[ "$CONTINUE_ON_FAILURE" == "false" ]]; then
            echo "| ${sprint_name} | ${sprint_file} | ${status_text} | ${completed} | ${failed} | ${sprint_minutes}m ${sprint_seconds}s |" >> "$MASTER_REPORT"

            # Write footer and exit
            cat >> "$MASTER_REPORT" <<STOP_EOF

## Stopped

Build stopped at Sprint ${sprint_name} due to failures. Use \`--continue-on-failure\` to push through, or fix the failures and \`--start-from ${sprint_name}\` to resume.

**Ended**: $(date '+%Y-%m-%d %H:%M:%S')
STOP_EOF
            log ""
            log "BUILD STOPPED at Sprint ${sprint_name}. See: ${MASTER_REPORT}"
            exit 1
        fi
    fi

    echo "| ${sprint_name} | ${sprint_file} | ${status_text} | ${completed} | ${failed} | ${sprint_minutes}m ${sprint_seconds}s |" >> "$MASTER_REPORT"
done

# ── Master Report Footer ─────────────────────────────────────────────────
overall_end=$(date +%s)
overall_duration=$(( overall_end - overall_start ))
overall_hours=$(( overall_duration / 3600 ))
overall_minutes=$(( (overall_duration % 3600) / 60 ))

cat >> "$MASTER_REPORT" <<EOF

## Summary

- **Total sprints**: ${total_sprints}
- **Passed**: ${passed_sprints}
- **Failed**: ${failed_sprints}
- **Skipped**: ${skipped_sprints}
- **Total duration**: ${overall_hours}h ${overall_minutes}m
- **Ended**: $(date '+%Y-%m-%d %H:%M:%S')

## Next Steps

$(if [[ $failed_sprints -gt 0 ]]; then
    echo "- Review failed sprint reports in .claude/reports/"
    echo "- Check task logs in .claude/logs/"
    echo "- Fix failures and re-run: \`./scripts/snapp-build-all.sh --start-from {failed_sprint}\`"
else
    echo "All sprints passed. The build is complete."
fi)

## Individual Sprint Reports

$(ls -1 "${REPORT_DIR}"/report_*.md 2>/dev/null | while read -r f; do
    echo "- $(basename "$f")"
done)
EOF

log ""
log "═══════════════════════════════════════════════════════════"
log "  MASTER BUILD COMPLETE"
log "  Passed: ${passed_sprints}  Failed: ${failed_sprints}  Skipped: ${skipped_sprints}"
log "  Duration: ${overall_hours}h ${overall_minutes}m"
log "  Report: ${MASTER_REPORT}"
log "═══════════════════════════════════════════════════════════"
