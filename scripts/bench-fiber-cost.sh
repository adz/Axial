#!/usr/bin/env bash
set -euo pipefail

# Run only on an otherwise idle machine. Compares the benchmark-defining commit with
# the current commit using identical FiberCostBenchmarks definitions.
repo=$(git rev-parse --show-toplevel)
baseline=${FIBER_COST_BASELINE:-61a4d3b1a}
head=$(git -C "$repo" rev-parse HEAD)
output=${1:-$repo/artifacts/benchmarks/fiber-cost-$(date -u +%Y%m%dT%H%M%SZ)}
baseline_worktree=$(mktemp -d "${TMPDIR:-/tmp}/axial-fiber-cost-baseline.XXXXXX")

cleanup() {
  git -C "$repo" worktree remove --force "$baseline_worktree" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

mkdir -p "$output/baseline" "$output/head"
git -C "$repo" worktree add --detach "$baseline_worktree" "$baseline" >/dev/null

# BenchmarkDotNet creates child builds. The analyzer is unrelated to timing and the
# historical benchmark commit predates FiberCostBenchmarks' boundary declaration.
export AxialGuardrailsEnabled=false

dotnet build "$baseline_worktree/benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj" -c Release --nologo -v minimal
dotnet build "$repo/benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj" -c Release --nologo -v minimal

(
  cd "$baseline_worktree"
  dotnet run -c Release --no-build --no-restore --project benchmarks/Axial.Benchmarks -- \
    --filter '*FiberCostBenchmarks*' --artifacts "$output/baseline"
)
(
  cd "$repo"
  dotnet run -c Release --no-build --no-restore --project benchmarks/Axial.Benchmarks -- \
    --filter '*FiberCostBenchmarks*' --artifacts "$output/head"
)

printf 'baseline=%s\nhead=%s\nresults=%s\n' "$baseline" "$head" "$output"
