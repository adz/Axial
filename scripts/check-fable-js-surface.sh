#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/benchmarks/FsFlow.Benchmarks.Fable/FsFlow.Benchmarks.Fable.fsproj"
out_dir="$root_dir/artifacts/fable-js-surface"

rm -rf "$out_dir"
mkdir -p "$out_dir"
printf '%s\n' '{ "type": "module" }' > "$out_dir/package.json"

dotnet fable "$project" --lang javascript --define BENCHMARK_NODE --outDir "$out_dir"
node "$out_dir/Program.js" >/dev/null

if grep -R "ColdTask" "$out_dir" >/dev/null; then
  echo "ColdTask leaked into the Fable JavaScript output." >&2
  exit 1
fi

echo "Fable JavaScript surface compiles and excludes .NET-only ColdTask."
