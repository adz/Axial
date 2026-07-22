#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
product="${1:-}"

case "$product" in
  validation|schema|flow) ;;
  *) echo "Usage: $0 <validation|schema|flow>" >&2; exit 2 ;;
esac

HUGO_BASEURL="${HUGO_BASEURL:-http://localhost:3000/}"
validate_dir="${AXIAL_DOCS_VALIDATE_DIR:-$root_dir/.fsdocs/validate-$product}"

if [[ "$product" != "validation" ]]; then
  "$root_dir/scripts/generate-example-docs.sh" "$product"
fi
bash "$root_dir/scripts/generate-api-docs.sh" "$product"
bash "$root_dir/scripts/populate-hugo-content.sh"

hugo --source "$root_dir/site" --destination "$validate_dir" --baseURL "$HUGO_BASEURL" --cleanDestinationDir

case "$product" in
  validation)
    test -f "$validate_dir/validation/getting-started/index.html"
    test -f "$validate_dir/validation/diagnostics/index.html"
    test -f "$validate_dir/validation/reference/check/t-errorhandling-check/index.html"
    ;;
  schema)
    test -f "$validate_dir/schema/getting-started/index.html"
    test -f "$validate_dir/schema/data/index.html"
    test -f "$validate_dir/schema/reference/schema/t-schema-schema/index.html"
    ;;
  flow)
    test -f "$validate_dir/flow/getting-started/index.html"
    test -f "$validate_dir/flow/reference/flow/t-flow-flow/index.html"
    test -f "$validate_dir/flow/reference/hosting-node/index.html"
    test -f "$validate_dir/flow/reference/hosting-browser/index.html"
    ;;
esac

echo "$product docs validation build written to $validate_dir"
