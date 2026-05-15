#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-"https://adz.github.io/FsFlow/"}"

for project in \
  "src/FsFlow/FsFlow.fsproj" \
  "src/FsFlow.Capabilities.Core/FsFlow.Capabilities.Core.fsproj" \
  "src/FsFlow.Capabilities.Console/FsFlow.Capabilities.Console.fsproj" \
  "src/FsFlow.Capabilities.FileSystem/FsFlow.Capabilities.FileSystem.fsproj" \
  "src/FsFlow.Capabilities.Http/FsFlow.Capabilities.Http.fsproj" \
  "src/FsFlow.Capabilities.Process/FsFlow.Capabilities.Process.fsproj"
do
  dotnet build "$root_dir/$project" --nologo -v minimal
done

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --baseURL "$HUGO_BASEURL" --cleanDestinationDir
