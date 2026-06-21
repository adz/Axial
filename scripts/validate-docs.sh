#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-http://localhost:3000/}"
validate_dir="${AXIAL_DOCS_VALIDATE_DIR:-"$root_dir/.fsdocs/validate"}"

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

hugo --source "$root_dir/site" --destination "$validate_dir" --baseURL "$HUGO_BASEURL" --cleanDestinationDir

echo "Docs validation build written to $validate_dir"
