#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
port="${AXIAL_DOCS_PREVIEW_PORT:-3000}"
HUGO_BASEURL="${HUGO_BASEURL:-http://192.168.86.180:$port/}"
stop_file="${AXIAL_DOCS_PREVIEW_STOP_FILE:-/tmp/axial-docs-preview.stop}"
hugo_pid=""
generate=true
force_generate=false
cache_dir="$root_dir/.fsdocs/cache"
generation_stamp="$cache_dir/preview-generation.sha256"

case "${1:-}" in
  "") ;;
  --no-generate) generate=false ;;
  --force-generate) force_generate=true ;;
  *) echo "Usage: $0 [--no-generate|--force-generate]" >&2; exit 2 ;;
esac

generation_fingerprint() {
  {
    find \
      "$root_dir/src" \
      "$root_dir/examples/Axial.Examples" \
      "$root_dir/examples/Axial.Playground" \
      "$root_dir/examples/Axial.MaintenanceExamples" \
      "$root_dir/scripts/docgen" \
      -type f \
      \( -name "*.fs" -o -name "*.fsproj" -o -name "*.props" -o -name "*.targets" -o -name "*.json" \) \
      -print0
    for input in \
      "$root_dir/Directory.Build.props" \
      "$root_dir/Directory.Packages.props" \
      "$root_dir/global.json" \
      "$root_dir/scripts/docs-build.proj" \
      "$root_dir/scripts/generate-example-docs.sh" \
      "$root_dir/scripts/generate-api-docs.sh"
    do
      if [ -f "$input" ]; then
        printf '%s\0' "$input"
      fi
    done
  } |
    sort -z |
    xargs -0 sha256sum |
    sha256sum |
    cut -d' ' -f1
}

if $generate; then
  fingerprint="$(generation_fingerprint)"
  cached_fingerprint=""
  if [ -f "$generation_stamp" ]; then
    cached_fingerprint="$(<"$generation_stamp")"
  fi

  if ! $force_generate &&
     [ "$fingerprint" = "$cached_fingerprint" ] &&
     [ -f "$root_dir/docs/flow/examples.md" ] &&
     [ -d "$root_dir/docs/flow/reference" ]; then
    echo "Docs generator inputs unchanged; reusing cached generated docs."
  else
    dotnet msbuild "$root_dir/scripts/docs-build.proj" \
      -t:Build -m -nologo -verbosity:minimal -p:DocsBuildScope=All

    "$root_dir/scripts/generate-example-docs.sh" flow --no-build &
    examples_pid=$!
    bash "$root_dir/scripts/generate-api-docs.sh" flow --no-build &
    api_pid=$!

    generation_status=0
    wait "$examples_pid" || generation_status=$?
    wait "$api_pid" || generation_status=$?
    if [ "$generation_status" -ne 0 ]; then
      exit "$generation_status"
    fi

    mkdir -p "$cache_dir"
    printf '%s\n' "$fingerprint" > "$generation_stamp"
  fi
fi

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
