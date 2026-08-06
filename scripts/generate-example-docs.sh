#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
skip_build=false

for arg in "$@"; do
  case "$arg" in
    flow) ;;
    --no-build) skip_build=true ;;
    *) echo "Usage: $0 [flow] [--no-build]" >&2; exit 2 ;;
  esac
done

if ! $skip_build; then
  dotnet msbuild "$root_dir/scripts/docs-build.proj" -t:Build -m -nologo -verbosity:minimal -p:DocsBuildScope=Examples
fi

output="${DOCS_FLOW_EXAMPLES_OUTPUT:-$root_dir/docs/flow/examples.md}"
staging="$(mktemp "${TMPDIR:-/tmp}/axial-examples.XXXXXX")"
trap 'rm -f "$staging"' EXIT

{
  printf '%s\n' '---' 'weight: 85' 'title: Runnable Examples' 'description: Executable Axial examples mirrored into the documentation.' '---' '' '# Runnable Examples' ''
  printf '%s\n\n' 'These examples are built and run while this page is generated, keeping the documentation tied to executable code.'
} > "$staging"

render_example() {
  local title="$1" project="$2" source="$3"
  local observed
  observed="$(dotnet run --project "$root_dir/$project" --no-build --no-restore --nologo 2>&1)"
  {
    printf '## %s\n\n' "$title"
    printf 'Run it:\n\n```bash\ndotnet run --project %s --nologo\n```\n\n' "$project"
    printf 'Source: [%s](https://github.com/adz/Axial/blob/main/%s)\n\n' "$(basename "$source")" "$source"
    printf '```fsharp\n'
    cat "$root_dir/$source"
    printf '\n```\n\nObserved output:\n\n```text\n%s\n```\n\n' "$observed"
  } >> "$staging"
}

render_example 'Playground' 'examples/Axial.Playground/Axial.Playground.fsproj' 'examples/Axial.Playground/Program.fs'
render_example 'Maintenance patterns' 'examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj' 'examples/Axial.MaintenanceExamples/Program.fs'
render_example 'Supervision and fiber observability' 'examples/Axial.Examples/Axial.Examples.fsproj' 'examples/Axial.Examples/SupervisionExample.fs'

mkdir -p "$(dirname "$output")"
mv "$staging" "$output"
