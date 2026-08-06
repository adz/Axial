#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/examples/Axial.AotProbe/Axial.AotProbe.fsproj"
publish_dir="$root_dir/artifacts/publish/Axial.AotProbe/linux-x64"

dotnet publish "$project" -c Release -r linux-x64 -o "$publish_dir"
"$publish_dir/Axial.AotProbe"
