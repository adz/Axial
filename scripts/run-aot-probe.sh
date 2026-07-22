#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for product in Result Diagnostics Refined Schema Flow; do
  project="$ROOT_DIR/examples/Axial.$product.AotProbe/Axial.$product.AotProbe.fsproj"
  publish_dir="$ROOT_DIR/artifacts/publish/Axial.$product.AotProbe/linux-x64"

  dotnet publish "$project" -c Release -r linux-x64 -p:PublishAot=true -o "$publish_dir"
  "$publish_dir/Axial.$product.AotProbe"
done
