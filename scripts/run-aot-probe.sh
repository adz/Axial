#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/examples/Axial.AotProbe/Axial.AotProbe.fsproj"
publish_dir="$root_dir/artifacts/publish/Axial.AotProbe/linux-x64"
publish_log="$root_dir/artifacts/publish/Axial.AotProbe/publish.log"

mkdir -p "$(dirname "$publish_log")"
dotnet publish "$project" -c Release -r linux-x64 -o "$publish_dir" 2>&1 | tee "$publish_log"

if grep -Eqi 'warning IL[0-9]{4}' "$publish_log"; then
  echo "NativeAOT publish emitted IL warnings; see $publish_log" >&2
  exit 1
fi

"$publish_dir/Axial.AotProbe"
