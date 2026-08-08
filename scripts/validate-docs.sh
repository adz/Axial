#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash "$root_dir/scripts/build-docs-site.sh"
bash "$root_dir/scripts/run-livedocs.sh" test

test -f "$root_dir/output/index.html"
test -f "$root_dir/output/getting-started/index.html"
test -f "$root_dir/output/api.html"
test -f "$root_dir/output/content/img/axial-inline-light.svg"
test -f "$root_dir/output/content/img/hero-lockup-light.png"
test -f "$root_dir/output/content/img/hero-lockup-dark.png"

rg -q 'src="content/img/hero-lockup-light.png"' "$root_dir/output/index.html"
rg -q 'src="content/img/hero-lockup-dark.png"' "$root_dir/output/index.html"
rg -q 'data-theme-variant="light"' "$root_dir/output/index.html"
rg -q 'data-theme-variant="dark"' "$root_dir/output/index.html"
rg -q 'applySiteTheme' "$root_dir/output/index.html"
rg -q 'href="content/axial-docs.css"' "$root_dir/output/index.html"
rg -q 'data-set-theme="light"' "$root_dir/output/index.html"
rg -q 'data-set-theme="dark"' "$root_dir/output/index.html"
if rg -q 'data-set-theme="(cupcake|dracula|emerald|corporate|retro|cyberpunk)"' "$root_dir/output/index.html"; then
  echo "Axial exposes an unsupported documentation theme." >&2
  exit 1
fi
rg -q 'href="api/Axial.Process.ProcessPlan.html"' "$root_dir/output/api.html"
rg -q 'href="api/Axial.Telemetry.FiberTelemetry.html"' "$root_dir/output/api.html"
rg -q 'href="api/Axial.Flow.html"' "$root_dir/output/api.html"
rg -q 'href="api/Axial.Flow`3.html"' "$root_dir/output/api.html"
rg -q 'href="api/Axial.Schedule`3.html"' "$root_dir/output/api.html"

if rg -q '<details class="group"[^>]*open' "$root_dir/output/api.html"; then
  echo "Sidebar sections must be initially closed." >&2
  exit 1
fi

api_index_entries="$(rg -o 'href="api/[^"]+\.html" class="card' "$root_dir/output/api.html" | wc -l)"
if (( api_index_entries < 160 )); then
  echo "API index contains only $api_index_entries entries; expected the complete Axial reference." >&2
  exit 1
fi

if rg -n '\{\{[<%]\s*(relref|ref)\b|/flow/' "$root_dir/docs"; then
  echo "Legacy Hugo links remain in docs/." >&2
  exit 1
fi

echo "FsLiveDocs validation build written to $root_dir/output"
