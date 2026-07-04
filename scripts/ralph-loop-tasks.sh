#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TASKS_FILE="${TASKS_FILE:-$ROOT_DIR/dev-docs/TASKS.md}"
CODEX_BIN="${CODEX_BIN:-codex}"
CLAUDE_BIN="${CLAUDE_BIN:-claude}"
ENGINE="${ENGINE:-}"

usage() {
  cat <<EOF
Usage: $(basename "$0") [--once] [--engine codex|claude] [-- <extra engine args>]

Runs Codex or Claude in a loop against the next unchecked item in dev-docs/TASKS.md.
If --engine is not given, prompts on startup to choose the engine.
After each run, the script verifies the task was checked off in dev-docs/TASKS.md,
commits any remaining changes itself (the engine is not required to commit), and
confirms the working tree is clean and a new commit exists before continuing.

Environment:
  TASKS_FILE  Override the path to TASKS.md
  CODEX_BIN   Override the codex executable
  CLAUDE_BIN  Override the claude executable
  ENGINE      Preselect the engine (codex|claude), skipping the prompt
Examples:
  $(basename "$0")
  $(basename "$0") --engine claude
  $(basename "$0") --once --engine codex -- -m gpt-5.4
EOF
}

prompt_engine() {
  local choice
  while true; do
    read -r -p "Which engine do you want to use? [codex/claude]: " choice
    case "$choice" in
      codex|Codex|CODEX)
        ENGINE="codex"
        return
        ;;
      claude|Claude|CLAUDE)
        ENGINE="claude"
        return
        ;;
      *)
        echo "Please answer 'codex' or 'claude'."
        ;;
    esac
  done
}

require_clean_tree() {
  if ! git -C "$ROOT_DIR" diff --quiet || ! git -C "$ROOT_DIR" diff --cached --quiet; then
    echo "Working tree has uncommitted changes. Commit or stash them before starting the loop." >&2
    exit 1
  fi
}

next_task() {
  awk '
    /^[[:space:]]*([0-9]+\.|[-*+]) \[[ xX]\] / {
      task_number += 1
    }
    match($0, /^[[:space:]]*([0-9]+)\. \[ \] (.*)$/, m) {
      print NR "\t" task_number "\tnumbered\t" m[1] "\t" m[2]
      exit
    }
    match($0, /^[[:space:]]*[-*+] \[ \] (.*)$/, m) {
      print NR "\t" task_number "\tbullet\t-\t" m[1]
      exit
    }
  ' "$TASKS_FILE"
}

task_is_checked() {
  local task_number="$1"

  awk \
    -v task_number="$task_number" '
      /^[[:space:]]*([0-9]+\.|[-*+]) \[[ xX]\] / {
        seen += 1
        if (seen == task_number && $0 ~ /^[[:space:]]*([0-9]+\.|[-*+]) \[[xX]\] /) {
          checked = 1
          exit
        }
      }
      END {
        exit(checked ? 0 : 1)
      }
    ' "$TASKS_FILE"
}

count_remaining_tasks() {
  grep -Ec '^[[:space:]]*([0-9]+\.|[-*+]) \[ \] ' "$TASKS_FILE" || true
}

run_task() {
  local task_line_number="$1"
  local task_number="$2"
  local task_kind="$3"
  local task_marker="$4"
  local task_text="$5"
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
    task_ref="item ${task_number} (line ${task_line_number})"
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
- Do not run git commit yourself. Leave your changes uncommitted; the wrapper script commits
  them automatically once it verifies the task is checked off.
- Run the relevant build/tests needed to validate the task.

If you cannot complete the task safely, do not mark it complete and do not create a misleading commit. Instead, stop and explain the blocker clearly.
EOF
)

  case "$ENGINE" in
    codex)
      "$CODEX_BIN" exec "${ENGINE_EXEC_ARGS[@]}" -C "$ROOT_DIR" "$prompt"
      ;;
    claude)
      (cd "$ROOT_DIR" && "$CLAUDE_BIN" -p "${ENGINE_EXEC_ARGS[@]}" "$prompt")
      ;;
    *)
      echo "Unknown engine: $ENGINE" >&2
      exit 1
      ;;
  esac

  if ! task_is_checked "$task_number"; then
    echo "Task ${task_ref} was not marked checked in TASKS.md. Stopping." >&2
    exit 1
  fi

  git -C "$ROOT_DIR" add -A

  if ! git -C "$ROOT_DIR" diff --cached --quiet; then
    git -C "$ROOT_DIR" commit -F - <<EOF
Complete task ${task_ref}: ${task_text}
EOF
  fi

  if ! git -C "$ROOT_DIR" diff --quiet || ! git -C "$ROOT_DIR" diff --cached --quiet; then
    echo "${ENGINE^} left uncommitted changes after task ${task_ref}. Stopping." >&2
    exit 1
  fi

  after_head="$(git -C "$ROOT_DIR" rev-parse --verify HEAD 2>/dev/null || true)"

  if [[ "$after_head" == "$before_head" ]]; then
    echo "No new commit was created for task ${task_ref}. Stopping." >&2
    exit 1
  fi

  printf 'Completed task %s with commit %s\n' "$task_ref" "$after_head"
}

main() {
  local once="false"
  local task_line
  local task_line_number
  local task_number
  local task_kind
  local task_marker
  local task_text
  local extra_args_set="false"
  ENGINE_EXEC_ARGS=()

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --once)
        once="true"
        shift
        ;;
      --engine)
        ENGINE="${2:-}"
        shift 2
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      --)
        shift
        if [[ $# -gt 0 ]]; then
          ENGINE_EXEC_ARGS=("$@")
          extra_args_set="true"
        fi
        break
        ;;
      *)
        usage
        exit 1
        ;;
    esac
  done

  case "$ENGINE" in
    codex|claude) ;;
    "") prompt_engine ;;
    *)
      echo "Unknown engine: $ENGINE (expected 'codex' or 'claude')" >&2
      exit 1
      ;;
  esac

  if [[ "$extra_args_set" == "false" ]]; then
    if [[ "$ENGINE" == "codex" ]]; then
      ENGINE_EXEC_ARGS=(--full-auto)
    else
      ENGINE_EXEC_ARGS=(--permission-mode bypassPermissions)
    fi
  fi

  require_clean_tree

  while true; do
    task_line="$(next_task || true)"

    if [[ -z "$task_line" ]]; then
      echo "No unchecked tasks remain in $(basename "$TASKS_FILE")."
      exit 0
    fi

    IFS=$'\t' read -r task_line_number task_number task_kind task_marker task_text <<<"$task_line"
    run_task "$task_line_number" "$task_number" "$task_kind" "$task_marker" "$task_text"

    if [[ "$once" == "true" ]]; then
      exit 0
    fi

    printf 'Tasks remaining: %s\n' "$(count_remaining_tasks)"
  done
}

main "$@"
