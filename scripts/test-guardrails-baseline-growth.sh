#!/usr/bin/env bash

set -euo pipefail

source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/scripts"
cp "$source_root/scripts/check-guardrails-baseline-growth.sh" "$tmp/scripts/"
cd "$tmp"
git init -q
git config user.name test
git config user.email test@example.invalid
printf '# baseline\nAXG001\tsrc/Old.fs\t%064d\n' 0 > Axial.Guardrails.baseline
git add Axial.Guardrails.baseline scripts/check-guardrails-baseline-growth.sh
git commit -qm base
base=$(git rev-parse HEAD)

# Replacing an entry is growth even though the count is unchanged.
printf '# baseline\nAXG003\tsrc/New.fs\t%064d\n' 1 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'replacement entry unexpectedly passed' >&2
    exit 1
fi

# IDs are parsed case-insensitively, then rejected unless stored canonically.
printf '# baseline\naxg001\tsrc/Old.fs\t%064d\n' 0 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'lowercase rule ID unexpectedly passed' >&2
    exit 1
fi

# Counted entries are a multiset: one extra occurrence is growth.
printf '# baseline\nAXG001\tsrc/Old.fs\t%064d x2\n' 0 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'increased baseline count unexpectedly passed' >&2
    exit 1
fi

# Counts are canonical: x1 and duplicate lines are rejected.
printf '# baseline\nAXG001\tsrc/Old.fs\t%064d x1\n' 0 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'non-canonical baseline count unexpectedly passed' >&2
    exit 1
fi
printf '# baseline\nAXG001\tsrc/Old.fs\t%064d\nAXG001\tsrc/Old.fs\t%064d\n' 0 0 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'duplicate baseline lines unexpectedly passed' >&2
    exit 1
fi

# A new branch's all-zero base is handled as an empty baseline.
printf '# baseline\n' > Axial.Guardrails.baseline
scripts/check-guardrails-baseline-growth.sh 0000000000000000000000000000000000000000 >/dev/null

echo 'Guardrails baseline growth tests passed.'
