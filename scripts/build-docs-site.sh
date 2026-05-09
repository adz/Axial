#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --cleanDestinationDir
