#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
printf 'version = 1\nTEST  tests/App.Tests/App.Tests.fsproj\t2\t99\n' > "$tmp/lock"
: > "$tmp/empty.log"
if python3 "$root/scripts/check-test-ratchet.py" "$tmp/lock" "$tmp/empty.log" 2>/dev/null; then
  echo 'zero parsed projects passed' >&2; exit 1
fi
printf 'Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1 - App.Tests.dll (net10.0)\n' > "$tmp/one.log"
if python3 "$root/scripts/check-test-ratchet.py" "$tmp/lock" "$tmp/one.log" 2>/dev/null; then
  echo 'deleted executed test was hidden by source syntax' >&2; exit 1
fi
printf 'Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2 - App.Tests.dll (net10.0)\n' > "$tmp/two.log"
python3 "$root/scripts/check-test-ratchet.py" "$tmp/lock" "$tmp/two.log"
