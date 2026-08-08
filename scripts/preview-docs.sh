#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stop_file="${AXIAL_DOCS_PREVIEW_STOP_FILE:-/tmp/axial-docs-preview.stop}"
preview_host="${AXIAL_DOCS_PREVIEW_HOST:-0.0.0.0}"
preview_port="${AXIAL_DOCS_PREVIEW_PORT:-5000}"
preview_pid=""

case "${1:-}" in
  ""|--force-generate) ;;
  --no-generate) echo "FsLiveDocs watch owns generation; --no-generate is no longer supported." >&2; exit 2 ;;
  *) echo "Usage: $0 [--force-generate]" >&2; exit 2 ;;
esac

if [[ ! "$preview_port" =~ ^[0-9]+$ ]] || (( preview_port < 1 || preview_port > 65535 )); then
  echo "AXIAL_DOCS_PREVIEW_PORT must be an integer between 1 and 65535." >&2
  exit 2
fi

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

bash "$root_dir/scripts/run-livedocs.sh" watch --host "$preview_host" --port "$preview_port" &
preview_pid=$!

echo "FsLiveDocs preview binding to http://$preview_host:$preview_port/"
if [ "$preview_host" = "0.0.0.0" ]; then
  echo "Browse locally at http://localhost:$preview_port/ or use this computer's LAN address from your phone."
fi
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
