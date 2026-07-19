#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/benchmarks/Axial.Benchmarks.Fable/Axial.Benchmarks.Fable.fsproj"
out_dir="$root_dir/artifacts/fable-js-surface"

rm -rf "$out_dir"
mkdir -p "$out_dir"
printf '%s\n' '{ "type": "module" }' > "$out_dir/package.json"

dotnet fable "$project" --lang javascript --define BENCHMARK_NODE --outDir "$out_dir"

if [ ! -f "$out_dir/src/Axial.Schema.Codec/Json.js" ]; then
  echo "Axial.Schema.Codec's Json.fs did not compile into the Fable JavaScript output." >&2
  exit 1
fi

program_output="$(node "$out_dir/Program.js")"

if ! grep -q "Codec round-trip: ok" <<<"$program_output"; then
  echo "Axial.Schema.Codec encode/decode round-trip did not run in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Otel spans: ok" <<<"$program_output"; then
  echo "Axial.Flow.Telemetry.JavaScript spans did not record correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "App hosting: ok" <<<"$program_output"; then
  echo "App plus Node/browser hosting did not run correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if grep -R "ColdTask" "$out_dir" >/dev/null; then
  echo "ColdTask leaked into the Fable JavaScript output." >&2
  exit 1
fi

echo "Fable JavaScript surface compiles, includes Axial.Schema.Codec, and excludes .NET-only ColdTask."
