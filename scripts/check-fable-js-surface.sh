#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/examples/Axial.Hosting.Browser.Example/Axial.Hosting.Browser.Example.fsproj"
out_dir="$root_dir/artifacts/fable-js-surface"

rm -rf "$out_dir"
dotnet fable "$project" --lang javascript --outDir "$out_dir"
test -f "$out_dir/Program.js"

if grep -R "ColdTask" "$out_dir" >/dev/null; then
  echo "ColdTask leaked into the Fable JavaScript output." >&2
  exit 1
fi

echo "Axial browser surface compiles with Fable and excludes .NET-only ColdTask."
