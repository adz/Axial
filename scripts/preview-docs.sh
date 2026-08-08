#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stop_file="${AXIAL_DOCS_PREVIEW_STOP_FILE:-/tmp/axial-docs-preview.stop}"
preview_pid=""

case "${1:-}" in
  ""|--force-generate) ;;
  --no-generate) echo "FsLiveDocs watch owns generation; --no-generate is no longer supported." >&2; exit 2 ;;
  *) echo "Usage: $0 [--force-generate]" >&2; exit 2 ;;
esac

rm -f "$stop_file"

cleanup() {
  trap - EXIT HUP INT TERM
  if [ -n "$preview_pid" ] && kill -0 "$preview_pid" 2>/dev/null; then
    kill "$preview_pid" 2>/dev/null || true
    wait "$preview_pid" 2>/dev/null || true
  fi
}

trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

bash "$root_dir/scripts/run-livedocs.sh" watch &
preview_pid=$!

echo "FsLiveDocs preview starting at http://localhost:5000/"
echo "Stop by touching $stop_file or sending SIGHUP, TERM, or INT to this script."

while kill -0 "$preview_pid" 2>/dev/null; do
  if [ -e "$stop_file" ]; then
    echo "Stop file detected: $stop_file"
    rm -f "$stop_file"
    exit 0
  fi
  sleep 1
done

set +e
wait "$preview_pid"
preview_status=$?
set -e
preview_pid=""
exit "$preview_status"
