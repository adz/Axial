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

# Sync guide directories from docs/ to site/content/docs/
# We exclude reference, content, and the root AGENT.md/index.md for now
rm -rf "$docs_dir"
for dir in ecosystem error-handling flow patterns refined schema start validation; do
  if [ -d "$root_dir/docs/$dir" ]; then
    mkdir -p "$docs_dir/$dir"
    cp -r "$root_dir/docs/$dir/"* "$docs_dir/$dir/"
  fi
done

# Fix all files: remove body titles to avoid double headings in Hugo
find "$ref_dir" "$docs_dir" -name "*.md" -type f -exec sed -i '/^# /d' {} \;

# Ensure all guide pages are marked as docs type
find "$docs_dir" -type f -name "*.md" -print0 |
  while IFS= read -r -d '' page; do
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"

# Copy the two group landing pages
mkdir -p "$root_dir/site/content/parse" "$root_dir/site/content/flow"
cp "$root_dir/docs/landing/parse.md" "$root_dir/site/content/parse/_index.md"
cp "$root_dir/docs/landing/flow.md" "$root_dir/site/content/flow/_index.md"

# Fixed 'Docs' landing page - avoid flat list
mkdir -p "$docs_dir"
echo "---
title: \"Docs\"
linkTitle: \"Docs\"
type: docs
weight: 20
---

Welcome to the Axial guides. Axial has two doors — [Schema](./schema/) for domain models and plain
[Result](./error-handling/) for simple code — with [Flow](./flow/) as the optional effects side. Start with
[Getting Started](./start/getting-started/).

<div class=\"docs-grid docs-index-grid\">

<section class=\"docs-card\">
<span class=\"label\">Getting oriented</span>
<h2><a href=\"./start/\">Start</a></h2>
<p>Install the package, learn the two-lane rule, and run small examples.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Door one &middot; domain models</span>
<h2><a href=\"./schema/\">Schema</a></h2>
<p>Declare the model once: input parsing, validation, redisplay, contextual rules, policies, and metadata interpreters fall out.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Door two &middot; simple code</span>
<h2><a href=\"./error-handling/\">Error Handling</a></h2>
<p>Plain F# Result with your own error type, kept terse by Check, focused helpers, and result { }.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Effects</span>
<h2><a href=\"./flow/\">Flow</a></h2>
<p>Environment access, async or task work, layers, resources, scheduling, concurrency, and service tutorials.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Machinery &middot; single values</span>
<h2><a href=\"./refined/\">Refined</a></h2>
<p>Parse and refine individual boundary values; the toolkit schemas use for their fields.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Machinery &middot; diagnostics</span>
<h2><a href=\"./validation/\">Validation</a></h2>
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
