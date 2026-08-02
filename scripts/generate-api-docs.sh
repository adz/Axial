#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
product="all"
skip_build=false

for arg in "$@"; do
  case "$arg" in
    data|validation|schema|flow|all) product="$arg" ;;
    --no-build) skip_build=true ;;
    *) echo "Usage: $0 [data|validation|schema|flow|all] [--no-build]" >&2; exit 2 ;;
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

case "$product" in
  data)
    run_docgen "$product"
    ;;
  validation)
    run_docgen "$product"
    ;;
  schema)
    run_docgen "$product"
    ;;
  flow)
    run_docgen "$product"
    ;;
  all)
    run_docgen data &
    data_pid=$!
    run_docgen validation &
    validation_pid=$!
    run_docgen schema &
    schema_pid=$!
    run_docgen flow &
    flow_pid=$!

    generation_status=0
    wait "$data_pid" || generation_status=$?
    wait "$validation_pid" || generation_status=$?
    wait "$schema_pid" || generation_status=$?
    wait "$flow_pid" || generation_status=$?
    exit "$generation_status"
    ;;
  *)
    echo "Usage: $0 [data|validation|schema|flow|all] [--no-build]" >&2
    exit 2
    ;;
esac
