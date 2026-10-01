#!/usr/bin/env bash

# Compiles tests/Axial.Fable.Tests to JavaScript with Fable and runs it on Node. It exercises the runtime paths that
# have their own Fable implementation (timers, fibers, scopes, signals) with the queue, hub, and stream behaviour
# built on them. A failing check sets a non-zero exit code. An argument runs only the tests whose name contains it.

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/tests/Axial.Fable.Tests/Axial.Fable.Tests.fsproj"
out_dir="$root_dir/artifacts/fable-tests"

rm -rf "$out_dir"
dotnet fable "$project" --lang javascript --outDir "$out_dir"
printf '%s\n' '{ "type": "module" }' > "$out_dir/package.json"
node "$out_dir/Program.js" "$@"
node "$root_dir/scripts/check-fable-js-bridge.mjs"
