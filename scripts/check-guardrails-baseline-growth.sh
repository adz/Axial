#!/usr/bin/env bash

# Rejects malformed baseline entries and any entry newly added relative to the supplied base commit.
# Usage: scripts/check-guardrails-baseline-growth.sh [base-sha]

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

zero_sha=0000000000000000000000000000000000000000
base=${1:-${GUARDRAILS_BASE_SHA:-}}
if [[ -z $base ]]; then
    if git rev-parse --verify --quiet HEAD^ >/dev/null; then
        base=HEAD^
    else
        base=$zero_sha
    fi
fi

base_file=$(mktemp)
trap 'rm -f "$base_file"' EXIT
if [[ $base == "$zero_sha" ]]; then
    : > "$base_file"
elif git cat-file -e "$base:Axial.Guardrails.baseline" 2>/dev/null; then
    git show "$base:Axial.Guardrails.baseline" > "$base_file"
elif git cat-file -e "$base^{commit}" 2>/dev/null; then
    : > "$base_file"
else
    echo "Guardrails baseline growth: base commit '$base' is unavailable; fetch the CI base SHA." >&2
    exit 2
fi

python3 - "$base_file" Axial.Guardrails.baseline "$base" <<'PY'
from collections import Counter
from pathlib import PurePosixPath
import re
import sys

base_path, current_path, base_name = sys.argv[1:]
syntax = re.compile(r"(AXG[0-9]{3})\t([^\t]+)\t([0-9a-f]{64})(?: x([2-9][0-9]*))?", re.IGNORECASE)

def parse(path: str, require_counted_duplicates: bool) -> Counter[tuple[str, str, str]]:
    entries: Counter[tuple[str, str, str]] = Counter()
    with open(path, encoding="utf-8") as stream:
        for number, raw in enumerate(stream, 1):
            line = raw.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            match = syntax.fullmatch(line)
            if not match:
                raise ValueError(f"{path}:{number}: non-canonical Guardrails baseline line")
            rule_id, file_name, fingerprint, count_text = match.groups()
            count = int(count_text) if count_text else 1
            parts = PurePosixPath(file_name).parts
            canonical = (
                rule_id == rule_id.upper()
                and fingerprint == fingerprint.lower()
                and "\\" not in file_name
                and not file_name.startswith("/")
                and all(part not in ("", ".", "..") for part in parts)
            )
            if not canonical:
                raise ValueError(f"{path}:{number}: non-canonical Guardrails baseline line")
            key = (rule_id, file_name, fingerprint)
            if key in entries and require_counted_duplicates:
                raise ValueError(f"{path}:{number}: duplicate Guardrails baseline entry; use a trailing count")
            entries[key] += count
    return entries

try:
    # Historical bases may use the former repeated-line multiset form; only the current file must be canonical.
    base = parse(base_path, False)
    current = parse(current_path, True)
except ValueError as error:
    print(error, file=sys.stderr)
    raise SystemExit(1)

added = current - base
if added:
    print(f"Guardrails baseline grew relative to {base_name}:", file=sys.stderr)
    for (rule_id, file_name, fingerprint), count in sorted(added.items()):
        suffix = f" x{count}" if count > 1 else ""
        print(f"  + {rule_id}\t{file_name}\t{fingerprint}{suffix}", file=sys.stderr)
    print("Fix new violations; replacing or removing another entry does not permit new baseline debt.", file=sys.stderr)
    raise SystemExit(1)

print(f"Guardrails baseline growth: pass ({sum(current.values())} entries; base {sum(base.values())} at {base_name})")
PY
