#!/usr/bin/env bash
# =============================================================================
# SNAPP Agentic Build Orchestrator
# =============================================================================
# Reads a sprint task file, resolves dependencies, and dispatches tasks to
# Claude Code agents sequentially (respecting dependency order). Each agent
# runs in its own git worktree with full autonomy — no human prompts.
#
# Usage:
#   ./scripts/snapp-build.sh [sprint-file] [--dry-run] [--resume TASK_ID]
#
# Example:
#   ./scripts/snapp-build.sh .claude/tasks/sprint-1.json
#   ./scripts/snapp-build.sh .claude/tasks/sprint-1.json --resume S1-004
#
# Prerequisites:
#   - claude CLI installed and authenticated
#   - jq installed
#   - git initialized in project root
# =============================================================================

set -euo pipefail

# ── Configuration ──────────────────────────────────────────────────────────
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
AGENTS_DIR="${PROJECT_ROOT}/.claude/agents"
REPORT_DIR="${PROJECT_ROOT}/.claude/reports"
LOG_DIR="${PROJECT_ROOT}/.claude/logs"
PROGRESS_FILE="${PROJECT_ROOT}/.claude/tasks/progress.json"
SPRINT_FILE="${1:-.claude/tasks/sprint-1.json}"
DRY_RUN=false
RESUME_FROM=""

# Parse flags
shift || true
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run) DRY_RUN=true; shift ;;
        --resume) RESUME_FROM="$2"; shift 2 ;;
        *) echo "Unknown flag: $1"; exit 1 ;;
    esac
done

# Resolve sprint file path
if [[ ! "$SPRINT_FILE" = /* ]]; then
    SPRINT_FILE="${PROJECT_ROOT}/${SPRINT_FILE}"
fi

# ── Prerequisites Check ───────────────────────────────────────────────────
check_prereqs() {
    local missing=()
    command -v claude >/dev/null 2>&1 || missing+=("claude")
    command -v jq >/dev/null 2>&1 || missing+=("jq")
    command -v git >/dev/null 2>&1 || missing+=("git")

    if [[ ${#missing[@]} -gt 0 ]]; then
        echo "ERROR: Missing required tools: ${missing[*]}"
        exit 1
    fi

    if [[ ! -f "$SPRINT_FILE" ]]; then
        echo "ERROR: Sprint file not found: $SPRINT_FILE"
        exit 1
    fi
}

# ── Logging ───────────────────────────────────────────────────────────────
mkdir -p "$REPORT_DIR" "$LOG_DIR"

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
REPORT_FILE="${REPORT_DIR}/report_${TIMESTAMP}.md"

log() {
    echo "[$(date '+%H:%M:%S')] $*"
    echo "[$(date '+%H:%M:%S')] $*" >> "${LOG_DIR}/orchestrator_${TIMESTAMP}.log"
}

# ── Progress Tracking ─────────────────────────────────────────────────────
init_progress() {
    if [[ ! -f "$PROGRESS_FILE" ]]; then
        echo '{}' > "$PROGRESS_FILE"
    fi
}

get_task_status() {
    local task_id="$1"
    jq -r --arg id "$task_id" '.[$id] // "pending"' "$PROGRESS_FILE"
}

set_task_status() {
    local task_id="$1"
    local status="$2"
    local tmp=$(mktemp)
    jq --arg id "$task_id" --arg status "$status" '.[$id] = $status' "$PROGRESS_FILE" > "$tmp"
    mv "$tmp" "$PROGRESS_FILE"
}

# ── Dependency Resolution ─────────────────────────────────────────────────
check_deps_met() {
    local task_id="$1"
    local deps
    deps=$(jq -r --arg id "$task_id" \
        '.tasks[] | select(.id == $id) | .depends_on[]' "$SPRINT_FILE" 2>/dev/null)

    if [[ -z "$deps" ]]; then
        return 0  # no deps
    fi

    while IFS= read -r dep; do
        local dep_status
        dep_status=$(get_task_status "$dep")
        if [[ "$dep_status" != "completed" ]]; then
            return 1  # dep not met
        fi
    done <<< "$deps"
    return 0
}

# ── Task Execution ────────────────────────────────────────────────────────
run_task() {
    local task_id="$1"
    local task_json
    task_json=$(jq -c --arg id "$task_id" '.tasks[] | select(.id == $id)' "$SPRINT_FILE")

    local title agent prompt done_when
    title=$(echo "$task_json" | jq -r '.title')
    agent=$(echo "$task_json" | jq -r '.agent')
    prompt=$(echo "$task_json" | jq -r '.prompt')
    done_when=$(echo "$task_json" | jq -r '.done_when')

    local agent_file="${AGENTS_DIR}/${agent}.md"
    if [[ ! -f "$agent_file" ]]; then
        log "WARNING: Agent file not found: $agent_file — using default"
        agent_file=""
    fi

    local task_log="${LOG_DIR}/${task_id}_${TIMESTAMP}.log"

    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log "TASK: ${task_id} — ${title}"
    log "AGENT: ${agent}"
    log "LOG: ${task_log}"
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    if [[ "$DRY_RUN" == "true" ]]; then
        log "DRY RUN — would execute task ${task_id} with agent ${agent}"
        set_task_status "$task_id" "completed"
        return 0
    fi

    set_task_status "$task_id" "in_progress"

    # Build the full prompt with agent persona and context
    local full_prompt
    local agent_persona=""
    if [[ -n "$agent_file" ]]; then
        agent_persona=$(cat "$agent_file")
    fi

    full_prompt="$(cat <<PROMPT_EOF
${agent_persona}

---

# Current Task: ${task_id} — ${title}

## Instructions

${prompt}

## Done When

${done_when}

## Context

- Working directory: ${PROJECT_ROOT}
- Read SNAPP-TRD.md for full specifications
- Read .claude/CLAUDE.md for project-wide rules
- All work must compile and pass tests before you finish
- Commit your work with a descriptive message when done

## Important

- Do NOT ask for permission or confirmation — just do the work
- If you encounter an error, diagnose and fix it
- If a dependency is missing, document it in a BLOCKERS.md file and move on
- When finished, create a git commit with message: "[${task_id}] ${title}"
PROMPT_EOF
)"

    # Execute Claude Code in print mode (non-interactive, autonomous)
    local exit_code=0
    claude \
        --print \
        --dangerously-skip-permissions \
        --model opus \
        --verbose \
        --max-budget-usd 5.00 \
        "$full_prompt" \
        > "$task_log" 2>&1 || exit_code=$?

    if [[ $exit_code -eq 0 ]]; then
        log "COMPLETED: ${task_id}"
        set_task_status "$task_id" "completed"
    else
        log "FAILED: ${task_id} (exit code: ${exit_code})"
        set_task_status "$task_id" "failed"
    fi

    return $exit_code
}

# ── Report Generation ─────────────────────────────────────────────────────
generate_report() {
    local sprint_name
    sprint_name=$(jq -r '.sprint' "$SPRINT_FILE")
    local sprint_desc
    sprint_desc=$(jq -r '.description' "$SPRINT_FILE")
    local total_tasks
    total_tasks=$(jq '.tasks | length' "$SPRINT_FILE")

    local completed=0 failed=0 pending=0 skipped=0
    while IFS= read -r task_id; do
        local status
        status=$(get_task_status "$task_id")
        case "$status" in
            completed) ((completed++)) ;;
            failed) ((failed++)) ;;
            skipped) ((skipped++)) ;;
            *) ((pending++)) ;;
        esac
    done < <(jq -r '.tasks[].id' "$SPRINT_FILE")

    cat > "$REPORT_FILE" <<REPORT_EOF
# SNAPP Build Report — ${sprint_name}
**Generated**: $(date '+%Y-%m-%d %H:%M:%S')
**Sprint**: ${sprint_name} — ${sprint_desc}

## Summary

| Status | Count |
|--------|-------|
| Completed | ${completed} |
| Failed | ${failed} |
| Pending | ${pending} |
| Skipped | ${skipped} |
| **Total** | **${total_tasks}** |

## Task Details

REPORT_EOF

    while IFS= read -r task_id; do
        local status title agent
        status=$(get_task_status "$task_id")
        title=$(jq -r --arg id "$task_id" '.tasks[] | select(.id == $id) | .title' "$SPRINT_FILE")
        agent=$(jq -r --arg id "$task_id" '.tasks[] | select(.id == $id) | .agent' "$SPRINT_FILE")

        local status_icon
        case "$status" in
            completed) status_icon="+" ;;
            failed) status_icon="x" ;;
            skipped) status_icon="-" ;;
            *) status_icon="?" ;;
        esac

        echo "- [${status_icon}] **${task_id}** (${agent}): ${title} — ${status}" >> "$REPORT_FILE"

        # Append failure details if failed
        if [[ "$status" == "failed" ]]; then
            local task_log="${LOG_DIR}/${task_id}_${TIMESTAMP}.log"
            if [[ -f "$task_log" ]]; then
                echo "" >> "$REPORT_FILE"
                echo "  Last 20 lines of log:" >> "$REPORT_FILE"
                echo '  ```' >> "$REPORT_FILE"
                tail -20 "$task_log" | sed 's/^/  /' >> "$REPORT_FILE"
                echo '  ```' >> "$REPORT_FILE"
            fi
        fi
    done < <(jq -r '.tasks[].id' "$SPRINT_FILE")

    echo "" >> "$REPORT_FILE"
    echo "## Log Files" >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"
    echo "- Orchestrator: \`${LOG_DIR}/orchestrator_${TIMESTAMP}.log\`" >> "$REPORT_FILE"
    for f in "${LOG_DIR}"/*_${TIMESTAMP}.log; do
        [[ -f "$f" ]] || continue
        echo "- $(basename "$f"): \`$f\`" >> "$REPORT_FILE"
    done

    log "Report written to: $REPORT_FILE"
}

# ── Main Loop ─────────────────────────────────────────────────────────────
main() {
    check_prereqs
    init_progress

    local sprint_name
    sprint_name=$(jq -r '.sprint' "$SPRINT_FILE")
    local sprint_desc
    sprint_desc=$(jq -r '.description' "$SPRINT_FILE")
    local total_tasks
    total_tasks=$(jq '.tasks | length' "$SPRINT_FILE")

    log "╔══════════════════════════════════════════════════════════╗"
    log "║  SNAPP Agentic Build Orchestrator                       ║"
    log "║  Sprint: ${sprint_name} — ${sprint_desc}"
    log "║  Tasks: ${total_tasks}"
    log "║  Mode: $(if $DRY_RUN; then echo 'DRY RUN'; else echo 'LIVE'; fi)"
    if [[ -n "$RESUME_FROM" ]]; then
        log "║  Resuming from: ${RESUME_FROM}"
    fi
    log "╚══════════════════════════════════════════════════════════╝"

    # Ensure git repo exists
    if ! git -C "$PROJECT_ROOT" rev-parse --git-dir >/dev/null 2>&1; then
        log "Initializing git repository..."
        git -C "$PROJECT_ROOT" init
        git -C "$PROJECT_ROOT" add -A
        git -C "$PROJECT_ROOT" commit -m "Initial commit — SNAPP project scaffolding"
    fi

    local skipping=true
    if [[ -z "$RESUME_FROM" ]]; then
        skipping=false
    fi

    # Topological sort: process tasks in order, checking deps
    local max_passes=10
    local pass=0
    local all_done=false

    while [[ "$all_done" == "false" && $pass -lt $max_passes ]]; do
        all_done=true
        ((pass++))
        log "── Pass ${pass} ──"

        while IFS= read -r task_id; do
            local status
            status=$(get_task_status "$task_id")

            # Handle --resume flag
            if [[ "$skipping" == "true" ]]; then
                if [[ "$task_id" == "$RESUME_FROM" ]]; then
                    skipping=false
                else
                    if [[ "$status" != "completed" ]]; then
                        set_task_status "$task_id" "skipped"
                    fi
                    continue
                fi
            fi

            # Skip already completed/failed tasks
            if [[ "$status" == "completed" || "$status" == "failed" ]]; then
                continue
            fi

            # Check dependencies
            if ! check_deps_met "$task_id"; then
                log "WAITING: ${task_id} — dependencies not met"
                all_done=false
                continue
            fi

            # Execute the task
            local task_exit=0
            run_task "$task_id" || task_exit=$?

            if [[ $task_exit -ne 0 ]]; then
                log "Task ${task_id} failed. Continuing with independent tasks..."
                # Don't stop — try other tasks whose deps are met
            fi

            all_done=false  # we did something, so loop again
        done < <(jq -r '.tasks[].id' "$SPRINT_FILE")
    done

    # Check for tasks blocked by failures
    while IFS= read -r task_id; do
        local status
        status=$(get_task_status "$task_id")
        if [[ "$status" == "pending" || "$status" == "in_progress" ]]; then
            log "BLOCKED: ${task_id} — dependencies failed or unresolvable"
            set_task_status "$task_id" "blocked"
        fi
    done < <(jq -r '.tasks[].id' "$SPRINT_FILE")

    generate_report

    # ── Vera: Trust-But-Verify Pass ───────────────────────────────────────
    # After all sprint tasks complete, Vera verifies the entire system.
    # Vera only runs if there were no task failures (no point verifying
    # a broken sprint) and if --dry-run is not set.
    local any_failed=false
    while IFS= read -r task_id; do
        local s; s=$(get_task_status "$task_id")
        [[ "$s" == "failed" || "$s" == "blocked" ]] && any_failed=true
    done < <(jq -r '.tasks[].id' "$SPRINT_FILE")

    if [[ "$any_failed" == "false" && "$DRY_RUN" == "false" ]]; then
        local vera_file="${AGENTS_DIR}/vera.md"
        if [[ -f "$vera_file" ]]; then
            local sprint_name
            sprint_name=$(jq -r '.sprint' "$SPRINT_FILE")
            local vera_log="${LOG_DIR}/vera_${sprint_name}_${TIMESTAMP}.log"

            log ""
            log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            log "VERA: Trust-But-Verify pass for Sprint ${sprint_name}"
            log "LOG: ${vera_log}"
            log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

            local vera_persona
            vera_persona=$(cat "$vera_file")

            local vera_prompt
            vera_prompt="$(cat <<VERA_EOF
${vera_persona}

---

# Current Task: Verify Sprint ${sprint_name}

You are running the post-sprint verification for Sprint ${sprint_name}.
Follow your checklist exactly. Do not skip steps. Do not trust prior reports.

Working directory: ${PROJECT_ROOT}

When finished, create a git commit with message: "[VERA] Sprint ${sprint_name} verification"
VERA_EOF
)"
            local vera_exit=0
            claude \
                --print \
                --dangerously-skip-permissions \
                --model "${CLAUDE_MODEL:-opus}" \
                --verbose \
                --max-budget-usd "${CLAUDE_BUDGET:-5.00}" \
                "$vera_prompt" \
                > "$vera_log" 2>&1 || vera_exit=$?

            if [[ $vera_exit -eq 0 ]]; then
                log "VERA: PASSED — Sprint ${sprint_name} verified"
            else
                log "VERA: ISSUES FOUND — check ${vera_log}"
            fi
        fi
    else
        log "VERA: Skipped — sprint has failures, nothing to verify"
    fi

    log ""
    log "═══════════════════════════════════════════════"
    log "  BUILD COMPLETE"
    log "  Report: ${REPORT_FILE}"
    log "═══════════════════════════════════════════════"
}

main
