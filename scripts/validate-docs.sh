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
rg -Fq "el.style.display = el.getAttribute('data-theme-variant') === theme ? 'block' : 'none'" "$root_dir/output/index.html"
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
rg -q 'href="api/Axial.State.html"' "$root_dir/output/api.html"
rg -q 'href="api/Axial.State.STM`1.html"' "$root_dir/output/api.html"
rg -Fq 'href="Axial.Exit`2.html">Exit</a>' "$root_dir/output/api/Axial.Deferred`2.html"
if rg -n 'href="/reference/' "$root_dir/output/api" --glob '*.html'; then
  echo "Legacy FSharp.Formatting reference links remain in generated API pages." >&2
  exit 1
fi
if rg -q 'Axial\.(State\.)?(ITRef|TJournal|TransactionResult|TContext)' "$root_dir/output/api.html"; then
  echo "Internal STM engine types leaked into the API reference." >&2
  exit 1
fi

if rg -q '<details class="group"[^>]*open' "$root_dir/output/api.html"; then
  echo "Sidebar sections must be initially closed." >&2
  exit 1
fi

rg -q 'data-docs-group="dependencies/processes"' "$root_dir/output/dependencies/index.html"
rg -q 'data-docs-group="dependencies/tutorials"' "$root_dir/output/dependencies/index.html"
rg -q 'data-docs-group="observability/telemetry"' "$root_dir/output/observability/index.html"
rg -q "currentSidebarLink.setAttribute('aria-current', 'page')" "$root_dir/output/dependencies/processes/composition.html"
rg -Fq '#sidebar-root [data-sidebar-item="true"] a[href]' "$root_dir/output/dependencies/processes/composition.html"
rg -q 'href="composition.html"' "$root_dir/output/dependencies/processes/index.html"

while IFS= read -r source_page; do
  page_name="$(basename "$source_page")"
  if [[ ! "$page_name" =~ ^[0-9][0-9]- ]]; then
    echo "Documentation page lacks a numeric ordering prefix: ${source_page#"$root_dir/"}" >&2
    exit 1
  fi
done < <(find "$root_dir"/docs/[0-9][0-9]-* -mindepth 1 -type f -name '*.md' ! -name '_index.md' | sort)

while IFS= read -r source_folder; do
  folder_name="$(basename "$source_folder")"
  if [[ ! "$folder_name" =~ ^[0-9][0-9]- ]]; then
    echo "Nested documentation folder lacks a numeric ordering prefix: ${source_folder#"$root_dir/"}" >&2
    exit 1
  fi
done < <(find "$root_dir"/docs/[0-9][0-9]-* -mindepth 1 -type d | sort)

if rg -n '^weight:' "$root_dir/docs" --glob '*.md'; then
  echo "Documentation ordering must use numeric file and folder prefixes, not frontmatter weights." >&2
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
