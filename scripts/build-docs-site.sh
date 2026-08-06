#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-"/"}"

for project in \
  "src/Axial/Axial.fsproj" \
  "src/Axial.PlatformService/Axial.PlatformService.fsproj" \
  "src/Axial.Console/Axial.Console.fsproj" \
  "src/Axial.FileSystem/Axial.FileSystem.fsproj" \
  "src/Axial.HttpClient/Axial.HttpClient.fsproj" \
  "src/Axial.Process/Axial.Process.fsproj"
do
  dotnet build "$root_dir/$project" --nologo -v minimal
done

"$root_dir/scripts/generate-example-docs.sh" flow
bash "$root_dir/scripts/generate-api-docs.sh" flow
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --baseURL "$HUGO_BASEURL" --cleanDestinationDir
