#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
port="${AXIAL_DOCS_PREVIEW_PORT:-3000}"
HUGO_BASEURL="${HUGO_BASEURL:-http://192.168.86.180:$port/}"
stop_file="${AXIAL_DOCS_PREVIEW_STOP_FILE:-/tmp/axial-docs-preview.stop}"
hugo_pid=""

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

rm -f "$stop_file"

cleanup() {
  trap - EXIT HUP INT TERM

  if [ -n "$hugo_pid" ] && kill -0 "$hugo_pid" 2>/dev/null; then
    kill "$hugo_pid" 2>/dev/null || true
    wait "$hugo_pid" 2>/dev/null || true
  fi
}

trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

hugo server --source "$root_dir/site" --bind 0.0.0.0 --port "$port" --baseURL "$HUGO_BASEURL" &
hugo_pid=$!

echo "Hugo preview starting at $HUGO_BASEURL"
echo "Stop by touching $stop_file or sending SIGHUP, TERM, or INT to this script."

while kill -0 "$hugo_pid" 2>/dev/null; do
  if [ -e "$stop_file" ]; then
    echo "Stop file detected: $stop_file"
    rm -f "$stop_file"
    exit 0
  fi

  sleep 1
done

set +e
wait "$hugo_pid"
hugo_status=$?
set -e
hugo_pid=""
exit "$hugo_status"
