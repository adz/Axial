#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fslivedocs_root="${FSLIVEDOCS_ROOT:-"$(cd "$root_dir/../../FsLiveDocs/main" 2>/dev/null && pwd || true)"}"

if [ -z "$fslivedocs_root" ] || [ ! -f "$fslivedocs_root/src/FsLiveDocs.Cli/FsLiveDocs.Cli.fsproj" ]; then
  echo "FsLiveDocs was not found. Set FSLIVEDOCS_ROOT to its repository root." >&2
  exit 1
fi

command_name="${1:-}"
if [ -z "$command_name" ]; then
  echo "Usage: $0 <build|test|watch>" >&2
  exit 2
fi
shift

projects=(
  src/Axial/Axial.fsproj
  src/Axial.PlatformService/Axial.PlatformService.fsproj
  src/Axial.Console/Axial.Console.fsproj
  src/Axial.FileSystem/Axial.FileSystem.fsproj
  src/Axial.HttpClient/Axial.HttpClient.fsproj
  src/Axial.Process/Axial.Process.fsproj
  src/Axial.Hosting/Axial.Hosting.fsproj
  src/Axial.Hosting.Node/Axial.Hosting.Node.fsproj
  src/Axial.Hosting.Browser/Axial.Hosting.Browser.fsproj
  src/Axial.Telemetry/Axial.Telemetry.fsproj
  src/Axial.Telemetry.JavaScript/Axial.Telemetry.JavaScript.fsproj
)

cd "$root_dir"
project_version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
case "$command_name" in
  build|watch)
    exec dotnet run --project "$fslivedocs_root/src/FsLiveDocs.Cli/FsLiveDocs.Cli.fsproj" -- "$command_name" "${projects[@]}" --version "$project_version" "$@"
    ;;
  test)
    exec dotnet run --project "$fslivedocs_root/src/FsLiveDocs.Cli/FsLiveDocs.Cli.fsproj" -- test "${projects[@]}" "$@"
    ;;
  extract)
    exec dotnet run --project "$fslivedocs_root/src/FsLiveDocs.Cli/FsLiveDocs.Cli.fsproj" -- extract "${projects[@]}" "$@"
    ;;
  build-history)
    exec dotnet run --project "$fslivedocs_root/src/FsLiveDocs.Cli/FsLiveDocs.Cli.fsproj" -- build-history "$@"
    ;;
  *)
    echo "Unsupported FsLiveDocs command: $command_name" >&2
    exit 2
    ;;
esac
