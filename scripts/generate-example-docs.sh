#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_file="${DOCS_EXAMPLES_OUTPUT:-$root_dir/docs/examples/README.md}"

mkdir -p "$(dirname "$output_file")"

render_code_block() {
  local language="$1"
  local file_path="$2"

  printf '```%s\n' "$language"
  cat "$file_path"
  printf '\n```\n'
}

run_example() {
  local project_path="$1"
  local example_filter="${2:-}"

  dotnet build "$project_path" --nologo --verbosity quiet
  if [[ -n "$example_filter" ]]; then
    FSFLOW_EXAMPLE="$example_filter" dotnet run --project "$project_path" --no-build --no-restore --nologo 2>&1
  else
    dotnet run --project "$project_path" --no-build --no-restore --nologo 2>&1
  fi
}

render_example_section() {
  local title="$1"
  local description="$2"
  local project_path="$3"
  local source_file="$4"
  local source_link="$5"
  local run_command="$6"
  local example_filter="${7:-}"

  local example_output
  example_output="$(run_example "$project_path" "$example_filter")"

  {
    printf '## %s\n\n' "$title"
    printf '%s\n\n' "$description"
    printf 'Run it:\n\n'
    printf '```bash\n%s\n```\n\n' "$run_command"
    printf 'Source:\n\n'
    printf -- '- [%s](%s)\n\n' "$(basename "$source_file")" "$source_link"
    printf 'Source code:\n\n'
    render_code_block fsharp "$source_file"
    printf '\n'
  } >> "$output_file"
}

cat > "$output_file" <<'EOF'
---
title: Runnable Examples
description: Application-shaped examples that are executed during docs generation and mirrored back into the site.
---

# Runnable Examples

This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.

The examples below are built from the repository projects, run with the current source, and then written back into this page.

The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.
The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.

EOF

render_example_section \
  "Request Boundary Example" \
  "This example shows a request boundary that pulls a user from a database-like environment, threads a trace id through the request context, and reuses the same validation shape across Flow, AsyncFlow, and TaskFlow." \
  "$root_dir/examples/FsFlow.Examples/FsFlow.Examples.fsproj" \
  "$root_dir/examples/FsFlow.Examples/RequestBoundaryExample.fs" \
  "https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Examples/RequestBoundaryExample.fs" \
  "FSFLOW_EXAMPLE=request-boundary dotnet run --project examples/FsFlow.Examples/FsFlow.Examples.fsproj --nologo" \
  "request-boundary"

render_example_section \
  "Diagnostics Example" \
  "This example shows a JSON-shaped request boundary with a root-level error, nested child branches, and a display-friendly diagnostics tree." \
  "$root_dir/examples/FsFlow.Examples/FsFlow.Examples.fsproj" \
  "$root_dir/examples/FsFlow.Examples/DiagnosticsExample.fs" \
  "https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Examples/DiagnosticsExample.fs" \
  "FSFLOW_EXAMPLE=diagnostics dotnet run --project examples/FsFlow.Examples/FsFlow.Examples.fsproj --nologo" \
  "diagnostics"

render_example_section \
  "CAPS Core Example" \
  'This example shows the sync-first FsFlow.Caps.Core surface: fixed and live capability providers, plus typed errors for missing and invalid environment variables.' \
  "$root_dir/examples/FsFlow.Caps.Core.Examples/FsFlow.Caps.Core.Examples.fsproj" \
  "$root_dir/examples/FsFlow.Caps.Core.Examples/CoreCapabilitiesExample.fs" \
  "https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Caps.Core.Examples/CoreCapabilitiesExample.fs" \
  "dotnet run --project examples/FsFlow.Caps.Core.Examples/FsFlow.Caps.Core.Examples.fsproj --nologo"

render_example_section \
  "Playground Example" \
  "This example shows the same core boundary across Flow, AsyncFlow, and TaskFlow using the normal direct-bind style inside each computation expression." \
  "$root_dir/examples/FsFlow.Playground/FsFlow.Playground.fsproj" \
  "$root_dir/examples/FsFlow.Playground/Program.fs" \
  "https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Playground/Program.fs" \
  "dotnet run --project examples/FsFlow.Playground/FsFlow.Playground.fsproj --nologo"

render_example_section \
  "Maintenance Example" \
  "This example shows smaller, focused shapes for maintenance and interop scenarios without switching away from the normal direct-bind style." \
  "$root_dir/examples/FsFlow.MaintenanceExamples/FsFlow.MaintenanceExamples.fsproj" \
  "$root_dir/examples/FsFlow.MaintenanceExamples/Program.fs" \
  "https://github.com/adz/FsFlow/blob/main/examples/FsFlow.MaintenanceExamples/Program.fs" \
  "dotnet run --project examples/FsFlow.MaintenanceExamples/FsFlow.MaintenanceExamples.fsproj --nologo"
