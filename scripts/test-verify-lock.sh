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

# Exercise a disposable verify tree so this test can itself run inside verification's lock.
fixture="$tmp/root"
mkdir -p "$fixture/scripts" "$fixture/experiments/flow-errors" "$fixture/artifacts/verify"
cp "$verify" "$fixture/scripts/verify.sh"
for script in check-docs-conventions.sh check-source-inventory.sh test-verify-summary.sh test-verify-lock.sh test-guardrails-v2-migration.sh; do
  printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/scripts/$script"
done
printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/experiments/flow-errors/check.sh"
printf 'raise SystemExit(0)\n' > "$fixture/scripts/check-package-maturity.py"

# A caller-supplied former bypass must still wait behind the fixture's real run lock.
printf 'untouched\n' > "$fixture/artifacts/verify/docs.log"
flock "$fixture/artifacts/verify/run.lock" bash -c 'touch "$1"; sleep 5' _ "$tmp/held" &
holder_pid=$!
while [[ ! -e $tmp/held ]]; do sleep 0.02; done
setsid bash "$fixture/scripts/verify.sh" --verify-lock-held docs >"$tmp/verify.out" 2>&1 &
verify_pid=$!
sleep 0.3
[[ $(cat "$fixture/artifacts/verify/docs.log") == untouched ]] || {
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
