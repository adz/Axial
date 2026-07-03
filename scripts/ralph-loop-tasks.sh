#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TASKS_FILE="${TASKS_FILE:-$ROOT_DIR/dev-docs/TASKS.md}"
CODEX_BIN="${CODEX_BIN:-codex}"

usage() {
  cat <<EOF
Usage: $(basename "$0") [--once] [-- <extra codex exec args>]

Runs Codex in a loop against the next unchecked item in dev-docs/TASKS.md.
After each Codex run, the script verifies that:
  1. the task was checked off in dev-docs/TASKS.md
  2. a new git commit was created
  3. the working tree is clean before continuing

Environment:
  TASKS_FILE  Override the path to TASKS.md
  CODEX_BIN   Override the codex executable
Examples:
  $(basename "$0")
  $(basename "$0") --once -- -m gpt-5.4
EOF
}

require_clean_tree() {
  if ! git -C "$ROOT_DIR" diff --quiet || ! git -C "$ROOT_DIR" diff --cached --quiet; then
    echo "Working tree has uncommitted changes. Commit or stash them before starting the loop." >&2
    exit 1
  fi
}

next_task() {
  awk '
    match($0, /^[[:space:]]*([0-9]+)\. \[ \] (.*)$/, m) {
      print NR "\tnumbered\t" m[1] "\t" m[2]
      exit
    }
    match($0, /^[[:space:]]*[-*+] \[ \] (.*)$/, m) {
      print NR "\tbullet\t-\t" m[1]
      exit
    }
  ' "$TASKS_FILE"
}

task_is_checked() {
  local line_number="$1"
  local task_kind="$2"
  local task_marker="$3"
  local task_text="$4"

  awk \
    -v line_number="$line_number" \
    -v task_kind="$task_kind" \
    -v task_marker="$task_marker" \
    -v task_text="$task_text" '
      NR == line_number {
        if (task_kind == "numbered" && $0 ~ "^[[:space:]]*" task_marker "\\. \\[x\\] " && index($0, "[x] " task_text) > 0) {
          found = 1
        }
        if (task_kind == "bullet" && $0 ~ "^[[:space:]]*[-*+] \\[x\\] " && index($0, "[x] " task_text) > 0) {
          found = 1
        }
      }
      END {
        exit(found ? 0 : 1)
      }
    ' "$TASKS_FILE"
}

count_remaining_tasks() {
  grep -Ec '^[[:space:]]*([0-9]+\.|[-*+]) \[ \] ' "$TASKS_FILE" || true
}

run_task() {
  local task_line_number="$1"
  local task_kind="$2"
  local task_marker="$3"
  local task_text="$4"
  local before_head
  local after_head
  local prompt
  local task_ref
  local task_line

  before_head="$(git -C "$ROOT_DIR" rev-parse --verify HEAD 2>/dev/null || true)"

  if [[ "$task_kind" == "numbered" ]]; then
    task_ref="$task_marker"
    task_line="${task_marker}. [ ] ${task_text}"
  else
    task_ref="line ${task_line_number}"
    task_line="- [ ] ${task_text}"
  fi

  printf 'Running task %s: %s\n' "$task_ref" "$task_text"

  prompt=$(cat <<EOF
Work only on the unchecked task at ${task_ref} from dev-docs/TASKS.md in this repository:

${task_line}

Repository rules to follow:
- Read and follow AGENTS.md, dev-docs/PLAN.md, and dev-docs/TASKS.md.
- Complete this task end-to-end and do not start any later tasks.
- Keep the repository's Axial architecture direction intact.
- Update dev-docs/TASKS.md to mark this task complete only if it is actually complete.
- After completing this task, create exactly one git commit for it before stopping.
- Leave the working tree clean when you finish.
- Run the relevant build/tests needed to validate the task.

If you cannot complete the task safely, do not mark it complete and do not create a misleading commit. Instead, stop and explain the blocker clearly.
EOF
)

  "$CODEX_BIN" exec "${CODEX_EXEC_ARGS[@]}" -C "$ROOT_DIR" "$prompt"

  if ! git -C "$ROOT_DIR" diff --quiet || ! git -C "$ROOT_DIR" diff --cached --quiet; then
    echo "Codex left uncommitted changes after task ${task_ref}. Stopping." >&2
    exit 1
  fi

  after_head="$(git -C "$ROOT_DIR" rev-parse --verify HEAD 2>/dev/null || true)"

  if [[ "$after_head" == "$before_head" ]]; then
    echo "No new commit was created for task ${task_ref}. Stopping." >&2
    exit 1
  fi

  if ! task_is_checked "$task_line_number" "$task_kind" "$task_marker" "$task_text"; then
    echo "Task ${task_ref} is still unchecked in TASKS.md. Stopping." >&2
    exit 1
  fi

  printf 'Completed task %s with commit %s\n' "$task_ref" "$after_head"
}

main() {
  local once="false"
  local task_line
  local task_line_number
  local task_kind
  local task_marker
  local task_text
  CODEX_EXEC_ARGS=(--full-auto)

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --once)
        once="true"
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      --)
        shift
        if [[ $# -gt 0 ]]; then
          CODEX_EXEC_ARGS=("$@")
        fi
        break
        ;;
      *)
        usage
        exit 1
        ;;
    esac
  done

  require_clean_tree

  while true; do
    task_line="$(next_task || true)"

    if [[ -z "$task_line" ]]; then
      echo "No unchecked tasks remain in $(basename "$TASKS_FILE")."
      exit 0
    fi

    IFS=$'\t' read -r task_line_number task_kind task_marker task_text <<<"$task_line"
    run_task "$task_line_number" "$task_kind" "$task_marker" "$task_text"

    if [[ "$once" == "true" ]]; then
      exit 0
    fi

    printf 'Tasks remaining: %s\n' "$(count_remaining_tasks)"
  done
}

main "$@"
