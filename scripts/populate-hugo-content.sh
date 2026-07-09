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

# Axial's three areas each get a top-level section: /error-handling/
# (with validation nested as its machinery, since Validation ships in
# Axial.ErrorHandling), /schema/ (with refined nested as its machinery,
# since Refined ships in Axial.Schema), and /flow/. Both docs/ and the
# generated site mirror the same nesting now that the source folders live
# at docs/error-handling/validation and docs/schema/refined directly.
# The generated API reference is split across them under <area>/reference/.
eh_dir="$root_dir/site/content/error-handling"
schema_dir="$root_dir/site/content/schema"
flow_dir="$root_dir/site/content/flow"
rm -rf "$eh_dir" "$schema_dir" "$flow_dir" \
  "$root_dir/site/content/docs" "$root_dir/site/content/reference" "$root_dir/site/content/parse"
mkdir -p "$eh_dir" "$schema_dir" "$flow_dir"

cp -r "$root_dir/docs/error-handling/"* "$eh_dir/"
cp -r "$root_dir/docs/schema/"* "$schema_dir/"
cp -r "$root_dir/docs/flow/"* "$flow_dir/"

# The landing pages are the section indexes for schema and flow
cp "$root_dir/docs/landing/schema.md" "$schema_dir/_index.md"
cp "$root_dir/docs/landing/flow.md" "$flow_dir/_index.md"

# Distribute the generated API reference (docs/reference/<group>) into the
# three areas, then apply weights and per-area index pages.
eh_ref="$eh_dir/reference"
schema_ref="$schema_dir/reference"
flow_ref="$flow_dir/reference"

copy_ref_group() {
  local dest="$1"
  local group="$2"
  mkdir -p "$dest"
  cp -r "$root_dir/docs/reference/$group" "$dest/"
}

for group in check predicate result validation diagnostics; do
  copy_ref_group "$eh_ref" "$group"
done
for group in schema codec refined; do
  copy_ref_group "$schema_ref" "$group"
done
for group in flow fiber exit cause concurrency schedule ref stm stream bind service layer scope; do
  copy_ref_group "$flow_ref" "$group"
done

cp "$root_dir/docs/reference-indexes/error-handling.md" "$eh_ref/_index.md"
cp "$root_dir/docs/reference-indexes/schema.md" "$schema_ref/_index.md"
cp "$root_dir/docs/reference-indexes/flow.md" "$flow_ref/_index.md"

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
upsert_frontmatter "$eh_ref/check/_index.md" "weight" "10"
upsert_frontmatter "$eh_ref/predicate/_index.md" "weight" "15"
upsert_frontmatter "$eh_ref/result/_index.md" "weight" "20"
upsert_frontmatter "$eh_ref/validation/_index.md" "weight" "30"
upsert_frontmatter "$eh_ref/diagnostics/_index.md" "weight" "40"
upsert_frontmatter "$schema_ref/schema/_index.md" "weight" "10"
upsert_frontmatter "$schema_ref/codec/_index.md" "weight" "20"
upsert_frontmatter "$schema_ref/refined/_index.md" "weight" "30"

# Rewrite absolute /reference/<group> links (in hand-written guides and any
# generated pages) to the split locations.
declare -A ref_home=(
  [check]=error-handling [predicate]=error-handling [result]=error-handling [validation]=error-handling [diagnostics]=error-handling
  [schema]=schema [codec]=schema [refined]=schema
  [flow]=flow [fiber]=flow [exit]=flow [cause]=flow [concurrency]=flow [schedule]=flow
  [ref]=flow [stm]=flow [stream]=flow [bind]=flow [service]=flow [layer]=flow [scope]=flow
)
sed_args=()
for group in "${!ref_home[@]}"; do
  sed_args+=(-e "s|/reference/$group\b|/${ref_home[$group]}/reference/$group|g")
done
find "$eh_dir" "$schema_dir" "$flow_dir" -name "*.md" -type f -exec sed -i "${sed_args[@]}" {} \;

# Fix all files: remove body titles to avoid double headings in Hugo
find "$eh_dir" "$schema_dir" "$flow_dir" -name "*.md" -type f -exec sed -i '/^# /d' {} \;

# Ensure all pages are marked as docs type
find "$eh_dir" "$schema_dir" "$flow_dir" -type f -name "*.md" -print0 |
  while IFS= read -r -d '' page; do
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"
