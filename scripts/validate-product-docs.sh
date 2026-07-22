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
    test -f "$validate_dir/error-handling/getting-started/index.html"
    test -f "$validate_dir/error-handling/diagnostics/index.html"
    test -f "$validate_dir/error-handling/reference/check/t-errorhandling-check/index.html"
    duplicate_sidebar_ids="$(grep -o 'id="[^"]*"' "$validate_dir/error-handling/reference/result/index.html" | sort | uniq -d)"
    test -z "$duplicate_sidebar_ids"
    ;;
  schema)
    test -f "$validate_dir/schema/getting-started/index.html"
    test -f "$validate_dir/schema/data/index.html"
    test -f "$validate_dir/schema/reference/schema/t-schema-schema/index.html"
    grep -q 'id="package-schema-reference-check" checked' "$validate_dir/schema/reference/schema/index.html"
    grep -q 'id="package-schemadata-reference-check" checked' "$validate_dir/schema/reference/data/index.html"
    grep -q 'id="package-schemajson-codec-reference-check" checked' "$validate_dir/schema/reference/codec/index.html"
    grep -q 'id="package-schemahttp-servers-reference-check" checked' "$validate_dir/schema/reference/schema/http/index.html"
    ;;
  flow)
    test -f "$validate_dir/flow/getting-started/index.html"
    test -f "$validate_dir/flow/reference/flow/t-flow-flow/index.html"
    test -f "$validate_dir/flow/reference/hosting-node/index.html"
    test -f "$validate_dir/flow/reference/hosting-browser/index.html"
    ;;
esac

echo "$product docs validation build written to $validate_dir"
