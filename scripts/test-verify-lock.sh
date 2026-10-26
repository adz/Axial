#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
verify="$root/scripts/verify.sh"

if grep -qE 'AXIAL_VERIFY_LOCKED|--verify-lock-held' "$verify"; then
  echo 'verify lock must not have an environment or argument bypass' >&2
  exit 1
fi
grep -Eq 'exec 9>.*run\.lock' "$verify" || {
  echo 'verify must own the run lock in its original process' >&2
  exit 1
}
grep -Eq 'outcome=.*run_.*9>&-' "$verify" || {
  echo 'verify stages must close the lock descriptor in children' >&2
  exit 1
}

tmp=$(mktemp -d)
verify_pid=
holder_pid=
cleanup() {
  [[ -z $verify_pid ]] || kill -- -"$verify_pid" 2>/dev/null || true
  [[ -z $holder_pid ]] || kill "$holder_pid" 2>/dev/null || true
  rm -rf "$tmp"
}
trap cleanup EXIT

# A caller-supplied former bypass must still wait behind the real run lock.
printf 'untouched\n' > "$root/artifacts/verify/docs.log"
flock "$root/artifacts/verify/run.lock" bash -c 'touch "$1"; sleep 5' _ "$tmp/held" &
holder_pid=$!
while [[ ! -e $tmp/held ]]; do sleep 0.02; done
setsid bash "$verify" --verify-lock-held docs >"$tmp/verify.out" 2>&1 &
verify_pid=$!
sleep 0.3
[[ $(cat "$root/artifacts/verify/docs.log") == untouched ]] || {
  echo 'former argument bypass started a stage while the lock was held' >&2
  exit 1
}
kill -- -"$verify_pid" 2>/dev/null || true
verify_pid=
kill "$holder_pid" 2>/dev/null || true
wait "$holder_pid" 2>/dev/null || true
holder_pid=

# Closing fd 9 for a stage means a leftover child cannot retain the lock.
(
  exec 9>"$tmp/lock"
  flock 9
  bash -c 'sleep 2 &' 9>&-
)
sleep 0.1
flock -n "$tmp/lock" true || {
  echo 'leftover stage child retained the lock' >&2
  exit 1
}
