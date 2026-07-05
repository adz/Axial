#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ref_dir="$root_dir/site/content/reference"
docs_dir="$root_dir/site/content/docs"

# Rebuild the generated reference subtree from scratch so removed API pages do
# not linger as stale site content.
rm -rf "$ref_dir"
mkdir -p "$ref_dir"

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

# The generator now creates a directory structure in docs/reference/ that
# matches our desired site structure. We just copy it over.

cp -r "$root_dir/docs/reference/"* "$ref_dir/"

# Fix index files: remove body titles to avoid double headings in Hugo
find "$ref_dir" -name "_index.md" -type f -exec sed -i '/^# /d' {} \;

# Set weights for main sections
upsert_frontmatter "$ref_dir/flow/_index.md" "weight" "10"
upsert_frontmatter "$ref_dir/flow/runtime/_index.md" "weight" "10"
upsert_frontmatter "$ref_dir/fiber/_index.md" "weight" "20"
upsert_frontmatter "$ref_dir/exit/_index.md" "weight" "30"
upsert_frontmatter "$ref_dir/cause/_index.md" "weight" "40"
upsert_frontmatter "$ref_dir/concurrency/_index.md" "weight" "115"
upsert_frontmatter "$ref_dir/result/_index.md" "weight" "60"
upsert_frontmatter "$ref_dir/check/_index.md" "weight" "70"
upsert_frontmatter "$ref_dir/validation/_index.md" "weight" "80"
upsert_frontmatter "$ref_dir/diagnostics/_index.md" "weight" "90"
upsert_frontmatter "$ref_dir/schedule/_index.md" "weight" "100"
upsert_frontmatter "$ref_dir/ref/_index.md" "weight" "110"
upsert_frontmatter "$ref_dir/stm/_index.md" "weight" "120"
upsert_frontmatter "$ref_dir/stream/_index.md" "weight" "130"
upsert_frontmatter "$ref_dir/service/_index.md" "weight" "140"
upsert_frontmatter "$ref_dir/layer/_index.md" "weight" "150"
upsert_frontmatter "$ref_dir/scope/_index.md" "weight" "160"
upsert_frontmatter "$ref_dir/service/core/_index.md" "weight" "10"
upsert_frontmatter "$ref_dir/service/console/_index.md" "weight" "20"
upsert_frontmatter "$ref_dir/service/filesystem/_index.md" "weight" "30"
upsert_frontmatter "$ref_dir/service/http/_index.md" "weight" "40"
upsert_frontmatter "$ref_dir/service/process/_index.md" "weight" "50"

# Ensure all reference pages are marked as docs type
find "$ref_dir" -type f -name "*.md" -print0 |
  while IFS= read -r -d '' page; do
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy root Reference index (manually maintained)
cp "$root_dir/docs/reference/_index.md" "$ref_dir/_index.md"
upsert_frontmatter "$ref_dir/_index.md" "type" "docs"
upsert_frontmatter "$ref_dir/_index.md" "weight" "30"

# Axial's three areas each get a top-level section: /error-handling/,
# /schema/ (with refined and validation nested as its machinery), and /flow/.
# Cross-cutting sections stay under /docs/.
eh_dir="$root_dir/site/content/error-handling"
schema_dir="$root_dir/site/content/schema"
flow_dir="$root_dir/site/content/flow"
rm -rf "$docs_dir" "$eh_dir" "$schema_dir" "$flow_dir" "$root_dir/site/content/parse"
mkdir -p "$docs_dir" "$eh_dir" "$schema_dir" "$flow_dir"

for dir in ecosystem patterns start; do
  if [ -d "$root_dir/docs/$dir" ]; then
    mkdir -p "$docs_dir/$dir"
    cp -r "$root_dir/docs/$dir/"* "$docs_dir/$dir/"
  fi
done

cp -r "$root_dir/docs/error-handling/"* "$eh_dir/"
cp -r "$root_dir/docs/schema/"* "$schema_dir/"
for dir in refined validation; do
  mkdir -p "$schema_dir/$dir"
  cp -r "$root_dir/docs/$dir/"* "$schema_dir/$dir/"
done
cp -r "$root_dir/docs/flow/"* "$flow_dir/"

# The landing pages are the section indexes for schema and flow
cp "$root_dir/docs/landing/schema.md" "$schema_dir/_index.md"
cp "$root_dir/docs/landing/flow.md" "$flow_dir/_index.md"

# Fix all files: remove body titles to avoid double headings in Hugo
find "$ref_dir" "$docs_dir" "$eh_dir" "$schema_dir" "$flow_dir" -name "*.md" -type f -exec sed -i '/^# /d' {} \;

# Ensure all guide pages are marked as docs type (landing indexes keep their
# own layout)
find "$docs_dir" "$eh_dir" "$schema_dir" "$flow_dir" -type f -name "*.md" -print0 |
  while IFS= read -r -d '' page; do
    case "$page" in
      "$schema_dir/_index.md" | "$flow_dir/_index.md") continue ;;
    esac
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"

# Fixed 'Docs' landing page - avoid flat list
mkdir -p "$docs_dir"
echo "---
title: \"Docs\"
linkTitle: \"Docs\"
type: docs
weight: 20
---

Welcome to the Axial guides. Axial consists of three areas that can be used independently but work together:
[Error Handling]({{< relref \"/error-handling/\" >}}) for pure fail-fast checks,
[Schema]({{< relref \"/schema/\" >}}) for domain models at data boundaries, and
[Flow]({{< relref \"/flow/\" >}}) for the effects around them. Start with
[Getting Started](./start/getting-started/).

<div class=\"docs-grid docs-index-grid\">

<section class=\"docs-card\">
<span class=\"label\">Getting oriented</span>
<h2><a href=\"./start/\">Start</a></h2>
<p>Install the package, pick the right area for the work in front of you, and run small examples.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Simple code</span>
<h2><a href=\"{{< relref \"/error-handling/\" >}}\">Error Handling</a></h2>
<p>Plain F# Result with your own error type, kept terse by Check, focused helpers, and result { }.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Domain models</span>
<h2><a href=\"{{< relref \"/schema/\" >}}\">Schema</a></h2>
<p>Declare the model once: input parsing, validation, redisplay, contextual rules, policies, and metadata interpreters fall out.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Effects</span>
<h2><a href=\"{{< relref \"/flow/\" >}}\">Flow</a></h2>
<p>Environment access, async or task work, layers, resources, scheduling, concurrency, and service tutorials.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Schema machinery &middot; single values</span>
<h2><a href=\"{{< relref \"/schema/refined/\" >}}\">Refined</a></h2>
<p>Parse and refine individual boundary values; the toolkit schemas use for their fields.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Schema machinery &middot; diagnostics</span>
<h2><a href=\"{{< relref \"/schema/validation/\" >}}\">Validation</a></h2>
<p>Accumulating sibling failures with Validation, Diagnostics, and validate { } — the error trees schema parsing produces.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Usage patterns</span>
<h2><a href=\"./patterns/\">Patterns</a></h2>
<p>Use runnable examples, benchmarks, and type troubleshooting notes while applying Axial.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Comparisons and integrations</span>
<h2><a href=\"./ecosystem/\">Comparisons</a></h2>
<p>Compare Axial with Validus, FsToolkit.ErrorHandling, FSharpPlus, and Effect-TS, and see where they fit together.</p>
</section>

</div>
" > "$docs_dir/_index.md"
