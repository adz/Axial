#!/usr/bin/env bash
set -euo pipefail
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

cat >"$tmp" <<'LOG'
Passed!  - Failed:     0, Passed:    27, Skipped: 0, Total: 27
Failed!  - Failed:     2, Passed:     3, Skipped: 0, Total: 5
Test summary: total: 11, failed: 1, succeeded: 9, skipped: 1
LOG
[[ $(python3 "$root_dir/scripts/parse-test-summary.py" "$tmp") == "39 3 3" ]]
: >"$tmp"
[[ $(python3 "$root_dir/scripts/parse-test-summary.py" "$tmp") == "0 0 0" ]]
echo "verify summary parser: passed"
