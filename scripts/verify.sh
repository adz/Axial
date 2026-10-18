#!/usr/bin/env bash

# Runs the repository's verification stages with fixed timeouts and writes a machine-readable summary to
# artifacts/verify/result.json, with one log per stage beside it. Agents report this file, not their own reading of
# the output. Stages: build test fable aot docs (default: all, in that order). FABLE_RUNS sets how many times the
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
[[ ${#stages[@]} -eq 0 ]] && stages=(build test fable aot docs)

mkdir -p "$out_dir"
cd "$root_dir"

results=()
failed=0

json_escape() { printf '%s' "$1" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))'; }

record() {
    local stage=$1 status=$2 seconds=$3 summary=$4
    results+=("{\"stage\":\"$stage\",\"status\":\"$status\",\"seconds\":$seconds,\"summary\":$(json_escape "$summary"),\"log\":\"artifacts/verify/$stage.log\"}")
    [[ $status == pass ]] || failed=1
    printf '%-6s %-4s %4ss  %s\n' "$stage" "$status" "$seconds" "$summary"
}

run_build() {
    timeout 1800 dotnet build Axial.slnx -c Release >"$out_dir/build.log" 2>&1
    local code=$?
    local errors
    errors=$(grep -c " error " "$out_dir/build.log")
    [[ $code -eq 0 ]] && echo "pass|0 errors" || echo "fail|exit $code, $errors error lines"
}

run_test() {
    timeout 2400 dotnet test Axial.slnx -c Release --no-build >"$out_dir/test.log" 2>&1
    local code=$?
    local passed failed_count projects
    passed=$(grep -oE "Passed: +[0-9]+" "$out_dir/test.log" | awk '{s+=$2} END {print s+0}')
    failed_count=$(grep -oE "Failed: +[0-9]+" "$out_dir/test.log" | awk '{s+=$2} END {print s+0}')
    projects=$(grep -cE "^(Passed|Failed)!" "$out_dir/test.log")
    if [[ $code -eq 0 && $failed_count -eq 0 && $projects -gt 0 ]]; then
        echo "pass|$passed passed, 0 failed, $projects projects"
    else
        local names
        names=$(grep -E "^\s+Failed .*\[[0-9.]+ m?s\]$" "$out_dir/test.log" | sed -E 's/^ *Failed //; s/ \[[0-9.]+ m?s\]$//' | sort -u | head -5 | paste -sd';' -)
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
    { bash scripts/check-docs-conventions.sh && bash scripts/check-source-inventory.sh; } >"$out_dir/docs.log" 2>&1 \
        && echo "pass|docs conventions and source inventory" || echo "fail|$(tail -1 "$out_dir/docs.log")"
}

for stage in "${stages[@]}"; do
    case $stage in
        build | test | fable | aot | docs) ;;
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
