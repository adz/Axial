#!/usr/bin/env bash

# Build this repository's live Guardrails analyzer before an entry point builds projects that consume it.
# Package consumers do not use this script: their analyzer is already present in the restored package.

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"
# Further arguments (for example -p:Version=...) go to both builds, so a later pack with the same properties reuses them.
extra_args=("${@:2}")

# Always build (incrementally) rather than only when missing: an analyzer or policy tool left over from an older
# checkout would otherwise check the policy with stale rules, which made verify's first builds fail after a merge.
dotnet build "$root_dir/src/Axial.Guardrails/Axial.Guardrails.fsproj" -c "$configuration" --nologo -v quiet "${extra_args[@]}"
dotnet build "$root_dir/src/Axial.Tool/Axial.Tool.fsproj" -c "$configuration" --nologo -v quiet "${extra_args[@]}"
