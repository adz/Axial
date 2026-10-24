#!/usr/bin/env bash

set -euo pipefail

source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/scripts" "$tmp/tests/Axial.Guardrails.Tests"
cp "$source_root/scripts/check-guardrails-baseline-growth.sh" "$tmp/scripts/"
cp "$source_root/tests/Axial.Guardrails.Tests/baseline-grammar-vectors.tsv" "$tmp/tests/Axial.Guardrails.Tests/"
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

# Exercise every shared grammar vector against the shell parser.
hash=$(printf '%064d' 0)
while IFS=$'\t' read -r name expectation encoded_record expected; do
    [[ -z $name || $name == \#* ]] && continue
    record=${encoded_record//\{hash\}/$hash}
    record=${record//\\s/ }
    record=${record//\\t/$'\t'}
    record=${record//\\\\/\\}
    printf '# baseline\n%s\n' "$record" > Axial.Guardrails.baseline

    case $expectation:$expected in
        valid:*'|'*)
            git add Axial.Guardrails.baseline
            git commit -qm "valid vector: $name"
            scripts/check-guardrails-baseline-growth.sh HEAD >/dev/null
            git reset -q --hard "$base"
            ;;
        invalid:error)
            if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
                echo "invalid baseline vector unexpectedly passed: $name" >&2
                exit 1
            fi
            ;;
        *)
            echo "baseline vector has invalid expectation: $name" >&2
            exit 1
            ;;
    esac
done < tests/Axial.Guardrails.Tests/baseline-grammar-vectors.tsv

printf '# baseline\nAXG001\tsrc/Old.fs\t%064d\nAXG001\tsrc/Old.fs\t%064d\n' 0 0 > Axial.Guardrails.baseline
if scripts/check-guardrails-baseline-growth.sh "$base" >/dev/null 2>&1; then
    echo 'duplicate baseline lines unexpectedly passed' >&2
    exit 1
fi

# A new branch's all-zero base is handled as an empty baseline.
printf '# baseline\n' > Axial.Guardrails.baseline
scripts/check-guardrails-baseline-growth.sh 0000000000000000000000000000000000000000 >/dev/null

echo 'Guardrails baseline growth tests passed.'
