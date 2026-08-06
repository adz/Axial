#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

output_dir="artifacts/package"

mkdir -p "$output_dir"
find "$output_dir" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

# Axial and FsFlow are separate release trains with separate version properties (see
# Directory.Build.props). Overriding one must not move the other, so -v sets the Axial version and
# -f sets the FsFlow version; either may be omitted to take the checked-in default.
VERSION=""
FSFLOW_VERSION=""
while getopts "v:f:" opt; do
  case $opt in
    v) VERSION="$OPTARG" ;;
    f) FSFLOW_VERSION="$OPTARG" ;;
    *) echo "Usage: $0 [-v <axial-version>] [-f <fsflow-version>]"; exit 1 ;;
  esac
done

version_args=()
if [[ -n "$VERSION" ]]; then
  version_args+=("-p:AxialVersion=$VERSION")
fi
if [[ -n "$FSFLOW_VERSION" ]]; then
  version_args+=("-p:FsFlowVersion=$FSFLOW_VERSION")
fi

projects=(
  "src/Axial/Axial.fsproj"
  "src/Axial.Data/Axial.Data.fsproj"
  "src/Axial.Result/Axial.Result.fsproj"
  "src/Axial.Constraint/Axial.Constraint.fsproj"
  "src/Axial.Refined/Axial.Refined.fsproj"
  "src/Axial.Parse/Axial.Parse.fsproj"
  "src/Axial.Schema/Axial.Schema.fsproj"
  "src/Axial.Schema.Json/Axial.Schema.Json.fsproj"
  "src/Axial.Schema.Http/Axial.Schema.Http.fsproj"
  "src/Axial.Schema.Http.AspNetCore/Axial.Schema.Http.AspNetCore.fsproj"
  "src/Axial.Schema.Http.GenHttp/Axial.Schema.Http.GenHttp.fsproj"
  "src/Axial.Schema.Contracts.Build/Axial.Schema.Contracts.Build.fsproj"
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
)

echo "Packing projects to $output_dir..."

for project in "${projects[@]}"; do
  echo "--- Packing $(basename "$project") ---"
  dotnet pack "$project" --configuration Release --output "$output_dir" "${version_args[@]}"
done

echo "Done. Packages are in $output_dir"
ls -1 "$output_dir"/*.nupkg
