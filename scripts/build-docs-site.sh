#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-"/"}"

for project in \
  "src/Axial/Axial.fsproj" \
  "src/Axial.Flow.PlatformService/Axial.Flow.PlatformService.fsproj" \
  "src/Axial.Flow.Console/Axial.Flow.Console.fsproj" \
  "src/Axial.Flow.FileSystem/Axial.Flow.FileSystem.fsproj" \
  "src/Axial.Flow.Http/Axial.Flow.Http.fsproj" \
  "src/Axial.Flow.Process/Axial.Flow.Process.fsproj"
do
  dotnet build "$root_dir/$project" --nologo -v minimal
done

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --baseURL "$HUGO_BASEURL" --cleanDestinationDir
