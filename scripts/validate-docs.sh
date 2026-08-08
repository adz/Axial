#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash "$root_dir/scripts/build-docs-site.sh"
bash "$root_dir/scripts/run-livedocs.sh" test

test -f "$root_dir/output/index.html"
test -f "$root_dir/output/getting-started/index.html"
test -f "$root_dir/output/api.html"
test -f "$root_dir/output/content/img/axial-inline-light.svg"

if rg -n '\{\{[<%]\s*(relref|ref)\b|/flow/' "$root_dir/docs"; then
  echo "Legacy Hugo links remain in docs/." >&2
  exit 1
fi

echo "FsLiveDocs validation build written to $root_dir/output"
