#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/examples/Axial.AotProbe/Axial.AotProbe.fsproj"
LIBRARY_PROJECT="$ROOT_DIR/src/Axial/Axial.fsproj"
PUBLISH_DIR="$ROOT_DIR/artifacts/publish/Axial.AotProbe/linux-x64"

dotnet build "$LIBRARY_PROJECT" -c Release -f net8.0 --nologo -v minimal /m:1
dotnet publish "$PROJECT" -c Release -r linux-x64 -p:PublishAot=true -p:UsePrebuiltAxial=true -o "$PUBLISH_DIR"
"$PUBLISH_DIR/Axial.AotProbe"
