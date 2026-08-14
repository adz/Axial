#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

output_dir="artifacts/package"

mkdir -p "$output_dir"
find "$output_dir" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

VERSION=""
while getopts "v:" opt; do
  case $opt in
    v) VERSION="$OPTARG" ;;
    *) echo "Usage: $0 [-v <axial-version>]"; exit 1 ;;
  esac
done

version_args=()
if [[ -n "$VERSION" ]]; then
  version_args+=("-p:Version=$VERSION")
fi

projects=(
  "src/Axial/Axial.fsproj"
  "src/Axial.Console/Axial.Console.fsproj"
  "src/Axial.FileSystem/Axial.FileSystem.fsproj"
  "src/Axial.HttpClient/Axial.HttpClient.fsproj"
  "src/Axial.Process/Axial.Process.fsproj"
  "src/Axial.PlatformService/Axial.PlatformService.fsproj"
  "src/Axial.Hosting/Axial.Hosting.fsproj"
  "src/Axial.Hosting.Node/Axial.Hosting.Node.fsproj"
  "src/Axial.Hosting.Browser/Axial.Hosting.Browser.fsproj"
  "src/Axial.Telemetry/Axial.Telemetry.fsproj"
  "src/Axial.Telemetry.JavaScript/Axial.Telemetry.JavaScript.fsproj"
  "src/Axial.Guardrails/Axial.Guardrails.fsproj"
)

echo "Packing projects to $output_dir..."

for project in "${projects[@]}"; do
  echo "--- Packing $(basename "$project") ---"
  dotnet pack "$project" --configuration Release --output "$output_dir" "${version_args[@]}"
done

echo "Done. Packages are in $output_dir"
ls -1 "$output_dir"/*.nupkg
