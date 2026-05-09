#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ref_dir="$root_dir/site/content/reference"
docs_dir="$root_dir/site/content/docs"
src_dir="$root_dir/docs/reference/fsflow"

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

create_ref_section() {
  local name="$1"
  local title="$2"
  local weight="$3"
  local main_file="$4"
  mkdir -p "$ref_dir/$name"
  
  if [ -f "$src_dir/$main_file" ]; then
    cp "$src_dir/$main_file" "$ref_dir/$name/_index.md"
    upsert_frontmatter "$ref_dir/$name/_index.md" "title" "\"$title\""
    upsert_frontmatter "$ref_dir/$name/_index.md" "type" "docs"
    upsert_frontmatter "$ref_dir/$name/_index.md" "weight" "$weight"
  else
    echo "---
title: \"$title\"
type: docs
weight: $weight
---" > "$ref_dir/$name/_index.md"
  fi
}

# API Reference Sections
create_ref_section "flow" "Flow" 10 "flow.md"
create_ref_section "asyncflow" "AsyncFlow" 20 "asyncflow.md"
create_ref_section "taskflow" "TaskFlow" 30 "taskflow.md"
create_ref_section "check" "Check" 40 "check.md"
create_ref_section "guard" "Guard" 50 "guard.md"
create_ref_section "validation" "Validation" 60 "validation.md"
create_ref_section "result" "Result" 70 "builders-result.md"
create_ref_section "diagnostics" "Diagnostics" 80 "diagnostics.md"
create_ref_section "asyncflow-runtime" "Runtime" 90 "runtime.md"
create_ref_section "taskflow-runtime" "TaskFlow Runtime" 100 "taskflow-runtime.md"
create_ref_section "taskflow-spec" "TaskFlowSpec" 110 "taskflow-spec.md"
create_ref_section "coldtask" "ColdTask" 120 "coldtask.md"
create_ref_section "caps" "CAPS" 130 "capability.md"
create_ref_section "interop" "Interop" 140 "interop.md"

# Helper to copy patterns
copy_group() {
  local target="$1"
  shift
  local patterns=("$@")
  
  for pattern in "${patterns[@]}"; do
    find "$src_dir" -maxdepth 1 -name "$pattern" -exec cp {} "$ref_dir/$target/" \;
  done
}

copy_group "flow" "builders-flow.md" "flow-*.md"
copy_group "asyncflow" "builders-asyncflow.md" "asyncflow-*.md"
rm -f "$ref_dir/asyncflow/asyncflow-runtime-"*

copy_group "taskflow" "taskbuilders-taskflow.md" "taskflow-*.md"
rm -f "$ref_dir/taskflow/taskflow-runtime-"*
rm -f "$ref_dir/taskflow/taskflow-spec.md"

copy_group "check" "check-*.md"
copy_group "guard" "guard.md"
copy_group "result" "builders-result.md"
copy_group "validation" "builders-validate.md" "validation-*.md"
copy_group "diagnostics" "diagnostics-*.md" "path.md" "pathsegment.md" "diagnostic.md"
copy_group "asyncflow-runtime" "retrypolicy*.md" "asyncflow-runtime-*.md" "loglevel.md" "logentry.md" "log*.md"
copy_group "taskflow-runtime" "runtimecontext*.md" "taskflow-runtime-*.md"
copy_group "taskflow-spec" "taskflowspec-*.md"
copy_group "coldtask" "coldtask-*.md"
copy_group "caps" "needs.md" "env.md" "capability-*.md" "layer-providelayer.md" "missingcapability.md"
copy_group "interop" "interop.md"

find "$ref_dir" -type f -name "*.md" ! -name "_index.md" -print0 |
  while IFS= read -r -d '' page; do
    upsert_frontmatter "$page" "type" "docs"
  done

# Copy the root Reference index
cp "$root_dir/docs/reference/_index.md" "$ref_dir/_index.md"
upsert_frontmatter "$ref_dir/_index.md" "type" "docs"
upsert_frontmatter "$ref_dir/_index.md" "weight" "30"

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true

# Re-create the Guides section indices
mkdir -p "$docs_dir/start" \
         "$docs_dir/core-model" \
         "$docs_dir/patterns" \
         "$docs_dir/ecosystem"

echo "---
title: \"Start\"
type: docs
weight: 10
---" > "$docs_dir/start/_index.md"

echo "---
title: \"Core Model\"
type: docs
weight: 20
---" > "$docs_dir/core-model/_index.md"

echo "---
title: \"Patterns\"
type: docs
weight: 30
---" > "$docs_dir/patterns/_index.md"

echo "---
title: \"Ecosystem\"
type: docs
weight: 40
---" > "$docs_dir/ecosystem/_index.md"

# Fixed 'Docs' landing page - avoid flat list
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
<span class=\"label\">Core Model</span>
<h2><a href=\"./core-model/\">How FsFlow fits together</a></h2>
<p>Read the semantics, environment model, CAPS boundaries, and task or async interop rules.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Patterns</span>
<h2><a href=\"./patterns/\">Usage patterns</a></h2>
<p>Use runnable examples, benchmarks, and type troubleshooting notes while applying FsFlow.</p>
</section>

<section class=\"docs-card\">
<span class=\"label\">Ecosystem</span>
<h2><a href=\"./ecosystem/\">Integrations</a></h2>
<p>Map FsFlow alongside Validus, FsToolkit.ErrorHandling, FSharpPlus, IcedTasks, and Effect-TS.</p>
</section>

</div>
" > "$docs_dir/_index.md"
