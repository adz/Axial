#!/usr/bin/env bash

# Proves new tests can fail: runs them against the source as it was at <base>, keeping the current tests. In a
# temporary detached worktree it checks out the current commit, restores src/ from <base>, and runs `dotnet test`
# with the filter. The check passes when those tests fail or no longer compile there. Commit (or checkpoint) the work
# first: the worktree is built from HEAD.
#
#   scripts/check-tests-fail-without-fix.sh <base> <test project> <dotnet test filter>
#   scripts/check-tests-fail-without-fix.sh origin/main tests/Axial.Tests "FullyQualifiedName~CallbackStream"

set -uo pipefail

base="${1:?usage: check-tests-fail-without-fix.sh <base> <test project> <filter>}"
project="${2:?test project path required}"
filter="${3:?dotnet test filter required}"

repo="$(git rev-parse --show-toplevel)"
scratch="$(mktemp -d)"
trap 'git -C "$repo" worktree remove --force "$scratch/tree" >/dev/null 2>&1; rm -rf "$scratch"' EXIT

git -C "$repo" worktree add --detach "$scratch/tree" HEAD >/dev/null
git -C "$scratch/tree" checkout "$base" -- src

if git -C "$scratch/tree" diff --quiet HEAD -- src; then
    echo "src/ is identical at $base and HEAD; nothing to revert." >&2
    exit 2
fi

cd "$scratch/tree"
timeout 1800 dotnet test "$project" -c Release --filter "$filter" >"$scratch/test.log" 2>&1
code=$?

if grep -qE "error FS|error CS" "$scratch/test.log"; then
    echo "OK: the tests do not compile against src/ at $base (they use the new API)."
    exit 0
elif [[ $code -ne 0 ]] && grep -qE "Failed: +[1-9]" "$scratch/test.log"; then
    echo "OK: the tests fail against src/ at $base:"
    grep -oE "^\s+Failed [^ ]+" "$scratch/test.log" | sed 's/^ */  /' | sort -u | head -20
    exit 0
elif grep -qE "Total: +0|No test matches" "$scratch/test.log"; then
    echo "No tests matched filter '$filter'." >&2
    exit 2
else
    echo "FAIL: the tests pass without the fix, so they do not prove it." >&2
    tail -5 "$scratch/test.log" >&2
    exit 1
fi
