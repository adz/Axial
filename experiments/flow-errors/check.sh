#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cp -R "$root/experiments/flow-errors/golden/baseline" "$tmp/expected"
"$root/experiments/flow-errors/capture.sh" baseline
diff -ru "$tmp/expected" "$root/experiments/flow-errors/golden/baseline"
echo "Flow error corpus matches baseline golden files."
