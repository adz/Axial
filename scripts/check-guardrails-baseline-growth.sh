#!/usr/bin/env bash

# Fails when the checked-in Guardrails baseline contains more violations than the comparison commit.
# Usage: scripts/check-guardrails-baseline-growth.sh [base-commit-or-ref]

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

if [[ $# -gt 0 ]]; then
    base=$1
elif [[ -n ${GITHUB_BASE_REF:-} ]] && git rev-parse --verify --quiet "origin/$GITHUB_BASE_REF" >/dev/null; then
    base="origin/$GITHUB_BASE_REF"
elif git rev-parse --verify --quiet HEAD^ >/dev/null; then
    base=HEAD^
else
    echo "Guardrails baseline growth: skipped (no base commit)"
    exit 0
fi

count_entries() {
    grep -cE '^AXG[0-9]{3}[[:space:]]' || true
}

current=$(count_entries < Axial.Guardrails.baseline)
if git cat-file -e "$base:Axial.Guardrails.baseline" 2>/dev/null; then
    previous=$(git show "$base:Axial.Guardrails.baseline" | count_entries)
else
    previous=0
fi

if (( current > previous )); then
    echo "Guardrails baseline grew from $previous to $current entries relative to $base." >&2
    echo "Fix new violations; the baseline may retain existing violations but must not grow." >&2
    exit 1
fi

echo "Guardrails baseline growth: pass ($current entries; base $previous at $base)"
