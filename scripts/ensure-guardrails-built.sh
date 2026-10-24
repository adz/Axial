#!/usr/bin/env bash

# Build this repository's live Guardrails analyzer before an entry point builds projects that consume it.
# Package consumers do not use this script: their analyzer is already present in the restored package.

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"
analyzer="$root_dir/artifacts/bin/Axial.Guardrails/${configuration,,}/Axial.Guardrails.Analyzers.dll"

if [[ ! -f "$analyzer" ]]; then
  dotnet build "$root_dir/src/Axial.Guardrails/Axial.Guardrails.fsproj" -c "$configuration" --nologo -v quiet
fi
