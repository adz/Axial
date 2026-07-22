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

# Axial has three product documentation areas: /validation/, /schema/, and
# /flow/. Generated API reference is distributed under the product that owns
# each package.
validation_dir="$root_dir/site/content/validation"
schema_dir="$root_dir/site/content/schema"
flow_dir="$root_dir/site/content/flow"
rm -rf "$root_dir/site/content/error-handling" "$root_dir/site/content/data" \
  "$validation_dir" "$schema_dir" "$flow_dir" \
  "$root_dir/site/content/docs" "$root_dir/site/content/reference" "$root_dir/site/content/parse"
mkdir -p "$validation_dir" "$schema_dir" "$flow_dir"

cp -r "$root_dir/docs/validation/." "$validation_dir/"
cp -r "$root_dir/docs/schema/." "$schema_dir/"
cp -r "$root_dir/docs/flow/." "$flow_dir/"
rm -f "$validation_dir/llms.txt" "$schema_dir/llms.txt" "$flow_dir/llms.txt"

# Product-local generated API reference is copied with the guides. Apply the
# navigation weights needed by the rendered site.
validation_ref="$validation_dir/reference"
schema_ref="$schema_dir/reference"
flow_ref="$flow_dir/reference"

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
upsert_frontmatter "$validation_ref/check/_index.md" "weight" "10"
upsert_frontmatter "$validation_ref/predicate/_index.md" "weight" "15"
upsert_frontmatter "$validation_ref/result/_index.md" "weight" "20"
upsert_frontmatter "$validation_ref/validation/_index.md" "weight" "30"
upsert_frontmatter "$validation_ref/diagnostics/_index.md" "weight" "40"
upsert_frontmatter "$validation_ref/refined/_index.md" "weight" "50"
upsert_frontmatter "$schema_ref/schema/_index.md" "weight" "10"
upsert_frontmatter "$schema_ref/codec/_index.md" "weight" "20"
upsert_frontmatter "$schema_ref/data/_index.md" "weight" "30"

# Hugo's docs layout supplies the page title. Keep generated content uniform
# with pages whose source already omits a body-level H1.
find "$validation_dir" "$schema_dir" "$flow_dir" -type f -name "*.md" -print0 |
  node -e '
    const fs = require("node:fs");
    for (const path of fs.readFileSync(0, "utf8").split("\0")) {
      if (!path) continue;
      const content = fs.readFileSync(path, "utf8")
        .split(/(?<=\n)/)
        .filter(line => !line.startsWith("# "))
        .join("");
      const frontmatterEnd = content.indexOf("\n---", 4);
      if (frontmatterEnd < 0) throw new Error(`missing frontmatter: ${path}`);
      let frontmatter = content.slice(0, frontmatterEnd);
      if (/^type:/m.test(frontmatter)) {
        frontmatter = frontmatter.replace(/^type:.*$/m, "type: docs");
      } else {
        frontmatter += "\ntype: docs";
      }
      fs.writeFileSync(path, frontmatter + content.slice(frontmatterEnd));
    }
  '

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
mkdir -p "$root_dir/site/static/schema" "$root_dir/site/static/flow"
cp "$root_dir/docs/schema/llms.txt" "$root_dir/site/static/schema/llms.txt"
cp "$root_dir/docs/flow/llms.txt" "$root_dir/site/static/flow/llms.txt"
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"
