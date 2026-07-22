#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

product="${1:-all}"

case "$product" in
  validation)
    dotnet build "$root_dir/src/Axial.Validation/Axial.Validation.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    cd "$root_dir/scripts/docgen"
    AXIAL_DOCS_PRODUCT="$product" dotnet run
    ;;
  schema)
    dotnet build "$root_dir/src/Axial.Schema.Json/Axial.Schema.Json.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Schema.JsonSchema/Axial.Schema.JsonSchema.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Schema.Http.AspNetCore/Axial.Schema.Http.AspNetCore.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Schema.Http.GenHttp/Axial.Schema.Http.GenHttp.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    cd "$root_dir/scripts/docgen"
    AXIAL_DOCS_PRODUCT="$product" dotnet run
    ;;
  flow)
    dotnet build "$root_dir/src/Axial.Flow.Process/Axial.Flow.Process.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Flow.Hosting/Axial.Flow.Hosting.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Flow.Hosting.Node/Axial.Flow.Hosting.Node.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    dotnet build "$root_dir/src/Axial.Flow.Hosting.Browser/Axial.Flow.Hosting.Browser.fsproj" --nologo --verbosity quiet --disable-build-servers -p:UseSharedCompilation=false
    cd "$root_dir/scripts/docgen"
    AXIAL_DOCS_PRODUCT="$product" dotnet run
    ;;
  all)
    "$root_dir/scripts/generate-api-docs.sh" validation
    "$root_dir/scripts/generate-api-docs.sh" schema
    "$root_dir/scripts/generate-api-docs.sh" flow
    ;;
  *)
    echo "Usage: $0 [validation|schema|flow|all]" >&2
    exit 2
    ;;
esac
