#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet build "$root_dir/Axial.slnx" --nologo -v minimal
bash "$root_dir/scripts/run-livedocs.sh" build
