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
upsert_frontmatter "$ref_dir/flow/runtime/_index.md" "weight" "100"
upsert_frontmatter "$ref_dir/check/_index.md" "weight" "40"
upsert_frontmatter "$ref_dir/validation/_index.md" "weight" "60"
upsert_frontmatter "$ref_dir/result/_index.md" "weight" "70"
upsert_frontmatter "$ref_dir/diagnostics/_index.md" "weight" "80"
upsert_frontmatter "$ref_dir/ref/_index.md" "weight" "90"
upsert_frontmatter "$ref_dir/stm/_index.md" "weight" "100"
upsert_frontmatter "$ref_dir/schedule/_index.md" "weight" "110"
upsert_frontmatter "$ref_dir/stream/_index.md" "weight" "120"
upsert_frontmatter "$ref_dir/capability/_index.md" "weight" "130"
upsert_frontmatter "$ref_dir/capability/core/_index.md" "weight" "10"
upsert_frontmatter "$ref_dir/capability/console/_index.md" "weight" "20"
upsert_frontmatter "$ref_dir/capability/filesystem/_index.md" "weight" "30"
upsert_frontmatter "$ref_dir/capability/http/_index.md" "weight" "40"
upsert_frontmatter "$ref_dir/capability/process/_index.md" "weight" "50"

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
for dir in core-model ecosystem managing-dependencies patterns start state-concurrency tutorials validation-results; do
  if [ -d "$root_dir/docs/$dir" ]; then
    mkdir -p "$docs_dir/$dir"
    cp -r "$root_dir/docs/$dir/"* "$docs_dir/$dir/"
  fi
done
rm -f "$docs_dir/managing-dependencies/capabilities.md"

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

# Fixed 'Docs' landing page - avoid flat list
mkdir -p "$docs_dir"
echo "---
title: \"Docs\"
linkTitle: \"Docs\"
type: docs
weight: 20
---

Welcome to the FsFlow guides. Choose a section from the sidebar or start with [Getting Started](./start/getting-started/).

<div class=\"docs-grid docs-index-grid\">

<section class=\"docs-card\">
<span class=\"label\">Start</span>
<h2><a href=\"./start/\">Getting oriented</a></h2>
<p>Install the package, see tiny examples, and learn the validation-first path into real app boundaries.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Tutorials</span>
<h2><a href=\"./tutorials/\">Step-by-step guides</a></h2>
<p>Learn common setups for records, capabilities, and .NET host integration.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Validation & Results</span>
<h2><a href=\"./validation-results/\">Pure Checks and Results</a></h2>
<p>Overview of the FsFlow validation stack, from pure checks to structured diagnostics.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Core Model</span>
<h2><a href=\"./core-model/\">How FsFlow fits together</a></h2>
<p>Read the semantics, task or async interop rules, and architectural styles.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Managing Dependencies</span>
<h2><a href=\"./managing-dependencies/\">Environment handling</a></h2>
<p>Learn how to manage dependencies using records, capability contracts, and runtime-owned services.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">State and Concurrency</span>
<h2><a href=\"./state-concurrency/\">Concurrent workflows</a></h2>
<p>Manage shared state, coordination, retries, and streaming.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Patterns</span>
<h2><a href=\"./patterns/\">Usage patterns</a></h2>
<p>Use runnable examples, benchmarks, and type troubleshooting notes while applying FsFlow.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Comparisons</span>
<h2><a href=\"./ecosystem/\">Comparisons and Integrations</a></h2>
<p>Compare FsFlow with Validus, FsToolkit.ErrorHandling, FSharpPlus, and Effect-TS, and see where they fit together.</p>
</section>

</div>
" > "$docs_dir/_index.md"
