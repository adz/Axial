#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

output_dir="artifacts/package"

mkdir -p "$output_dir"
find "$output_dir" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

# Default version comes from Directory.Build.props if not provided via -v.
VERSION=""
while getopts "v:" opt; do
  case $opt in
    v) VERSION="$OPTARG" ;;
    *) echo "Usage: $0 [-v <version>]"; exit 1 ;;
  esac
done

projects=(
  "src/Axial.Flow/Axial.Flow.fsproj"
  "src/Axial.ErrorHandling/Axial.ErrorHandling.fsproj"
  "src/Axial.Schema/Axial.Schema.fsproj"
  "src/Axial.Schema.Http/Axial.Schema.Http.fsproj"
  "src/Axial.Schema.Http.AspNetCore/Axial.Schema.Http.AspNetCore.fsproj"
  "src/Axial.Schema.Http.GenHttp/Axial.Schema.Http.GenHttp.fsproj"
  "src/Axial.Codec/Axial.Codec.fsproj"
  "src/Axial/Axial.fsproj"
  "src/Axial.Flow.Console/Axial.Flow.Console.fsproj"
  "src/Axial.Flow.FileSystem/Axial.Flow.FileSystem.fsproj"
  "src/Axial.Flow.HttpClient/Axial.Flow.HttpClient.fsproj"
  "src/Axial.Flow.Process/Axial.Flow.Process.fsproj"
  "src/Axial.Flow.PlatformService/Axial.Flow.PlatformService.fsproj"
  "src/Axial.Flow.Hosting/Axial.Flow.Hosting.fsproj"
  "src/Axial.Flow.Hosting.Node/Axial.Flow.Hosting.Node.fsproj"
  "src/Axial.Flow.Hosting.Browser/Axial.Flow.Hosting.Browser.fsproj"
  "src/Axial.Flow.Telemetry/Axial.Flow.Telemetry.fsproj"
)

echo "Packing projects to $output_dir..."

for project in "${projects[@]}"; do
  echo "--- Packing $(basename "$project") ---"
  if [[ -n "$VERSION" ]]; then
    dotnet pack "$project" --configuration Release --output "$output_dir" -p:Version="$VERSION"
  else
    dotnet pack "$project" --configuration Release --output "$output_dir"
  fi
done

echo "Done. Packages are in $output_dir"
ls -1 "$output_dir"/*.nupkg
