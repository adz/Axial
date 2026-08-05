#!/usr/bin/env bash

# Package-consumer fixtures: pack the Axial packages, then restore and run tiny projects that install
# them the way an outside consumer would. Project references inside the solution hide missing package
# files, wrong dependency ranges, broken build targets, and source-order problems; these fixtures do
# not, because they only ever see the .nupkg.
#
# Usage: scripts/run-package-consumers.sh [-v <axial-version>] [--no-pack]

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

fixtures_dir="tests/package-consumers"
skip_pack=false
version=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v) version="$2"; shift 2 ;;
    --no-pack) skip_pack=true; shift ;;
    *) echo "Usage: $0 [-v <axial-version>] [--no-pack]" >&2; exit 2 ;;
  esac
done

if [[ -z "$version" ]]; then
  version="$(dotnet msbuild src/Axial.Result/Axial.Result.fsproj -getProperty:Version -nologo | tr -d '[:space:]')"
fi

echo "Testing Axial $version as an installed package."

if ! $skip_pack; then
  ./scripts/pack.sh -v "$version"
fi

# A stale package of the same version in the global cache would shadow what we just packed, so the
# fixtures must never restore from it.
for package in Axial.Result Axial.Parse Axial.Constraint Axial.Refined Axial.Data Axial.Schema; do
  cached="$HOME/.nuget/packages/$(echo "$package" | tr '[:upper:]' '[:lower:]')/$version"
  if [[ -d "$cached" ]]; then
    echo "Evicting cached $package $version"
    rm -rf "$cached"
  fi
done

failures=()

for fixture in "$fixtures_dir"/Consumer.*; do
  name="$(basename "$fixture")"
  echo
  echo "=== $name ==="

  if dotnet run --project "$fixture/$name.fsproj" \
      -p:AxialPackageVersion="$version" \
      --configuration Release --nologo; then
    echo "$name passed"
  else
    echo "$name FAILED" >&2
    failures+=("$name")
  fi
done

echo
if [[ ${#failures[@]} -gt 0 ]]; then
  echo "Package-consumer fixtures failed: ${failures[*]}" >&2
  exit 1
fi

echo "All package-consumer fixtures passed."
