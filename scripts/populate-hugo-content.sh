#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

upsert_frontmatter() {
  local file="$1"
  local key="$2"
  local value="$3"
  local tmp

  if [ ! -f "$file" ]; then
    return 0
  fi

  tmp="$(mktemp)"
  awk -v key="$key" -v value="$value" '
    NR == 1 && $0 == "---" {
      in_frontmatter = 1
      print
      next
    }

    in_frontmatter && $0 == "---" {
      if (!seen) {
        print key ": " value
      }
      in_frontmatter = 0
      print
      next
    }

    in_frontmatter && $0 ~ "^" key ":" {
      print key ": " value
      seen = 1
      next
    }

    { print }
  ' "$file" > "$tmp"
  mv "$tmp" "$file"
}

# Axial has two product documentation areas: /schema/ and /flow/. Schema owns
# the Data and ErrorHandling package documentation while keeping their package
# boundaries visible. Generated API reference is distributed under the product
# that owns each package.
schema_dir="$root_dir/site/content/schema"
flow_dir="$root_dir/site/content/flow"
rm -rf "$root_dir/site/content/error-handling" "$root_dir/site/content/data" \
  "$schema_dir" "$flow_dir" \
  "$root_dir/site/content/docs" "$root_dir/site/content/reference" "$root_dir/site/content/parse"
mkdir -p "$schema_dir" "$flow_dir"

cp -r "$root_dir/docs/schema/." "$schema_dir/"
cp -r "$root_dir/docs/flow/." "$flow_dir/"
rm -rf "$schema_dir/reference-indexes" "$flow_dir/reference-indexes"

# Distribute the generated API reference (docs/reference/<group>) into the
# three areas, then apply weights and per-area index pages.
schema_ref="$schema_dir/reference"
flow_ref="$flow_dir/reference"

copy_ref_group() {
  local dest="$1"
  local group="$2"
  mkdir -p "$dest"
  cp -r "$root_dir/docs/reference/$group" "$dest/"
}

for group in check predicate result validation diagnostics refined; do
  copy_ref_group "$schema_ref/error-handling" "$group"
done
for group in schema codec data; do
  copy_ref_group "$schema_ref" "$group"
done
for group in app flow fiber exit cause concurrency schedule ref stm stream bind service layer scope hosting hosting-node hosting-browser; do
  copy_ref_group "$flow_ref" "$group"
done

cp "$root_dir/docs/schema/reference-indexes/error-handling.md" "$schema_ref/error-handling/_index.md"
cp "$root_dir/docs/schema/reference-indexes/schema.md" "$schema_ref/_index.md"
cp "$root_dir/docs/flow/reference-indexes/flow.md" "$flow_ref/_index.md"

upsert_frontmatter "$flow_ref/flow/_index.md" "weight" "10"
upsert_frontmatter "$flow_ref/flow/runtime/_index.md" "weight" "10"
upsert_frontmatter "$flow_ref/fiber/_index.md" "weight" "20"
upsert_frontmatter "$flow_ref/exit/_index.md" "weight" "30"
upsert_frontmatter "$flow_ref/cause/_index.md" "weight" "40"
upsert_frontmatter "$flow_ref/concurrency/_index.md" "weight" "50"
upsert_frontmatter "$flow_ref/schedule/_index.md" "weight" "60"
upsert_frontmatter "$flow_ref/ref/_index.md" "weight" "70"
upsert_frontmatter "$flow_ref/stm/_index.md" "weight" "80"
upsert_frontmatter "$flow_ref/stream/_index.md" "weight" "90"
upsert_frontmatter "$flow_ref/bind/_index.md" "weight" "100"
upsert_frontmatter "$flow_ref/service/_index.md" "weight" "110"
upsert_frontmatter "$flow_ref/layer/_index.md" "weight" "120"
upsert_frontmatter "$flow_ref/scope/_index.md" "weight" "130"
upsert_frontmatter "$flow_ref/service/core/_index.md" "weight" "10"
upsert_frontmatter "$flow_ref/service/console/_index.md" "weight" "20"
upsert_frontmatter "$flow_ref/service/filesystem/_index.md" "weight" "30"
upsert_frontmatter "$flow_ref/service/http/_index.md" "weight" "40"
upsert_frontmatter "$flow_ref/service/process/_index.md" "weight" "50"
upsert_frontmatter "$schema_ref/error-handling/check/_index.md" "weight" "10"
upsert_frontmatter "$schema_ref/error-handling/predicate/_index.md" "weight" "15"
upsert_frontmatter "$schema_ref/error-handling/result/_index.md" "weight" "20"
upsert_frontmatter "$schema_ref/error-handling/validation/_index.md" "weight" "30"
upsert_frontmatter "$schema_ref/error-handling/diagnostics/_index.md" "weight" "40"
upsert_frontmatter "$schema_ref/error-handling/refined/_index.md" "weight" "50"
upsert_frontmatter "$schema_ref/schema/_index.md" "weight" "10"
upsert_frontmatter "$schema_ref/codec/_index.md" "weight" "20"
upsert_frontmatter "$schema_ref/data/_index.md" "weight" "30"

# Rewrite absolute /reference/<group> links (in hand-written guides and any
# generated pages) to the split locations.
declare -A ref_home=(
  [check]=schema/reference/error-handling [predicate]=schema/reference/error-handling [result]=schema/reference/error-handling
  [validation]=schema/reference/error-handling [diagnostics]=schema/reference/error-handling [refined]=schema/reference/error-handling
  [schema]=schema/reference [codec]=schema/reference [data]=schema/reference
  [app]=flow/reference [flow]=flow/reference [fiber]=flow/reference [exit]=flow/reference [cause]=flow/reference
  [concurrency]=flow/reference [schedule]=flow/reference [ref]=flow/reference [stm]=flow/reference [stream]=flow/reference
  [bind]=flow/reference [service]=flow/reference [layer]=flow/reference [scope]=flow/reference
  [hosting]=flow/reference [hosting-node]=flow/reference [hosting-browser]=flow/reference
)
sed_args=()
for group in "${!ref_home[@]}"; do
  sed_args+=(-e "s|/reference/$group\b|/${ref_home[$group]}/$group|g")
done
find "$schema_dir" "$flow_dir" -name "*.md" -type f -exec sed -i "${sed_args[@]}" {} \;

# Fix all files: remove body titles to avoid double headings in Hugo
find "$schema_dir" "$flow_dir" -name "*.md" -type f -exec sed -i '/^# /d' {} \;

# Ensure all pages are marked as docs type
find "$schema_dir" "$flow_dir" -type f -name "*.md" -print0 |
  while IFS= read -r -d '' page; do
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"
