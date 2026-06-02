#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-"https://adz.github.io/FsFlow/"}"

for project in \
  "src/FsFlow/FsFlow.fsproj" \
  "src/FsFlow.Services.Core/FsFlow.Services.Core.fsproj" \
  "src/FsFlow.Services.Console/FsFlow.Services.Console.fsproj" \
  "src/FsFlow.Services.FileSystem/FsFlow.Services.FileSystem.fsproj" \
  "src/FsFlow.Services.Http/FsFlow.Services.Http.fsproj" \
  "src/FsFlow.Services.Process/FsFlow.Services.Process.fsproj"
do
  dotnet build "$root_dir/$project" --nologo -v minimal
done

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --baseURL "$HUGO_BASEURL" --cleanDestinationDir
