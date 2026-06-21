#!/usr/bin/env bash

set -euo pipefail

read -r -p "Stop other processes and run the Fable benchmark suites for Node and Erlang? [y/N] " answer

case "$answer" in
  y|Y|yes|YES)
    ;;
  *)
    echo "Benchmark run cancelled."
    exit 1
    ;;
esac

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/benchmarks/Axial.Benchmarks.Fable/Axial.Benchmarks.Fable.fsproj"
project_dir="$root_dir/benchmarks/Axial.Benchmarks.Fable"
node_out="$root_dir/artifacts/fable-benchmarks/node"
beam_out="$root_dir/artifacts/fable-benchmarks/beam"
mise_state_dir="/tmp/axial-mise-state"
mise_cache_dir="/tmp/axial-mise-cache"

rm -rf "$node_out" "$beam_out"
mkdir -p "$node_out" "$beam_out"
mkdir -p "$mise_state_dir" "$mise_cache_dir"

printf '%s\n' '{ "type": "module" }' > "$node_out/package.json"

XDG_STATE_HOME="$mise_state_dir" XDG_CACHE_HOME="$mise_cache_dir" mise trust -C "$project_dir" --all -y >/dev/null

echo "Running Fable benchmark suite on Node..."
XDG_STATE_HOME="$mise_state_dir" XDG_CACHE_HOME="$mise_cache_dir" mise exec -C "$project_dir" -- dotnet fable "$project" --lang javascript --define BENCHMARK_NODE --outDir "$node_out"
XDG_STATE_HOME="$mise_state_dir" XDG_CACHE_HOME="$mise_cache_dir" mise exec -C "$project_dir" -- node "$node_out/Program.js"

echo "Running Fable benchmark suite on Erlang/BEAM..."
XDG_STATE_HOME="$mise_state_dir" XDG_CACHE_HOME="$mise_cache_dir" mise exec -C "$project_dir" -- dotnet fable "$project" --lang beam --define BENCHMARK_BEAM --outDir "$beam_out"
beam_ebin="$beam_out/_build/default/lib/axial_benchmarks_fable/ebin"
rm -rf "$beam_ebin"
mkdir -p "$beam_ebin"
find "$beam_out" -name '*.erl' -print0 | xargs -0 /home/adam/.local/share/mise/installs/erlang/27.2.2/bin/erlc -o "$beam_ebin"
/home/adam/.local/share/mise/installs/erlang/27.2.2/bin/erl -noshell -pa "$beam_ebin" -eval 'program:program_main(argv), halt().'
