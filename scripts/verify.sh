#!/usr/bin/env bash

# Runs the repository's verification stages with fixed timeouts and writes a machine-readable summary to
# artifacts/verify/result.json, with one log per stage beside it. Agents report this file, not their own reading of
# the output. Stages: build test fable aot docs livedocs (default: all, in that order). livedocs compiles and runs every
# checked documentation example, as the LiveDocs workflow does, and uses the Release build from the build stage. FABLE_RUNS sets how many times the
# Fable suite runs (default 3), so an intermittent stall cannot pass on one lucky run.
#
#   scripts/verify.sh                # every stage
#   scripts/verify.sh build test     # selected stages
#
# Exits non-zero when any stage fails.

set -uo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out_dir="$root_dir/artifacts/verify"
fable_runs="${FABLE_RUNS:-3}"
stages=("$@")
[[ ${#stages[@]} -eq 0 ]] && stages=(build test fable aot docs livedocs)

mkdir -p "$out_dir"
cd "$root_dir"

# Concurrent verification used to corrupt shared logs and let one run publish another run's result.
if [[ ${AXIAL_VERIFY_LOCKED:-0} != 1 ]]; then
    exec flock "$out_dir/run.lock" env AXIAL_VERIFY_LOCKED=1 "$0" "$@"
fi

results=()
failed=0

json_escape() { printf '%s' "$1" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))'; }

record() {
    local stage=$1 status=$2 seconds=$3 summary=$4
    results+=("{\"stage\":\"$stage\",\"status\":\"$status\",\"seconds\":$seconds,\"summary\":$(json_escape "$summary"),\"log\":\"artifacts/verify/$stage.log\"}")
    [[ $status == pass ]] || failed=1
    printf '%-8s %-4s %4ss  %s\n' "$stage" "$status" "$seconds" "$summary"
}

run_build() {
    # Start as CI entry points do: no analyzer output is available yet. The two probes prevent AOT and package
    # entry points from regressing independently of the solution build.
    rm -rf artifacts/bin/Axial.Guardrails
    timeout 1800 bash scripts/ensure-guardrails-built.sh Release >"$out_dir/build.log" 2>&1
    local code=$?
    timeout 600 dotnet build examples/Axial.AotProbe/Axial.AotProbe.fsproj -c Release --nologo -v quiet >>"$out_dir/build.log" 2>&1 || code=$?
    rm -rf artifacts/bin/Axial.Guardrails
    timeout 600 bash scripts/ensure-guardrails-built.sh Release >>"$out_dir/build.log" 2>&1 || code=$?
    timeout 600 dotnet pack src/Axial/Axial.fsproj -c Release --nologo -v quiet -o artifacts/verify/package-probe >>"$out_dir/build.log" 2>&1 || code=$?
    # --no-incremental: an up-to-date project skips the Guardrails analyzer, which let new AXG findings reach CI.
    timeout 1800 dotnet build Axial.slnx -c Release --no-incremental >>"$out_dir/build.log" 2>&1 || code=$?
    local errors
    errors=$(grep -c " error " "$out_dir/build.log")
    [[ $code -eq 0 ]] && echo "pass|0 errors" || echo "fail|exit $code, $errors error lines"
}

run_test() {
    timeout 2400 dotnet test Axial.slnx -c Release --no-build >"$out_dir/test.log" 2>&1
    local code=$?
    # The shipped-assembly scan is outside Axial.slnx because it builds every shipped project, HTTP adapters included.
    timeout 1200 dotnet test tests/Axial.Build.Tests -c Release >>"$out_dir/test.log" 2>&1 || code=$?
    local passed failed_count projects
    # VSTest reports "Passed! - Failed: N, Passed: N"; Microsoft.Testing.Platform reports
    # "Test summary: total: N, failed: N, succeeded: N, skipped: N".
    read -r passed failed_count projects < <(python3 scripts/parse-test-summary.py "$out_dir/test.log")
    if [[ $code -eq 0 && $failed_count -eq 0 && $projects -gt 0 && $passed -gt 0 ]]; then
        echo "pass|$passed passed, 0 failed, $projects projects"
    else
        local names
        names=$(grep -aE "^\s+Failed .*\[[0-9.]+ m?s\]$" "$out_dir/test.log" | sed -E 's/^ *Failed //; s/ \[[0-9.]+ m?s\]$//' | sort -u | head -5 | paste -sd';' -)
        echo "fail|exit $code, $passed passed, $failed_count failed, $projects projects; failing: ${names:-see log}"
    fi
}

run_fable() {
    : >"$out_dir/fable.log"
    timeout 1500 bash scripts/run-fable-tests.sh >>"$out_dir/fable.log" 2>&1 || { echo "fail|run 1: $(tail -1 "$out_dir/fable.log")"; return; }
    local run
    for ((run = 2; run <= fable_runs; run++)); do
        echo "--- run $run" >>"$out_dir/fable.log"
        timeout 300 node artifacts/fable-tests/Program.js >>"$out_dir/fable.log" 2>&1 \
            || { echo "fail|run $run: $(tail -1 "$out_dir/fable.log")"; return; }
    done
    echo "pass|$(grep -E "^[0-9]+ tests, 0 failed" "$out_dir/fable.log" | tail -1) x$fable_runs"
}

run_aot() {
    timeout 1800 bash scripts/run-aot-probe.sh >"$out_dir/aot.log" 2>&1 \
        && echo "pass|AOT probe passed" || echo "fail|$(tail -1 "$out_dir/aot.log")"
}

run_docs() {
    { bash scripts/check-docs-conventions.sh && bash scripts/check-source-inventory.sh && bash experiments/flow-errors/check.sh && python3 scripts/check-package-maturity.py && bash scripts/test-verify-summary.sh && bash scripts/test-guardrails-v2-migration.sh && bash scripts/test-guardrails-baseline-growth.sh && bash scripts/check-guardrails-baseline-growth.sh; } >"$out_dir/docs.log" 2>&1 \
        && echo "pass|docs conventions and source inventory" || echo "fail|$(tail -1 "$out_dir/docs.log")"
}

run_livedocs() {
    timeout 1800 dotnet livedocs test --warn-as-error --interactive false --banner false >"$out_dir/livedocs.log" 2>&1 \
        && echo "pass|documentation examples compiled and ran" || echo "fail|$(grep -m1 -E "error|FAIL" "$out_dir/livedocs.log" || tail -1 "$out_dir/livedocs.log")"
}

for stage in "${stages[@]}"; do
    case $stage in
        build | test | fable | aot | docs | livedocs) ;;
        *) echo "Unknown stage: $stage" >&2; exit 2 ;;
    esac
    started=$SECONDS
    outcome=$("run_$stage")
    record "$stage" "${outcome%%|*}" $((SECONDS - started)) "${outcome#*|}"
done

{
    printf '{"commit":"%s","dirty":%s,"stages":[' "$(git rev-parse --short HEAD)" \
        "$([[ -n $(git status --porcelain) ]] && echo true || echo false)"
    (IFS=,; printf '%s' "${results[*]}")
    printf '],"passed":%s}\n' "$([[ $failed -eq 0 ]] && echo true || echo false)"
} >"$out_dir/result.json"

echo "Summary: artifacts/verify/result.json"
exit $failed
