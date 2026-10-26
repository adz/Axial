#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
verify="$root/scripts/verify.sh"

if grep -q 'AXIAL_VERIFY_LOCKED' "$verify"; then
  echo 'verify lock must not have an environment bypass' >&2
  exit 1
fi
grep -Eq 'exec flock -o .*run\.lock' "$verify" || {
  echo 'verify lock must close its descriptor in children with flock -o' >&2
  exit 1
}

# Prove why -o is required: a child inheriting the descriptor keeps the lock,
# while close-on-exec lets another process acquire it as soon as the owner exits.
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
flock "$tmp/lock" bash -c 'sleep 2 &' 
sleep 0.1
if flock -n "$tmp/lock" true; then
  echo 'control lock unexpectedly released by inheriting child' >&2
  exit 1
fi
sleep 2
flock -o "$tmp/lock" bash -c 'sleep 2 &' 
sleep 0.1
flock -n "$tmp/lock" true || {
  echo 'close-on-exec lock remained held by child' >&2
  exit 1
}
