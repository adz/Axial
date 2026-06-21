#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="$root_dir/artifacts/package"

# Ensure output directory exists and is clean
rm -rf "$output_dir"
mkdir -p "$output_dir"

# Default version from Axial.fsproj if not provided via -v
VERSION=""
while getopts "v:" opt; do
  case $opt in
    v) VERSION="$OPTARG" ;;
    *) echo "Usage: $0 [-v <version>]"; exit 1 ;;
  esac
done

# Current released projects (only core Axial for now)
projects=(
  "src/Axial/Axial.fsproj"
)

echo "Packing projects to $output_dir..."

for project in "${projects[@]}"; do
  echo "--- Packing $(basename "$project") ---"
  if [[ -n "$VERSION" ]]; then
    dotnet pack "$root_dir/$project" --configuration Release --output "$output_dir" -p:Version="$VERSION"
  else
    dotnet pack "$root_dir/$project" --configuration Release --output "$output_dir"
  fi
done

echo "Done. Packages are in $output_dir"
ls -1 "$output_dir"/*.nupkg
