#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
product="flow"
skip_build=false

for arg in "$@"; do
  case "$arg" in
    flow) product="$arg" ;;
    --no-build) skip_build=true ;;
    *) echo "Usage: $0 [flow] [--no-build]" >&2; exit 2 ;;
  esac
done

if ! $skip_build; then
  dotnet msbuild "$root_dir/scripts/docs-build.proj" \
    -t:Build -m -nologo -verbosity:minimal -p:DocsBuildScope=Api
fi

run_docgen() {
  local selected_product="$1"
  (
    cd "$root_dir/scripts/docgen"
    AXIAL_DOCS_PRODUCT="$selected_product" \
      dotnet run --no-build --no-restore --nologo
  )
}

run_docgen flow
