#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
schema_output="${DOCS_SCHEMA_EXAMPLES_OUTPUT:-$root_dir/docs/schema/examples.md}"
flow_output="${DOCS_FLOW_EXAMPLES_OUTPUT:-$root_dir/docs/flow/examples.md}"

mkdir -p "$(dirname "$schema_output")" "$(dirname "$flow_output")"

# Build the pages in temp files and move them into place only after every section succeeded,
# so a mid-run failure (or a killed run) can never leave truncated docs behind.
schema_staging="$(mktemp "${TMPDIR:-/tmp}/axial-schema-examples.XXXXXX")"
flow_staging="$(mktemp "${TMPDIR:-/tmp}/axial-flow-examples.XXXXXX")"
trap 'rm -f "$schema_staging" "$flow_staging"' EXIT

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

  dotnet build "$project_path" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
  if [[ -n "$example_filter" ]]; then
    AXIAL_EXAMPLE="$example_filter" dotnet run --project "$project_path" --no-build --no-restore --nologo 2>&1
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
  printf 'Building docs example: %s\n' "$title"

  if ! example_output="$(run_example "$project_path" "$example_filter")"; then
    printf 'Docs example failed: %s\n' "$title" >&2
    printf '%s\n' "$example_output" >&2
    return 1
  fi

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

write_page_header() {
  local file="$1"
  local description="$2"

  {
    printf -- '---\n'
    printf 'weight: 85\n'
    printf 'title: Runnable Examples\n'
    printf 'description: %s\n' "$description"
    printf -- '---\n\n'
    printf '# Runnable Examples\n\n'
    printf 'This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.\n\n'
    printf 'The examples below are built from the repository projects, run with the current source, and then written back into this page.\n\n'
    printf 'The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.\n'
    printf 'The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.\n\n'
  } > "$file"
}

write_page_header "$schema_staging" "Executable schema, refined, diagnostics, and policy examples mirrored back into the docs."
write_page_header "$flow_staging" "Executable workflow boundary examples mirrored back into the docs."

output_file="$flow_staging"
render_example_section \
  "Request Boundary Example" \
  "This example shows a request boundary that pulls a user from a database-like environment, threads a trace id through the request context, and reuses the same validation shape across Flow." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/RequestBoundaryExample.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RequestBoundaryExample.fs" \
  "AXIAL_EXAMPLE=request-boundary dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "request-boundary"

output_file="$schema_staging"
render_example_section \
  "Diagnostics Example" \
  "This example shows a JSON-shaped request boundary with a root-level error, nested child branches, and a display-friendly diagnostics tree." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/DiagnosticsExample.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/DiagnosticsExample.fs" \
  "AXIAL_EXAMPLE=diagnostics dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "diagnostics"

render_example_section \
  "Refined Catalog Example" \
  "This example shows a request boundary that parses strings, builds refined numeric/text/collection values, chooses a domain union case, and rejects invalid input before the domain record is created." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/RefinedCatalogExample.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RefinedCatalogExample.fs" \
  "AXIAL_EXAMPLE=refined-catalog dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "refined-catalog"

render_example_section \
  "Refined Value Schema Example" \
  "This example shows total domain conversions built with Schema.convert, composed into a record schema, and lowered to executable checks." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/RefinedValueSchemaExample.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RefinedValueSchemaExample.fs" \
  "AXIAL_EXAMPLE=refined-value-schema dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "refined-value-schema"

render_example_section \
  "Minimal API Boundary Example" \
  "This example is a complete ASP.NET Core minimal API where one schema declaration drives JSON body parsing with 400 path diagnostics, trusted-model serialization through the compiled codec, a generated OpenAPI document, and an HTML form with redisplay. Running it with AXIAL_EXAMPLE=smoke starts the server and exercises every endpoint." \
  "$root_dir/examples/Axial.Api/Axial.Api.fsproj" \
  "$root_dir/examples/Axial.Api/Program.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Api/Program.fs" \
  "AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api/Axial.Api.fsproj --nologo" \
  "smoke"

render_example_section \
  "Policy Example" \
  "This example shows Policy adapting every verification boundary — raw parsing, refined construction, schema input parsing, intrinsic validation, and contextual rules — into one workflow error type run with Flow.verify." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/PolicyExamples.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/PolicyExamples.fs" \
  "AXIAL_EXAMPLE=policy dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "policy"

output_file="$flow_staging"
render_example_section \
  'Playground Example' \
  "This example shows the same core boundary across Flow using the normal direct-bind style inside each computation expression." \
  "$root_dir/examples/Axial.Playground/Axial.Playground.fsproj" \
  "$root_dir/examples/Axial.Playground/Program.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Playground/Program.fs" \
  "dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo"

render_example_section \
  "Maintenance Example" \
  "This example shows smaller, focused shapes for maintenance and interop scenarios without switching away from the normal direct-bind style." \
  "$root_dir/examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj" \
  "$root_dir/examples/Axial.MaintenanceExamples/Program.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.MaintenanceExamples/Program.fs" \
  "dotnet run --project examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj --nologo"

render_example_section \
  "Supervision and Fiber Observability Example" \
  "This example shows Flow.Runtime.supervise restarting a background worker that dies with a defect, a FiberObserver reporting the defect of a fiber whose fork handle was discarded, and Flow.forkDetached stating intentional fire-and-forget so the report is suppressed." \
  "$root_dir/examples/Axial.Examples/Axial.Examples.fsproj" \
  "$root_dir/examples/Axial.Examples/SupervisionExample.fs" \
  "https://github.com/adz/Axial/blob/main/examples/Axial.Examples/SupervisionExample.fs" \
  "AXIAL_EXAMPLE=supervision dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo" \
  "supervision"

# mktemp creates the staging files with mode 600; the docs should stay world-readable.
chmod 644 "$schema_staging" "$flow_staging"
mv "$schema_staging" "$schema_output"
mv "$flow_staging" "$flow_output"
