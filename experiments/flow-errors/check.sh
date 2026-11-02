#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cp -R "$root/experiments/flow-errors/golden/witness" "$tmp/expected"
"$root/experiments/flow-errors/capture.sh" witness
diff -ru "$tmp/expected" "$root/experiments/flow-errors/golden/witness"
echo "Flow error corpus matches shipped witness golden files."
